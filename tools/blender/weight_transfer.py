#!/usr/bin/env python3
"""
weight_transfer.py -- Marvelous Designer garment -> CC5 body -> Unity-ready FBX.

Runs INSIDE Blender (bpy). The module still imports cleanly WITHOUT Blender so
it can be inspected, linted, and unit-tested outside Blender; any function that
needs bpy raises PipelineError with a clear message instead.

Pipeline
--------
1. Import the CC5 character FBX (body + armature) and the Marvelous Designer
   garment FBX.
2. Sanity checks: unit/scale convention (expects the "cm / DAZ Studio" scale
   convention from the research: body and garment must share the same order of
   magnitude and the body must be human-scale) and T-pose (arm bones roughly
   horizontal).
3. Remove garment faces hidden inside the body (or only flag them).
4. Decimate the garment to a triangle budget.
5. Transfer skin weights body -> garment via a Data Transfer modifier
   (vertex groups, nearest-face-interpolated), then bind the garment to the
   body armature.
6. Export the garment alone as a Unity-ready FBX.

Usage (Blender background mode -- this is how batch_export.py calls it)::

    blender --background --python weight_transfer.py -- \
        --garment /path/to/gmt_gown_empire.fbx \
        --body    /path/to/chr_rose_hartwell.fbx \
        --output  /path/to/out/gmt_gown_empire.fbx \
        --tri-budget 12000

Exit codes: 0 = ok, 2 = bad input / failed check (message on stderr),
1 = unexpected crash (traceback on stderr).
"""

from __future__ import annotations

import argparse
import json
import logging
import math
import os
import sys

LOG = logging.getLogger("weight_transfer")

# ---------------------------------------------------------------------------
# bpy guard: importable without Blender for inspection / py_compile.
# ---------------------------------------------------------------------------
try:  # pragma: no cover - exercised only inside Blender
    import bpy
    import bmesh
    from mathutils import Vector, BVHTree

    _HAS_BPY = True
except ImportError:  # outside Blender
    bpy = None  # type: ignore[assignment]
    bmesh = None  # type: ignore[assignment]
    Vector = None  # type: ignore[assignment]
    BVHTree = None  # type: ignore[assignment]
    _HAS_BPY = False


class PipelineError(Exception):
    """A loud, user-actionable failure. Message should say what to fix."""


def _require_bpy() -> None:
    if not _HAS_BPY:
        raise PipelineError(
            "weight_transfer.py must be run inside Blender "
            "(blender --background --python weight_transfer.py -- ...). "
            "bpy is not importable in this Python."
        )


# ---------------------------------------------------------------------------
# Pure helpers (work without bpy)
# ---------------------------------------------------------------------------
def validate_inputs(garment_path: str, body_path: str, output_path: str) -> None:
    """Fail loudly on bad CLI input. No bpy needed."""
    for label, path in (("garment", garment_path), ("body", body_path)):
        if not path:
            raise PipelineError(f"--{label} is required.")
        if not os.path.isfile(path):
            raise PipelineError(
                f"--{label} file not found: {path!r}. "
                "Check the path (relative paths resolve from your shell cwd)."
            )
        if not path.lower().endswith(".fbx"):
            raise PipelineError(
                f"--{label} must be an .fbx file, got: {path!r}."
            )
    if not output_path:
        raise PipelineError("--output is required.")
    if not output_path.lower().endswith(".fbx"):
        raise PipelineError(f"--output must end in .fbx, got: {output_path!r}.")
    out_dir = os.path.dirname(os.path.abspath(output_path))
    if not os.path.isdir(out_dir):
        raise PipelineError(
            f"Output directory does not exist: {out_dir!r}. Create it first."
        )
    if os.path.abspath(garment_path) == os.path.abspath(output_path):
        raise PipelineError(
            "--garment and --output must be different files "
            "(never overwrite your source garment)."
        )


def _parse_argv(argv: list[str]) -> argparse.Namespace:
    """Parse args after the '--' separator Blender convention."""
    if "--" in argv:
        argv = argv[argv.index("--") + 1 :]
    p = argparse.ArgumentParser(
        description="Fit a Marvelous Designer FBX garment to a CC5 body and "
        "export a Unity-ready FBX."
    )
    p.add_argument("--garment", required=True, help="Marvelous Designer garment FBX.")
    p.add_argument("--body", required=True, help="CC5 character FBX (mesh + armature).")
    p.add_argument("--output", required=True, help="Destination Unity-ready FBX.")
    p.add_argument("--tri-budget", type=int, default=15000,
                   help="Target triangle count for the garment (default 15000).")
    p.add_argument("--skip-interior-removal", action="store_true",
                   help="Keep faces hidden inside the body (not recommended).")
    p.add_argument("--flag-interior-only", action="store_true",
                   help="Don't delete interior faces; tag them in the "
                        "INTERIOR_FLAG vertex layer for manual review instead.")
    p.add_argument("--no-decimate", action="store_true",
                   help="Skip decimation (garment must already be in budget).")
    p.add_argument("--bake-space-transform", action="store_true",
                   help="Bake world transform into the exported FBX. Try this if "
                        "the garment appears mis-scaled in Unity.")
    p.add_argument("--log-level", default="INFO",
                   choices=["DEBUG", "INFO", "WARNING", "ERROR"])
    return p.parse_args(argv)


# ---------------------------------------------------------------------------
# bpy scene helpers
# ---------------------------------------------------------------------------
def _deselect_all() -> None:
    _require_bpy()
    bpy.ops.object.select_all(action="DESELECT")


def _activate(obj) -> None:
    _require_bpy()
    _deselect_all()
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def _import_fbx(path: str, label: str):
    """Import an FBX; return the imported mesh objects."""
    _require_bpy()
    before = set(bpy.context.scene.objects)
    LOG.info("Importing %s FBX: %s", label, path)
    try:
        bpy.ops.import_scene.fbx(filepath=path)
    except Exception as exc:  # noqa: BLE001 - rewrap with context
        raise PipelineError(
            f"Failed to import {label} FBX {path!r}: {exc}. "
            "Is the file a valid FBX (not e.g. a renamed .obj)?"
        ) from exc
    imported = [o for o in bpy.context.scene.objects if o not in before]
    meshes = [o for o in imported if o.type == "MESH"]
    if not meshes:
        names = ", ".join(sorted(o.name for o in imported)) or "(nothing imported)"
        raise PipelineError(
            f"{label} FBX {path!r} contained no mesh objects "
            f"(imported: {names}). Export a mesh from Marvelous Designer / CC5 "
            "and try again."
        )
    # Heuristic: the body/garment is the largest mesh by face count.
    meshes.sort(key=lambda o: len(o.data.polygons), reverse=True)
    LOG.info("%s mesh: %s (%d faces)", label, meshes[0].name,
             len(meshes[0].data.polygons))
    return meshes[0]


def _world_size(obj) -> "Vector":
    _require_bpy()
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    xs = [c.x for c in corners]
    ys = [c.y for c in corners]
    zs = [c.z for c in corners]
    return Vector((max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)))


def check_scale(body, garment) -> dict:
    """Enforce the cm / DAZ-Studio scale convention from the research.

    Body and garment must share a unit order of magnitude, and the body must
    be human-scale. Returns a dict with the findings for the report.
    """
    _require_bpy()
    bsize = _world_size(body)
    gsize = _world_size(garment)
    bmax, gmax = max(bsize), max(gsize)

    def unit_of(m: float) -> str:
        return "cm" if m > 10.0 else "m"

    bunit, gunit = unit_of(bmax), unit_of(gmax)
    if bunit != gunit:
        raise PipelineError(
            f"Unit mismatch: body measures {bmax:.2f} Blender units "
            f"({bunit}-scale) but garment measures {gmax:.2f} ({gunit}-scale). "
            "Re-export both with the SAME unit convention -- the pipeline "
            'expects "cm / DAZ Studio" scale on the CC5 FBX import in '
            "Marvelous Designer, so both files should arrive in cm."
        )
    height_m = bmax / 100.0 if bunit == "cm" else bmax
    if not 0.8 <= height_m <= 2.5:
        raise PipelineError(
            f"Body is {height_m:.2f} m tall -- not human-scale. "
            "Did you export the full character (not a single limb), and is "
            "the armature scale applied?"
        )
    gheight_m = gmax / 100.0 if gunit == "cm" else gmax
    ratio = gheight_m / height_m
    if not 0.2 <= ratio <= 1.6:
        raise PipelineError(
            f"Garment size looks wrong: longest garment dimension is "
            f"{ratio:.2f}x body height. Expected roughly 0.2x-1.6x. "
            "Check the garment draped on the right avatar in Marvelous "
            "Designer before re-exporting."
        )
    LOG.info("Scale OK: body %.2f m (%s), garment %.2f m",
             height_m, bunit, gheight_m)
    return {"body_unit": bunit, "body_height_m": round(height_m, 3),
            "garment_height_m": round(gheight_m, 3)}


def _find_armature(body):
    _require_bpy()
    for mod in body.modifiers:
        if mod.type == "ARMATURE" and mod.object is not None:
            return mod.object
    for obj in bpy.context.scene.objects:
        if obj.type == "ARMATURE":
            return obj
    return None


def check_tpose(body) -> dict:
    """Heuristic T-pose check on the body's armature.

    Looks for upper-arm bones and requires them to point roughly along +/-X
    (horizontal). Fails loudly if there is no armature or no arm bones --
    a non-T-pose source silently ruins every transferred weight.
    """
    _require_bpy()
    arm = _find_armature(body)
    if arm is None:
        raise PipelineError(
            f"No armature found for body mesh {body.name!r}. "
            "The CC5 export must include the skeleton (check 'Include Armature' "
            "on FBX export)."
        )
    pbones = arm.pose.bones
    arms = [b for b in pbones
            if "upperarm" in b.name.lower()
            or ("upper" in b.name.lower() and "arm" in b.name.lower())]
    if not arms:
        sample = ", ".join(b.name for b in list(pbones)[:12])
        raise PipelineError(
            f"Armature {arm.name!r} has no recognizable upper-arm bones "
            f"(first bones: {sample}...). Cannot verify T-pose -- "
            "rename per CC5 convention or check the export."
        )
    results = {}
    for bone in arms:
        vec = bone.tail - bone.head
        length = vec.length
        if length < 1e-6:
            raise PipelineError(
                f"Arm bone {bone.name!r} is zero-length -- corrupt armature."
            )
        n = vec / length
        name_l = bone.name.lower()
        side = "L" if any(s in name_l for s in ("_l", ".l", "left", " l")) else \
               "R" if any(s in name_l for s in ("_r", ".r", "right", " r")) else "?"
        horizontal = abs(n.x) > 0.80 and abs(n.y) < 0.45 and abs(n.z) < 0.45
        results[bone.name] = {"side": side, "dir": [round(float(c), 3) for c in n],
                              "horizontal": bool(horizontal)}
        if not horizontal:
            raise PipelineError(
                f"Arm bone {bone.name!r} is not horizontal "
                f"(direction {tuple(round(float(c), 2) for c in n)}). "
                "The body does not look T-posed. Re-export the character in "
                "T-pose from CC5 -- weight transfer from an A-pose/posed body "
                "produces garbage deformations."
            )
    # Left and right arms must point opposite ways.
    dirs = [Vector(r["dir"]) for r in results.values()]
    if len(dirs) >= 2 and dirs[0].dot(dirs[1]) > -0.5:
        raise PipelineError(
            "Arm bones do not point in opposite directions -- "
            "skeleton looks mirrored or malformed."
        )
    LOG.info("T-pose OK (%d arm bones horizontal).", len(results))
    return results


def _interior_vertex_indices(garment, body) -> set[int]:
    """Garment verts strictly inside the body mesh (parity raycast in world space)."""
    _require_bpy()
    deps = bpy.context.evaluated_depsgraph_get()
    tree = BVHTree.FromObject(body, deps)  # world-space tree
    mat = garment.matrix_world
    up = Vector((0.0, 0.0, 1.0))
    inside: set[int] = set()
    for v in garment.data.vertices:
        origin = mat @ v.co
        direction = up
        hits = 0
        hit = tree.ray_cast(origin, direction)
        guard = 0
        while hit[0] is not None and guard < 64:
            hits += 1
            origin = hit[0] + direction * 1e-4
            hit = tree.ray_cast(origin, direction)
            guard += 1
        if hits % 2 == 1:
            inside.add(v.index)
    return inside


def remove_interior_faces(garment, body, flag_only: bool = False) -> dict:
    """Delete (or flag) garment faces fully hidden inside the body.

    Returns counts for the report. This is a rough pass -- Beth reviews the
    result; see the anti-clipping playbook in the research notes.
    """
    _require_bpy()
    _activate(garment)
    interior = _interior_vertex_indices(garment, body)
    mesh = garment.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.verts.ensure_lookup_table()
    inset = {bm.verts[i] for i in interior if i < len(bm.verts)}
    removed = 0
    if flag_only:
        layer = (bm.verts.layers.int.get("INTERIOR_FLAG")
                 or bm.verts.layers.int.new("INTERIOR_FLAG"))
        for v in inset:
            v[layer] = 1
        LOG.warning("Flagged %d interior verts (INTERIOR_FLAG) -- NOT deleted.",
                    len(inset))
    else:
        doomed = [f for f in bm.faces if f.verts and all(v in inset for v in f.verts)]
        removed = len(doomed)
        if doomed:
            bmesh.ops.delete(bm, geom=doomed, context="FACES")
        LOG.info("Removed %d interior faces (%d interior verts).",
                 removed, len(inset))
    bm.to_mesh(mesh)
    mesh.update()
    bm.free()
    return {"interior_verts": len(inset), "faces_removed": removed,
            "flag_only": flag_only}


def mesh_tri_count(obj) -> int:
    _require_bpy()
    deps = bpy.context.evaluated_depsgraph_get()
    eval_obj = obj.evaluated_get(deps)
    mesh = eval_obj.to_mesh()
    mesh.calc_loop_triangles()
    n = len(mesh.loop_triangles)
    eval_obj.to_mesh_clear()
    return n


def decimate_to_budget(garment, tri_budget: int) -> dict:
    """Collapse-decimate the garment until under tri_budget (max 4 passes)."""
    _require_bpy()
    if tri_budget <= 0:
        raise PipelineError(f"--tri-budget must be positive, got {tri_budget}.")
    _activate(garment)
    before = mesh_tri_count(garment)
    after = before
    passes = 0
    while after > tri_budget and passes < 4:
        ratio = max(0.05, (tri_budget / after) * 0.95)
        mod = garment.modifiers.new(name="SS_Decimate", type="DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = ratio
        mod.use_collapse_triangulate = True
        try:
            bpy.ops.object.modifier_apply(modifier=mod.name)
        except RuntimeError as exc:
            raise PipelineError(
                f"Decimate modifier failed on {garment.name!r}: {exc}."
            ) from exc
        after = mesh_tri_count(garment)
        passes += 1
        LOG.info("Decimate pass %d: ratio %.3f -> %d tris", passes, ratio, after)
    if after > tri_budget:
        LOG.warning("Still %d tris after %d passes (budget %d) -- continuing "
                    "anyway; review manually.", after, passes, tri_budget)
    return {"tris_before": before, "tris_after": after, "passes": passes,
            "in_budget": after <= tri_budget}


def transfer_weights(garment, body) -> dict:
    """Transfer skin weights body -> garment (Data Transfer, vertex groups)."""
    _require_bpy()
    _activate(garment)
    mod = garment.modifiers.new(name="SS_WeightTransfer", type="DATA_TRANSFER")
    mod.object = body
    mod.use_vert_data = True
    mod.data_types_verts = {"VGROUP_WEIGHTS"}
    mod.vert_mapping = "POLYINTERP_NEAREST"
    mod.layers_vgroup_select_src = "ALL"
    mod.layers_vgroup_select_dst = "ALL"
    mod.mix_mode = "REPLACE"
    mod.mix_factor = 1.0
    try:
        bpy.ops.object.modifier_apply(modifier=mod.name)
    except RuntimeError as exc:
        raise PipelineError(
            f"Weight transfer failed on {garment.name!r}: {exc}. "
            "Do the body and garment overlap in space (same scale/pose)?"
        ) from exc
    groups = [g.name for g in garment.vertex_groups]
    if not groups:
        raise PipelineError(
            f"No vertex groups landed on {garment.name!r} -- the Data Transfer "
            "found no source weights. Is the body mesh actually skinned "
            "(does IT have vertex groups)?"
        )
    # Warn on unweighted verts (they would stay frozen in-engine).
    zero = 0
    for v in garment.data.vertices:
        if not any(g.weight > 1e-6 for g in garment.data.vertices[v.index].groups):
            zero += 1
    if zero:
        LOG.warning("%d/%d garment verts have zero weight -- they will not "
                    "deform. Clean up shoulders/armpits manually.",
                    zero, len(garment.data.vertices))
    LOG.info("Weight transfer OK: %d vertex groups.", len(groups))
    return {"vertex_groups": len(groups), "zero_weight_verts": zero}


def bind_to_armature(garment, body) -> str:
    """Add an Armature modifier so the garment follows the body skeleton."""
    _require_bpy()
    arm = _find_armature(body)
    _activate(garment)
    existing = next((m for m in garment.modifiers if m.type == "ARMATURE"), None)
    if existing is not None:
        existing.object = arm
        return existing.name
    mod = garment.modifiers.new(name="SS_Armature", type="ARMATURE")
    mod.object = arm
    LOG.info("Bound %s to armature %s.", garment.name, arm.name)
    return mod.name


def export_unity_fbx(garment, output_path: str, bake_space_transform: bool) -> None:
    _require_bpy()
    _activate(garment)
    try:
        bpy.ops.export_scene.fbx(
            filepath=output_path,
            use_selection=True,
            object_types={"MESH"},
            use_mesh_modifiers=True,
            mesh_smooth_type="FACE",
            add_leaf_bones=False,
            bake_anim=False,
            apply_unit_scale=True,
            bake_space_transform=bake_space_transform,
            axis_forward="-Z",
            axis_up="Y",
        )
    except Exception as exc:  # noqa: BLE001 - rewrap with context
        raise PipelineError(
            f"FBX export failed to {output_path!r}: {exc}."
        ) from exc
    if not os.path.isfile(output_path):
        raise PipelineError(
            f"FBX exporter reported success but {output_path!r} was not "
            "created. Disk full / permissions?"
        )
    LOG.info("Exported Unity-ready FBX: %s", output_path)


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------
def run_pipeline(args: argparse.Namespace) -> dict:
    """Full pipeline. Requires bpy. Returns a JSON-serializable summary."""
    _require_bpy()
    validate_inputs(args.garment, args.body, args.output)

    body = _import_fbx(args.body, "body")
    garment = _import_fbx(args.garment, "garment")

    report: dict = {
        "garment": os.path.basename(args.garment),
        "body": os.path.basename(args.body),
        "output": args.output,
        "tri_budget": args.tri_budget,
    }
    report["scale"] = check_scale(body, garment)
    report["tpose"] = check_tpose(body)

    if args.skip_interior_removal:
        LOG.warning("Skipping interior-face removal per --skip-interior-removal.")
        report["interior"] = {"skipped": True}
    else:
        report["interior"] = remove_interior_faces(
            garment, body, flag_only=args.flag_interior_only
        )

    if args.no_decimate:
        tris = mesh_tri_count(garment)
        LOG.info("Skipping decimation: %d tris.", tris)
        if tris > args.tri_budget:
            raise PipelineError(
                f"Garment has {tris} tris, over budget {args.tri_budget}, and "
                "--no-decimate was given. Raise --tri-budget or drop the flag."
            )
        report["decimate"] = {"skipped": True, "tris": tris}
    else:
        report["decimate"] = decimate_to_budget(garment, args.tri_budget)

    report["weights"] = transfer_weights(garment, body)
    report["armature_modifier"] = bind_to_armature(garment, body)

    export_unity_fbx(garment, args.output, args.bake_space_transform)
    report["status"] = "ok"
    return report


def main(argv: list[str] | None = None) -> int:
    args = _parse_argv(argv if argv is not None else sys.argv)
    logging.basicConfig(level=getattr(logging, args.log_level),
                        format="%(levelname)s %(name)s: %(message)s")
    try:
        summary = run_pipeline(args)
    except PipelineError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2
    except Exception:  # noqa: BLE001 - unexpected; full traceback wanted
        LOG.exception("Unexpected crash")
        return 1
    print("RESULT_JSON:" + json.dumps(summary))
    LOG.info("Done: %s", args.output)
    return 0


if __name__ == "__main__":
    sys.exit(main())
