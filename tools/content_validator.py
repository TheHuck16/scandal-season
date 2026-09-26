#!/usr/bin/env python3
"""
content_validator.py -- validate Scandal Season Content/*.json against JSON schemas.

The game's content pipeline is: Beth/agents author versioned JSON ->
this validator checks it -> Unity editor scripts convert it to assets.
AI coding agents must never need to guess at content shape; the schemas are
the contract.

Schema matching: Content/<stem>.json is validated against
<schema-dir>/<stem>.schema.json. (E.g. Content/outfits.json ->
schemas/outfits.schema.json.)

Usage::

    python3 content_validator.py
    python3 content_validator.py --content-dir ./Content --schema-dir ./schemas
    python3 content_validator.py --strict --report-json ./validation_report.json

Validation engine: uses the `jsonschema` package when installed
(`pip install -r requirements.txt`). Without it, a small built-in structural
checker runs instead (types, required, properties, enum, min/max) -- it is
honest about being a subset, and the report says which engine was used.

Exit codes: 0 = all files valid, 1 = at least one file failed (or a schema
was missing in --strict mode), 2 = bad configuration (missing directories,
unreadable files).
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import sys
from pathlib import Path

LOG = logging.getLogger("content_validator")

TOOLS_DIR = Path(__file__).resolve().parent
PROJECT_DIR = TOOLS_DIR.parent
DEFAULT_CONTENT_DIR = PROJECT_DIR / "Content"
DEFAULT_SCHEMA_DIR = PROJECT_DIR / "Content" / "schemas"

try:
    from jsonschema import Draft202012Validator  # type: ignore[import]
    _HAS_JSONSCHEMA = True
except ImportError:
    _HAS_JSONSCHEMA = False


class ValidationConfigError(Exception):
    """Loud configuration failure."""


# ---------------------------------------------------------------------------
# Fallback structural validator (subset of JSON Schema; used only when the
# `jsonschema` package is not installed).
# ---------------------------------------------------------------------------
_TYPE_MAP = {
    "string": str, "integer": int, "number": (int, float),
    "boolean": bool, "array": list, "object": dict, "null": type(None),
}


def _fallback_check(instance, schema: dict, path: str) -> list[str]:
    errors: list[str] = []
    if not isinstance(schema, dict):
        return errors
    stype = schema.get("type")
    if stype:
        want = _TYPE_MAP.get(stype)
        # bool is a subclass of int: exclude it from integer/number.
        ok = (isinstance(instance, want)
              and not (stype in ("integer", "number")
                       and isinstance(instance, bool)))
        if not ok:
            errors.append(f"{path}: expected {stype}, "
                          f"got {type(instance).__name__}")
            return errors  # deeper checks are meaningless on wrong type
    if "enum" in schema and instance not in schema["enum"]:
        errors.append(f"{path}: {instance!r} not in enum {schema['enum']!r}")
    if isinstance(instance, dict):
        for req in schema.get("required", []):
            if req not in instance:
                errors.append(f"{path}: missing required property {req!r}")
        for key, subschema in schema.get("properties", {}).items():
            if key in instance:
                errors.extend(_fallback_check(instance[key], subschema,
                                              f"{path}.{key}"))
    if isinstance(instance, list):
        if "minItems" in schema and len(instance) < schema["minItems"]:
            errors.append(f"{path}: array has {len(instance)} items, "
                          f"minimum {schema['minItems']}")
        if "maxItems" in schema and len(instance) > schema["maxItems"]:
            errors.append(f"{path}: array has {len(instance)} items, "
                          f"maximum {schema['maxItems']}")
        items = schema.get("items")
        if isinstance(items, dict):
            for i, item in enumerate(instance):
                errors.extend(_fallback_check(item, items, f"{path}[{i}]"))
    if isinstance(instance, (int, float)) and not isinstance(instance, bool):
        if "minimum" in schema and instance < schema["minimum"]:
            errors.append(f"{path}: {instance} below minimum "
                          f"{schema['minimum']}")
        if "maximum" in schema and instance > schema["maximum"]:
            errors.append(f"{path}: {instance} above maximum "
                          f"{schema['maximum']}")
    if isinstance(instance, str):
        if "minLength" in schema and len(instance) < schema["minLength"]:
            errors.append(f"{path}: string shorter than minLength "
                          f"{schema['minLength']}")
    return errors


def validate_with_fallback(instance, schema: dict) -> list[str]:
    return _fallback_check(instance, schema, "$")


def validate_with_jsonschema(instance, schema: dict) -> list[str]:
    validator = Draft202012Validator(schema)
    return [f"${e.json_path}: {e.message}" for e in validator.iter_errors(instance)]


# ---------------------------------------------------------------------------
# File-level validation
# ---------------------------------------------------------------------------
def find_content_files(content_dir: Path) -> list[Path]:
    files = sorted(p for p in content_dir.rglob("*.json")
                   if p.is_file() and ".schema." not in p.name)
    return files


def load_json(path: Path, label: str) -> object:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ValidationConfigError(
            f"{label} {path} is not valid JSON: {exc}."
        ) from exc
    except OSError as exc:
        raise ValidationConfigError(
            f"{label} {path} could not be read: {exc}."
        ) from exc


def validate_file(content_path: Path, schema_dir: Path,
                  strict: bool) -> dict:
    """Validate one content file. Returns a result dict."""
    stem = content_path.stem  # outfits.json -> outfits
    schema_path = schema_dir / f"{stem}.schema.json"
    result: dict = {"file": str(content_path), "schema": None,
                    "status": "ok", "errors": [], "warnings": []}
    try:
        instance = load_json(content_path, "Content file")
    except ValidationConfigError as exc:
        result["status"] = "fail"
        result["errors"] = [str(exc)]
        return result
    if not schema_path.is_file():
        msg = (f"No schema {schema_path.name} in schema dir "
               f"{schema_dir} -- file not validated.")
        if strict:
            result["status"] = "fail"
            result["errors"] = [msg]
        else:
            result["status"] = "skipped"
            result["warnings"] = [msg]
        return result
    result["schema"] = str(schema_path)
    try:
        schema = load_json(schema_path, "Schema")
    except ValidationConfigError as exc:
        result["status"] = "fail"
        result["errors"] = [str(exc)]
        return result
    if not isinstance(schema, dict):
        result["status"] = "fail"
        result["errors"] = [f"Schema {schema_path} must be a JSON object."]
        return result
    if _HAS_JSONSCHEMA:
        errors = validate_with_jsonschema(instance, schema)
        result["engine"] = "jsonschema"
    else:
        errors = validate_with_fallback(instance, schema)
        result["engine"] = "fallback-structural"
        result["warnings"].append(
            "The 'jsonschema' package is not installed -- ran the built-in "
            "structural subset checker instead (types/required/properties/"
            "enum/min/max). Run: pip install -r requirements.txt")
    if errors:
        result["status"] = "fail"
        result["errors"] = errors
    return result


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(
        description="Validate Scandal Season Content/*.json files against "
                    "JSON schemas.")
    p.add_argument("--content-dir", default=str(DEFAULT_CONTENT_DIR),
                   help=f"Content folder (default: {DEFAULT_CONTENT_DIR}).")
    p.add_argument("--schema-dir", default=str(DEFAULT_SCHEMA_DIR),
                   help=f"Schema folder (default: {DEFAULT_SCHEMA_DIR}). "
                        "Each Content/<stem>.json is checked against "
                        "<schema-dir>/<stem>.schema.json.")
    p.add_argument("--strict", action="store_true",
                   help="Treat a missing schema as a failure instead of a skip.")
    p.add_argument("--report-json", default=None,
                   help="Write a structured validation report to this path.")
    p.add_argument("--log-level", default="WARNING",
                   choices=["DEBUG", "INFO", "WARNING", "ERROR"])
    args = p.parse_args(argv)
    logging.basicConfig(level=getattr(logging, args.log_level),
                        format="%(levelname)s %(name)s: %(message)s")

    content_dir = Path(args.content_dir)
    schema_dir = Path(args.schema_dir)
    if not content_dir.is_dir():
        print(f"ERROR: --content-dir not found: {content_dir}. "
              "Create it or pass the right path.", file=sys.stderr)
        return 2
    if not schema_dir.is_dir():
        print(f"ERROR: --schema-dir not found: {schema_dir}. "
              "The schemas folder does not exist yet -- author the JSON "
              "schemas first (see the content pipeline README), then pass "
              "--schema-dir explicitly if they live elsewhere.",
              file=sys.stderr)
        return 2

    files = find_content_files(content_dir)
    if not files:
        print(f"ERROR: no .json content files found under {content_dir}.",
              file=sys.stderr)
        return 2

    results = [validate_file(f, schema_dir, args.strict) for f in files]
    n_ok = sum(1 for r in results if r["status"] == "ok")
    n_skip = sum(1 for r in results if r["status"] == "skipped")
    n_fail = sum(1 for r in results if r["status"] == "fail")
    engine = ("jsonschema" if _HAS_JSONSCHEMA
              else "fallback-structural (install jsonschema for full checks)")

    for r in results:
        name = os.path.basename(r["file"])
        if r["status"] == "ok":
            print(f"OK      {name}")
        elif r["status"] == "skipped":
            print(f"SKIPPED {name}: {r['warnings'][0]}")
        else:
            print(f"FAIL    {name}")
            for e in r["errors"][:10]:
                print(f"          - {e}")
            if len(r["errors"]) > 10:
                print(f"          ... and {len(r['errors']) - 10} more")
    print(f"\nValidated {len(results)} files: {n_ok} ok, {n_fail} failed, "
          f"{n_skip} skipped. Engine: {engine}.")

    if args.report_json:
        report = {"content_dir": str(content_dir),
                  "schema_dir": str(schema_dir),
                  "engine": engine, "strict": args.strict,
                  "summary": {"total": len(results), "ok": n_ok,
                              "failed": n_fail, "skipped": n_skip},
                  "results": results}
        Path(args.report_json).write_text(json.dumps(report, indent=2),
                                          encoding="utf-8")
        print(f"Report written: {args.report_json}")

    return 0 if n_fail == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
