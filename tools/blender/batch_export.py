#!/usr/bin/env python3
"""
batch_export.py -- batch wrapper around weight_transfer.py.

Processes many Marvelous Designer garment FBX files through Blender in
background mode, then writes a per-file report JSON.

Two ways to select garments:

1. A JSON config file (--config). Example (see batch_config.example.json)::

    {
      "body_fbx": "../characters/chr_rose_hartwell.fbx",
      "output_dir": "../unity_ready",
      "default_tri_budget": 15000,
      "blender": "blender",
      "garments": [
        {"file": "gmt_gown_empire_red.fbx", "tri_budget": 12000},
        {"file": "gmt_spencer_navy.fbx"}
      ]
    }

   Relative paths resolve against the config file's directory.

2. A folder scan: --input-dir DIR --body BODY.fbx --output-dir DIR
   [--tri-budget N]. Every *.fbx in DIR is processed with the same budget.

Usage::

    python3 batch_export.py --config batch_config.example.json
    python3 batch_export.py --input-dir ./md_exports --body ./chr_body.fbx \\
        --output-dir ./unity_ready --tri-budget 12000 --report ./report.json

Exit codes: 0 = every garment ok, 1 = at least one failed, 2 = bad config /
Blender not found (nothing was attempted).
"""

from __future__ import annotations

import argparse
import json
import logging
import os
import shutil
import subprocess
import sys
from pathlib import Path

LOG = logging.getLogger("batch_export")

WEIGHT_TRANSFER = Path(__file__).resolve().parent / "weight_transfer.py"
RESULT_PREFIX = "RESULT_JSON:"


class ConfigError(Exception):
    """Loud config failure -- nothing was attempted."""


def load_config(path: str) -> dict:
    cfg_path = Path(path)
    if not cfg_path.is_file():
        raise ConfigError(f"Config file not found: {path!r}.")
    try:
        cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise ConfigError(f"Config {path!r} is not valid JSON: {exc}.") from exc
    if not isinstance(cfg, dict):
        raise ConfigError(f"Config {path!r} must be a JSON object.")
    base = cfg_path.resolve().parent

    def resolve(p: str | None, label: str, required: bool = True) -> str | None:
        if p is None:
            if required:
                raise ConfigError(
                    f"Config {path!r} is missing required key {label!r}."
                )
            return None
        rp = (base / p).resolve() if not os.path.isabs(p) else Path(p)
        return str(rp)

    body = resolve(cfg.get("body_fbx"), "body_fbx")
    out_dir = resolve(cfg.get("output_dir"), "output_dir")
    if not os.path.isfile(body or ""):
        raise ConfigError(f"body_fbx not found: {cfg.get('body_fbx')!r} "
                          f"(resolved: {body}).")
    garments = cfg.get("garments")
    if not garments or not isinstance(garments, list):
        raise ConfigError(
            f"Config {path!r} needs a non-empty 'garments' list.")
    items = []
    for i, g in enumerate(garments):
        if not isinstance(g, dict) or "file" not in g:
            raise ConfigError(
                f"Config {path!r}: garments[{i}] must be an object with a "
                "'file' key.")
        gfile = resolve(g["file"], f"garments[{i}].file")
        if not os.path.isfile(gfile or ""):
            raise ConfigError(f"Garment file not found: {g['file']!r} "
                              f"(resolved: {gfile}).")
        budget = g.get("tri_budget", cfg.get("default_tri_budget", 15000))
        if not isinstance(budget, int) or budget <= 0:
            raise ConfigError(
                f"Config {path!r}: garments[{i}] tri_budget must be a positive "
                f"integer, got {budget!r}.")
        stem = Path(gfile).stem  # e.g. gmt_gown_empire_red
        out_name = g.get("output", stem + ".fbx")
        items.append({
            "garment": gfile,
            "tri_budget": budget,
            "output": str(Path(out_dir) / out_name),
            "label": g["file"],
        })
    return {
        "body": body,
        "output_dir": out_dir,
        "blender": cfg.get("blender", "blender"),
        "items": items,
    }


def discover_input_dir(input_dir: str, body: str, output_dir: str,
                       tri_budget: int) -> dict:
    if not os.path.isdir(input_dir):
        raise ConfigError(f"--input-dir not found: {input_dir!r}.")
    if not os.path.isfile(body):
        raise ConfigError(f"--body not found: {body!r}.")
    files = sorted(Path(input_dir).glob("*.fbx"))
    if not files:
        raise ConfigError(f"No .fbx files in --input-dir {input_dir!r}.")
    return {
        "body": os.path.abspath(body),
        "output_dir": os.path.abspath(output_dir),
        "blender": "blender",
        "items": [{
            "garment": str(f.resolve()),
            "tri_budget": tri_budget,
            "output": str(Path(output_dir).resolve() / (f.stem + ".fbx")),
            "label": f.name,
        } for f in files],
    }


def run_one(blender_bin: str, item: dict, body: str, timeout: int) -> dict:
    """Run weight_transfer.py in Blender background mode for one garment."""
    os.makedirs(os.path.dirname(item["output"]), exist_ok=True)
    cmd = [
        blender_bin, "--background",
        "--python", str(WEIGHT_TRANSFER), "--",
        "--garment", item["garment"],
        "--body", body,
        "--output", item["output"],
        "--tri-budget", str(item["tri_budget"]),
    ]
    LOG.info("Processing %s (budget %d tris)...",
             item["label"], item["tri_budget"])
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True,
                              timeout=timeout)
    except subprocess.TimeoutExpired:
        return {"garment": item["label"], "status": "failed",
                "error": f"Timed out after {timeout}s -- garment may be too "
                         "dense; try a lower tri budget or simplify in MD.",
                "output": item["output"]}
    result_json = None
    for line in (proc.stdout or "").splitlines():
        if line.startswith(RESULT_PREFIX):
            try:
                result_json = json.loads(line[len(RESULT_PREFIX):])
            except json.JSONDecodeError:
                pass
    if proc.returncode == 0 and os.path.isfile(item["output"]):
        return {"garment": item["label"], "status": "ok",
                "output": item["output"],
                "tri_budget": item["tri_budget"],
                "detail": result_json}
    tail = "\n".join(((proc.stderr or "") + "\n" + (proc.stdout or ""))
                     .splitlines()[-25:])
    return {"garment": item["label"], "status": "failed",
            "error": f"Blender exited with code {proc.returncode}.",
            "log_tail": tail, "output": item["output"]}


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(
        description="Batch-fit Marvelous Designer garments to a CC5 body via "
                    "Blender background mode.")
    src = p.add_mutually_exclusive_group(required=True)
    src.add_argument("--config", help="JSON config file (see batch_config.example.json).")
    src.add_argument("--input-dir", help="Folder of .fbx garments to process.")
    p.add_argument("--body", help="CC5 body FBX (required with --input-dir).")
    p.add_argument("--output-dir", help="Destination folder (required with --input-dir).")
    p.add_argument("--tri-budget", type=int, default=15000,
                   help="Triangle budget per garment with --input-dir.")
    p.add_argument("--blender", default=None,
                   help="Blender binary (overrides config).")
    p.add_argument("--timeout", type=int, default=1800,
                   help="Seconds per garment before giving up (default 1800).")
    p.add_argument("--report", default=None,
                   help="Where to write the report JSON "
                        "(default: <output_dir>/batch_report.json).")
    p.add_argument("--log-level", default="INFO",
                   choices=["DEBUG", "INFO", "WARNING", "ERROR"])
    args = p.parse_args(argv)
    logging.basicConfig(level=getattr(logging, args.log_level),
                        format="%(levelname)s %(name)s: %(message)s")

    try:
        if args.config:
            job = load_config(args.config)
        else:
            if not args.body or not args.output_dir:
                raise ConfigError(
                    "--input-dir mode requires --body and --output-dir.")
            job = discover_input_dir(args.input_dir, args.body,
                                     args.output_dir, args.tri_budget)
    except ConfigError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    blender_bin = args.blender or job["blender"]
    if shutil.which(blender_bin) is None:
        print(f"ERROR: Blender binary not found: {blender_bin!r}. "
              "Install Blender and ensure it is on PATH, or pass --blender "
              "/path/to/blender.", file=sys.stderr)
        return 2
    if not WEIGHT_TRANSFER.is_file():
        print(f"ERROR: weight_transfer.py not found next to this script "
              f"(expected {WEIGHT_TRANSFER}).", file=sys.stderr)
        return 2

    os.makedirs(job["output_dir"], exist_ok=True)
    results = [run_one(blender_bin, item, job["body"], args.timeout)
               for item in job["items"]]
    ok = sum(1 for r in results if r["status"] == "ok")
    failed = len(results) - ok
    report = {
        "body": job["body"],
        "output_dir": job["output_dir"],
        "summary": {"total": len(results), "ok": ok, "failed": failed},
        "results": results,
    }
    report_path = args.report or str(Path(job["output_dir"]) / "batch_report.json")
    with open(report_path, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2)
    print(f"Batch complete: {ok} ok, {failed} failed "
          f"({len(results)} total). Report: {report_path}")
    for r in results:
        if r["status"] != "ok":
            print(f"  FAILED {r['garment']}: {r.get('error')}", file=sys.stderr)
    return 0 if failed == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
