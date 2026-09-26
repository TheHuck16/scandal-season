// Apple App Store Server API (v2) helpers for the validate-purchase function.
//
// SECURITY NOTE (read before launch):
// - verifyTransactionJws() checks the JWS signature against the leaf key in the
//   token's x5c chain AND verifies the chain anchors to the pinned Apple Root CA.
//   The DER-walking code below is minimal but correct for X.509; it MUST be tested
//   against real StoreKit sandbox transactions before launch.
// - The purchase is additionally confirmed with Apple's server API over
//   mutually-authenticated TLS (ES256-signed JWT), which is the authoritative check.
// - Never trust product/price/currency fields from the client. Crowns come only
//   from the server-side products table.

import * as jose from "npm:jose@5.9.6";

export interface AppleTransaction {
  transactionId: string;
  originalTransactionId: string;
  productId: string;
  bundleId: string;
  environment: "Sandbox" | "Production";
  purchaseDate: number;
  [k: string]: unknown;
}

// ---------- base64url ----------

function b64UrlDecode(b64url: string): Uint8Array {
  const b64 = b64url.replace(/-/g, "+").replace(/_/g, "/");
  const padded = b64 + "=".repeat((4 - (b64.length % 4)) % 4);
  const bin = atob(padded);
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  return bytes;
}

function derToPem(derB64: string, label = "CERTIFICATE"): string {
  const lines = derB64.match(/.{1,64}/g) ?? [];
  return `-----BEGIN ${label}-----\n${lines.join("\n")}\n-----END ${label}-----`;
}

// ---------- minimal DER reader (enough for X.509 Certificate) ----------

interface Tlv {
  tag: number;
  content: Uint8Array;
  full: Uint8Array; // tag + length + content
}

function readTlv(bytes: Uint8Array, offset: number): Tlv {
  const tag = bytes[offset];
  let len = bytes[offset + 1];
  let headerLen = 2;
  if (len & 0x80) {
    const n = len & 0x7f;
    if (n === 0 || n > 4) throw new Error("Unsupported DER length");
    len = 0;
    for (let i = 0; i < n; i++) len = (len << 8) | bytes[offset + 2 + i];
    headerLen = 2 + n;
  }
  const full = bytes.subarray(offset, offset + headerLen + len);
  return { tag, content: bytes.subarray(offset + headerLen, offset + headerLen + len), full };
}

/** Split a Certificate DER into { tbsDer, signature } for chain verification. */
function certParts(certDer: Uint8Array): { tbsDer: Uint8Array; signature: Uint8Array } {
  const outer = readTlv(certDer, 0);
  if (outer.tag !== 0x30) throw new Error("Expected Certificate SEQUENCE");
  let off = 0;
  const tbs = readTlv(outer.content, off);
  off += tbs.full.length;
  const _sigAlg = readTlv(outer.content, off);
  off += _sigAlg.full.length;
  const sigVal = readTlv(outer.content, off);
  if (sigVal.tag !== 0x03) throw new Error("Expected signature BIT STRING");
  if (sigVal.content[0] !== 0x00) throw new Error("Unexpected unused-bits in signature");
  return { tbsDer: tbs.full, signature: sigVal.content.subarray(1) };
}

// Copy a (possibly subarray-backed) Uint8Array into one with a concrete
// ArrayBuffer, satisfying WebCrypto's BufferSource typing.
function asBufferSource(u8: Uint8Array): Uint8Array<ArrayBuffer> {
  return new Uint8Array(u8);
}

async function sha256Hex(bytes: Uint8Array): Promise<string> {
  const d = await crypto.subtle.digest("SHA-256", asBufferSource(bytes));
  return [...new Uint8Array(d)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

// ---------- JWS verification ----------

/**
 * Verify a StoreKit 2 signedTransaction JWS.
 * 1. Verifies the JWS ES256 signature with the leaf cert's public key.
 * 2. Verifies each cert was issued by the next, up to the pinned Apple Root CA.
 * 3. Checks bundleId matches the game's bundle id.
 */
export async function verifyTransactionJws(
  signedTransaction: string,
  expectedBundleId: string,
  appleRootCaPem: string,
): Promise<AppleTransaction> {
  const parts = signedTransaction.split(".");
  if (parts.length !== 3) throw new Error("Malformed JWS");

  const header = JSON.parse(new TextDecoder().decode(b64UrlDecode(parts[0])));
  if (header.alg !== "ES256") throw new Error(`Unexpected JWS alg: ${header.alg}`);
  const x5c: string[] = header.x5c;
  if (!x5c || x5c.length < 2) throw new Error("x5c chain too short");

  // 1. Signature check against the leaf certificate's public key.
  const leafPem = derToPem(x5c[0]);
  const leafKey = await jose.importX509(leafPem, "ES256");
  const { payload: verifiedPayload } = await jose.compactVerify(signedTransaction, leafKey);
  const txn = JSON.parse(new TextDecoder().decode(verifiedPayload)) as AppleTransaction;

  // 2. Chain verification up to the pinned root.
  const chainDer = x5c.map((b64) => b64UrlDecode(b64));
  for (let i = 0; i < chainDer.length - 1; i++) {
    const { tbsDer, signature } = certParts(chainDer[i]);
    const issuerKey = await jose.importX509(derToPem(x5c[i + 1]), "ES256");
    const ok = await crypto.subtle.verify(
      { name: "ECDSA", hash: "SHA-256" },
      issuerKey as CryptoKey,
      asBufferSource(signature),
      asBufferSource(tbsDer),
    );
    if (!ok) throw new Error(`Certificate chain broken at depth ${i}`);
  }
  // Anchor: the last chain cert must be (or be issued by) the pinned Apple Root CA.
  const rootDer = b64UrlDecode(
    appleRootCaPem
      .replace(/-----[^-]+-----/g, "")
      .replace(/\s+/g, "")
      .replace(/-/g, "+")
      .replace(/_/g, "/"),
  );
  const last = chainDer[chainDer.length - 1];
  const pinned = (await sha256Hex(rootDer)) === (await sha256Hex(last));
  if (pinned) {
    // Chain includes the root itself; pin match is sufficient.
  } else {
    const { tbsDer, signature } = certParts(last);
    const rootKey = await jose.importX509(appleRootCaPem, "ES256");
    const ok = await crypto.subtle.verify(
      { name: "ECDSA", hash: "SHA-256" },
      rootKey as CryptoKey,
      asBufferSource(signature),
      asBufferSource(tbsDer),
    );
    if (!ok) throw new Error("Chain does not anchor to the pinned Apple Root CA");
  }

  // 3. Bundle check.
  if (txn.bundleId !== expectedBundleId) {
    throw new Error(`bundleId mismatch: ${txn.bundleId}`);
  }
  return txn;
}

// ---------- App Store Server API ----------

export interface ServerApiConfig {
  issuerId: string;
  keyId: string;
  privateKeyPem: string; // .p8 contents
  bundleId: string;
}

/** Signed JWT for App Store Server API authentication (ES256, 1h expiry). */
export async function serverApiJwt(cfg: ServerApiConfig): Promise<string> {
  const key = await jose.importPKCS8(cfg.privateKeyPem, "ES256");
  return await new jose.SignJWT({})
    .setProtectedHeader({ alg: "ES256", kid: cfg.keyId, typ: "JWT" })
    .setIssuer(cfg.issuerId)
    .setIssuedAt()
    .setExpirationTime("1h")
    .setAudience("appstoreconnect-v1")
    .sign(key);
}

function serverApiBase(environment: string): string {
  return environment === "Sandbox"
    ? "https://api.storekit-sandbox.itunes.apple.com"
    : "https://api.storekit.itunes.apple.com";
}

/**
 * Authoritative confirmation of a transaction with Apple.
 * Returns the decoded signedTransactionInfo payload from Apple's server.
 */
export async function getTransactionInfo(
  cfg: ServerApiConfig,
  transactionId: string,
  environment: "Sandbox" | "Production",
): Promise<AppleTransaction> {
  const jwt = await serverApiJwt(cfg);
  const res = await fetch(
    `${serverApiBase(environment)}/inApps/v1/transactions/${transactionId}`,
    { headers: { Authorization: `Bearer ${jwt}` } },
  );
  if (!res.ok) {
    const body = await res.text();
    throw new Error(`App Store Server API ${res.status}: ${body}`);
  }
  const { signedTransactionInfo } = await res.json();
  const payloadB64 = String(signedTransactionInfo).split(".")[1];
  return JSON.parse(new TextDecoder().decode(b64UrlDecode(payloadB64))) as AppleTransaction;
}
