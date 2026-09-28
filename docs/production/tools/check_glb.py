#!/usr/bin/env python3
"""
check_glb.py -- stdlib-only, read-only staging check for Meshy GLB outputs (AI-054).

Checks a binary glTF 2.0 (.glb) file against the AI-050 staging contract in
docs/production/ASSET_QUEUE.md and the AI-049 manifest technical constraints
(docs/production/batches/AI-049-skyline-seer/manifest-entry.json):
  1. Container: 12-byte header (magic "glTF", version 2, declared length equal
     to the file size), JSON chunk first, optional BIN chunk second, every
     chunk in bounds and 4-byte aligned, JSON is strict (no NaN/Infinity).
  2. Geometry: every drawn POSITION accessor is VEC3 FLOAT, in bounds of its
     bufferView/buffer/BIN chunk, finite, and inside its declared min/max;
     indices are in range. World-space bounds apply node transforms
     (matrix or TRS) through the default scene's node tree.
  3. Filename: the file stem is a snake_case card_id; if given, it equals
     --card-id; if a manifest is available, the id exists in it.
  4. Scale: world Y extent is --expected-height (default 1.8) +/- tolerance
     (default 10%).
  5. Origin: world min Y (the lowest point, i.e. the feet) is within
     --feet-tolerance (default 0.05 units) of y = 0.
  6. Materials (opt-in, --require-materials): every rendered primitive
     references a material. Out-of-range material indices are malformed and
     rejected with or without this flag.
It reports bounds, topology (meshes, primitives, triangles, vertices,
degenerate triangles) and material names.

What this does NOT prove: glTF defines +Y as up, so the Y extent is measured as
height, but the checker cannot tell whether the model is semantically upright
or facing +Z. It also cannot judge material separation, style, silhouette or
likeness. Those need a visual review and a Unity import.

Malformed, non-finite or unsupported input (Draco/meshopt/quantized positions,
sparse accessors, external or data-URI buffers, point/line primitives,
non-tree node graphs) is rejected with a message; the file is never modified.

Exit codes: 0 = all checks pass; 1 = one or more checks fail;
            2 = file rejected (unreadable, malformed or unsupported) or usage error.
Run:  python check_glb.py path/to/card_id.glb [--card-id ID] [--json]
"""

import argparse
import csv
import json
import math
import os
import re
import struct
import sys
from array import array

GLB_MAGIC = b"glTF"
CHUNK_JSON = 0x4E4F534A
CHUNK_BIN = 0x004E4942
MAX_FILE_BYTES = 1 << 30  # refuse anything over 1 GiB rather than read it

COMPONENT_TYPES = {  # componentType -> (array typecode, byte size)
    5120: ("b", 1), 5121: ("B", 1), 5122: ("h", 2),
    5123: ("H", 2), 5125: ("I", 4), 5126: ("f", 4),
}
TYPE_WIDTHS = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4,
               "MAT2": 4, "MAT3": 9, "MAT4": 16}
INDEX_COMPONENTS = (5121, 5123, 5125)
TRIANGLE_MODES = {4: "TRIANGLES", 5: "TRIANGLE_STRIP", 6: "TRIANGLE_FAN"}
UNSUPPORTED_MODES = {0: "POINTS", 1: "LINES", 2: "LINE_LOOP", 3: "LINE_STRIP"}
# Required extensions that change how positions are stored; we cannot decode them.
GEOMETRY_EXTENSIONS = ("KHR_draco_mesh_compression", "EXT_meshopt_compression",
                       "KHR_mesh_quantization")

CARD_ID_RE = re.compile(r"^[a-z0-9]+(?:_[a-z0-9]+)+$")
DEFAULT_MANIFEST = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..", "..", "muse", "sprint-01", "manifest.csv"))

NOT_PROVEN = [
    "semantic up/forward orientation (+Y up, +Z forward): glTF is +Y-up by "
    "definition, so Y extent is measured as height, but an upright pose and "
    "forward facing need visual review",
    "material separation, style, silhouette and likeness compliance",
    "Unity import result and in-game scale/performance",
]

IDENTITY = ((1.0, 0.0, 0.0, 0.0), (0.0, 1.0, 0.0, 0.0),
            (0.0, 0.0, 1.0, 0.0), (0.0, 0.0, 0.0, 1.0))


class GlbError(Exception):
    """File is malformed or uses a feature this checker cannot safely read."""


# ---------------------------------------------------------------- helpers

def _is_int(v):
    return isinstance(v, int) and not isinstance(v, bool)


def _is_num(v):
    return isinstance(v, (int, float)) and not isinstance(v, bool)


def _item(gltf, key, idx, what):
    items = gltf.get(key)
    if not isinstance(items, list):
        raise GlbError(f"{what} references {key}[{idx}] but '{key}' is missing")
    if not _is_int(idx) or not 0 <= idx < len(items):
        raise GlbError(f"{what} references invalid {key} index {idx!r}")
    if not isinstance(items[idx], dict):
        raise GlbError(f"{key}[{idx}] is not an object")
    return items[idx]


def _finite_list(value, length, what):
    if (not isinstance(value, list) or len(value) != length
            or not all(_is_num(v) for v in value)):
        raise GlbError(f"{what} must be a list of {length} numbers")
    if not all(math.isfinite(v) for v in value):
        raise GlbError(f"{what} contains non-finite values")
    return [float(v) for v in value]


def _reject_constant(name):
    raise GlbError(f"JSON chunk contains non-standard constant {name}")


def _matmul(a, b):
    return tuple(tuple(sum(a[r][k] * b[k][c] for k in range(4)) for c in range(4))
                 for r in range(4))


def _node_matrix(node, idx, warnings):
    has_trs = any(k in node for k in ("translation", "rotation", "scale"))
    if "matrix" in node:
        if has_trs:
            raise GlbError(f"nodes[{idx}] has both matrix and TRS properties")
        m = _finite_list(node["matrix"], 16, f"nodes[{idx}].matrix")
        # glTF matrices are column-major.
        return tuple(tuple(m[c * 4 + r] for c in range(4)) for r in range(4))
    t = _finite_list(node.get("translation", [0, 0, 0]), 3, f"nodes[{idx}].translation")
    q = _finite_list(node.get("rotation", [0, 0, 0, 1]), 4, f"nodes[{idx}].rotation")
    s = _finite_list(node.get("scale", [1, 1, 1]), 3, f"nodes[{idx}].scale")
    norm = math.sqrt(sum(v * v for v in q))
    if norm == 0.0:
        raise GlbError(f"nodes[{idx}].rotation is a zero quaternion")
    if abs(norm - 1.0) > 1e-3:
        warnings.append(f"nodes[{idx}].rotation is not unit length ({norm:.6f}); normalized")
    x, y, z, w = (v / norm for v in q)
    rot = ((1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)),
           (2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)),
           (2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)))
    return tuple(tuple(rot[r][c] * s[c] for c in range(3)) + (t[r],) for r in range(3)) \
        + ((0.0, 0.0, 0.0, 1.0),)


# ---------------------------------------------------------------- container

def read_glb(path):
    """Return (gltf_json, bin_chunk_bytes_or_None, container_info)."""
    size = os.path.getsize(path)
    if size > MAX_FILE_BYTES:
        raise GlbError(f"file is {size} bytes; refusing files over {MAX_FILE_BYTES}")
    with open(path, "rb") as fh:
        data = fh.read()
    if len(data) < 12:
        raise GlbError(f"file is {len(data)} bytes; a GLB header needs 12")
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    if magic != GLB_MAGIC:
        raise GlbError(f"bad magic {magic!r}; expected b'glTF'")
    if version != 2:
        raise GlbError(f"GLB container version {version}; only version 2 is supported")
    if length != len(data):
        raise GlbError(f"header declares {length} bytes but file has {len(data)}")

    chunks = []
    offset = 12
    while offset < length:
        if offset + 8 > length:
            raise GlbError(f"truncated chunk header at byte {offset}")
        clen, ctype = struct.unpack_from("<II", data, offset)
        start = offset + 8
        if start + clen > length:
            raise GlbError(f"chunk at byte {offset} declares {clen} bytes, past end of file")
        if clen % 4:
            raise GlbError(f"chunk at byte {offset} length {clen} is not 4-byte aligned")
        chunks.append((ctype, start, clen))
        offset = start + clen

    if not chunks or chunks[0][0] != CHUNK_JSON:
        raise GlbError("first chunk is not a JSON chunk")
    _, jstart, jlen = chunks[0]
    try:
        text = data[jstart:jstart + jlen].rstrip(b" \x00").decode("utf-8")
        gltf = json.loads(text, parse_constant=_reject_constant)
    except (UnicodeDecodeError, ValueError) as exc:
        raise GlbError(f"JSON chunk does not parse: {exc}") from None
    if not isinstance(gltf, dict):
        raise GlbError("JSON chunk is not an object")
    asset = gltf.get("asset")
    if not isinstance(asset, dict) or not str(asset.get("version", "")).startswith("2."):
        raise GlbError("asset.version is missing or not 2.x")

    bin_chunk = None
    for i, (ctype, start, clen) in enumerate(chunks[1:], start=1):
        if ctype == CHUNK_BIN:
            if i != 1:
                raise GlbError("BIN chunk must immediately follow the JSON chunk")
            bin_chunk = memoryview(data)[start:start + clen]
        elif ctype == CHUNK_JSON:
            raise GlbError("more than one JSON chunk")
        # Unknown chunk types are ignored per the glTF 2.0 spec.

    info = {"bytes": length, "container_version": version,
            "generator": asset.get("generator"),
            "json_bytes": jlen, "bin_bytes": len(bin_chunk) if bin_chunk is not None else 0}
    return gltf, bin_chunk, info


def read_accessor(gltf, bin_chunk, idx, allowed_types, allowed_components, what):
    """Decode an accessor into a flat array; raise GlbError on anything unsafe."""
    acc = _item(gltf, "accessors", idx, what)
    if "sparse" in acc:
        raise GlbError(f"accessors[{idx}] ({what}) is sparse; unsupported")
    if "bufferView" not in acc:
        raise GlbError(f"accessors[{idx}] ({what}) has no bufferView "
                       "(zero-filled or extension-decoded data); unsupported")
    atype, comp, count = acc.get("type"), acc.get("componentType"), acc.get("count")
    if atype not in allowed_types:
        raise GlbError(f"accessors[{idx}] ({what}) type {atype!r}; expected {allowed_types}")
    if comp not in allowed_components:
        raise GlbError(f"accessors[{idx}] ({what}) componentType {comp!r} unsupported "
                       f"(allowed {allowed_components})")
    if acc.get("normalized"):
        raise GlbError(f"accessors[{idx}] ({what}) is normalized; unsupported")
    if not _is_int(count) or count < 1:
        raise GlbError(f"accessors[{idx}] ({what}) count {count!r} is invalid")

    bv_idx = acc["bufferView"]
    bv = _item(gltf, "bufferViews", bv_idx, f"accessors[{idx}]")
    buf_idx = bv.get("buffer")
    buf = _item(gltf, "buffers", buf_idx, f"bufferViews[{bv_idx}]")
    if "uri" in buf or buf_idx != 0:
        raise GlbError(f"buffers[{buf_idx}] is external or a data URI; only the "
                       "GLB-embedded buffer is read")
    if bin_chunk is None:
        raise GlbError("geometry references buffers[0] but the GLB has no BIN chunk")
    buf_len = buf.get("byteLength")
    if not _is_int(buf_len) or buf_len < 0 or buf_len > len(bin_chunk):
        raise GlbError(f"buffers[0].byteLength {buf_len!r} exceeds BIN chunk ({len(bin_chunk)})")

    bv_off, bv_len = bv.get("byteOffset", 0), bv.get("byteLength")
    if not (_is_int(bv_off) and _is_int(bv_len)) or bv_off < 0 or bv_len < 0 \
            or bv_off + bv_len > buf_len:
        raise GlbError(f"bufferViews[{bv_idx}] range is outside buffers[0]")

    code, csize = COMPONENT_TYPES[comp]
    width = TYPE_WIDTHS[atype]
    elem = csize * width
    stride = bv.get("byteStride", elem)
    if not _is_int(stride) or stride < elem:
        raise GlbError(f"bufferViews[{bv_idx}].byteStride {stride!r} smaller than element ({elem})")
    acc_off = acc.get("byteOffset", 0)
    if not _is_int(acc_off) or acc_off < 0:
        raise GlbError(f"accessors[{idx}].byteOffset {acc_off!r} is invalid")
    if acc_off + stride * (count - 1) + elem > bv_len:
        raise GlbError(f"accessors[{idx}] ({what}) reads past the end of bufferViews[{bv_idx}]")
    start = bv_off + acc_off
    if start % csize:
        raise GlbError(f"accessors[{idx}] ({what}) data is not aligned to its component size")

    out = array(code)
    if out.itemsize != csize:
        raise GlbError(f"platform array('{code}') itemsize {out.itemsize} != {csize}")
    if stride == elem:
        out.frombytes(bin_chunk[start:start + count * elem])
    else:
        for i in range(count):
            p = start + i * stride
            out.frombytes(bin_chunk[p:p + elem])
    if sys.byteorder != "little":
        out.byteswap()
    return acc, out


# ---------------------------------------------------------------- geometry

def _drawn_mesh_instances(gltf, warnings):
    """Yield (mesh_index, world_matrix) for every mesh reachable from the scene."""
    nodes = gltf.get("nodes", [])
    if not isinstance(nodes, list):
        raise GlbError("'nodes' is not a list")
    scenes = gltf.get("scenes")
    if scenes:
        scene_idx = gltf.get("scene", 0)
        scene = _item(gltf, "scenes", scene_idx, "scene")
        roots = scene.get("nodes", [])
        if not isinstance(roots, list):
            raise GlbError(f"scenes[{scene_idx}].nodes is not a list")
    else:
        warnings.append("no scenes defined; using every parentless node as a root")
        children = set()
        for n in nodes:
            if isinstance(n, dict) and isinstance(n.get("children"), list):
                children.update(c for c in n["children"] if _is_int(c))
        roots = [i for i in range(len(nodes)) if i not in children]

    visited = set()
    stack = [(r, IDENTITY) for r in reversed(roots)]
    while stack:
        n_idx, parent = stack.pop()
        node = _item(gltf, "nodes", n_idx, "node graph")
        if n_idx in visited:
            raise GlbError(f"nodes[{n_idx}] is reached twice; node graph is not a tree")
        visited.add(n_idx)
        world = _matmul(parent, _node_matrix(node, n_idx, warnings))
        if not all(math.isfinite(v) for row in world for v in row):
            raise GlbError(f"nodes[{n_idx}] world transform is non-finite")
        if "skin" in node:
            warnings.append(f"nodes[{n_idx}] is skinned; bounds use the node "
                            "transform, not the skeleton pose")
        if "mesh" in node:
            yield node["mesh"], world
        kids = node.get("children", [])
        if not isinstance(kids, list):
            raise GlbError(f"nodes[{n_idx}].children is not a list")
        for c in reversed(kids):
            stack.append((c, world))


def _bounds_of(pos, world):
    """World-space (min, max) of flat xyz positions under an affine matrix."""
    linear_diag = all(world[r][c] == 0.0 for r in range(3) for c in range(3) if r != c)
    if linear_diag:
        lo, hi = [], []
        for axis in range(3):
            col = pos[axis::3]
            a = min(col) * world[axis][axis] + world[axis][3]
            b = max(col) * world[axis][axis] + world[axis][3]
            lo.append(min(a, b))
            hi.append(max(a, b))
        return lo, hi
    lo = [math.inf] * 3
    hi = [-math.inf] * 3
    m = world
    for x, y, z in zip(pos[0::3], pos[1::3], pos[2::3]):
        for r in range(3):
            v = m[r][0] * x + m[r][1] * y + m[r][2] * z + m[r][3]
            if v < lo[r]:
                lo[r] = v
            if v > hi[r]:
                hi[r] = v
    return lo, hi


def analyze(gltf, bin_chunk):
    warnings = []
    required = gltf.get("extensionsRequired", [])
    if not isinstance(required, list):
        raise GlbError("extensionsRequired is not a list")
    blocked = [e for e in required if e in GEOMETRY_EXTENSIONS]
    if blocked:
        raise GlbError(f"required geometry extension(s) unsupported: {', '.join(blocked)}")
    for ext in required:
        warnings.append(f"extension required but not interpreted: {ext}")

    materials = gltf.get("materials", [])
    if not isinstance(materials, list):
        raise GlbError("'materials' is not a list")
    material_names = []
    for i, mat in enumerate(materials):
        name = mat.get("name") if isinstance(mat, dict) else None
        material_names.append(name if isinstance(name, str) and name else f"<unnamed #{i}>")

    positions_cache, index_cache = {}, {}
    lo = [math.inf] * 3
    hi = [-math.inf] * 3
    topo = {"mesh_instances": 0, "primitives": 0, "triangles": 0, "vertices": 0,
            "degenerate_triangles": 0, "modes": {}, "primitives_without_material": 0,
            "primitives_missing_material": [],
            "materials_used": set()}
    meshes_seen = set()

    for mesh_idx, world in _drawn_mesh_instances(gltf, warnings):
        mesh = _item(gltf, "meshes", mesh_idx, "node")
        prims = mesh.get("primitives")
        if not isinstance(prims, list) or not prims:
            raise GlbError(f"meshes[{mesh_idx}] has no primitives")
        meshes_seen.add(mesh_idx)
        topo["mesh_instances"] += 1
        for p_i, prim in enumerate(prims):
            where = f"meshes[{mesh_idx}].primitives[{p_i}]"
            if not isinstance(prim, dict):
                raise GlbError(f"{where} is not an object")
            mode = prim.get("mode", 4)
            if mode in UNSUPPORTED_MODES:
                raise GlbError(f"{where} mode {UNSUPPORTED_MODES[mode]} is unsupported geometry")
            if mode not in TRIANGLE_MODES:
                raise GlbError(f"{where} mode {mode!r} is invalid")
            attrs = prim.get("attributes")
            if not isinstance(attrs, dict) or "POSITION" not in attrs:
                raise GlbError(f"{where} has no POSITION attribute")
            if prim.get("targets"):
                warnings.append(f"{where} has morph targets; bounds use base positions only")

            p_idx = attrs["POSITION"]
            if not _is_int(p_idx) or p_idx not in positions_cache:
                acc, pos = read_accessor(gltf, bin_chunk, p_idx, ("VEC3",), (5126,),
                                         f"{where} POSITION")
                if not all(map(math.isfinite, pos)):
                    raise GlbError(f"accessors[{p_idx}] POSITION contains non-finite values")
                _check_declared_bounds(acc, p_idx, pos, warnings)
                positions_cache[p_idx] = pos
            pos = positions_cache[p_idx]
            vcount = len(pos) // 3

            if "indices" in prim:
                i_idx = prim["indices"]
                if not _is_int(i_idx) or i_idx not in index_cache:
                    _, idx = read_accessor(gltf, bin_chunk, i_idx, ("SCALAR",),
                                           INDEX_COMPONENTS, f"{where} indices")
                    index_cache[i_idx] = idx
                idx = index_cache[i_idx]
                if max(idx) >= vcount:
                    raise GlbError(f"{where} index {max(idx)} out of range for "
                                   f"{vcount} vertices")
                n = len(idx)
            else:
                idx, n = None, vcount

            if mode == 4:
                if n % 3:
                    raise GlbError(f"{where} TRIANGLES element count {n} is not a multiple of 3")
                tris = n // 3
                if idx is not None:
                    topo["degenerate_triangles"] += sum(
                        1 for a, b, c in zip(idx[0::3], idx[1::3], idx[2::3])
                        if a == b or b == c or a == c)
            else:
                tris = max(n - 2, 0)
            mname = TRIANGLE_MODES[mode]
            topo["modes"][mname] = topo["modes"].get(mname, 0) + 1
            topo["primitives"] += 1
            topo["triangles"] += tris
            topo["vertices"] += vcount

            if "material" in prim:
                _item(gltf, "materials", prim["material"], where)
                topo["materials_used"].add(material_names[prim["material"]])
            else:
                topo["primitives_without_material"] += 1
                if where not in topo["primitives_missing_material"]:
                    topo["primitives_missing_material"].append(where)

            plo, phi = _bounds_of(pos, world)
            for a in range(3):
                lo[a] = min(lo[a], plo[a])
                hi[a] = max(hi[a], phi[a])

    if topo["primitives"] == 0:
        raise GlbError("default scene draws no mesh geometry")
    if not all(math.isfinite(v) for v in lo + hi):
        raise GlbError("world-space bounds are non-finite")
    all_meshes = gltf.get("meshes", [])
    unused = len(all_meshes) - len(meshes_seen) if isinstance(all_meshes, list) else 0
    if unused:
        warnings.append(f"{unused} mesh(es) not reachable from the scene; excluded from bounds")
    topo["materials_used"] = sorted(topo["materials_used"])
    topo["meshes_defined"] = len(all_meshes) if isinstance(all_meshes, list) else 0
    topo["textures"] = len(gltf.get("textures", []) or [])
    topo["images"] = len(gltf.get("images", []) or [])
    return {"min": lo, "max": hi}, topo, material_names, warnings


def _check_declared_bounds(acc, idx, pos, warnings):
    if "min" not in acc or "max" not in acc:
        warnings.append(f"accessors[{idx}] POSITION has no declared min/max (required by glTF)")
        return
    dmin = _finite_list(acc["min"], 3, f"accessors[{idx}].min")
    dmax = _finite_list(acc["max"], 3, f"accessors[{idx}].max")
    for a in range(3):
        col = pos[a::3]
        amin, amax = min(col), max(col)
        tol = 1e-4 * max(1.0, abs(amax - amin), abs(dmin[a]), abs(dmax[a]))
        if dmin[a] > dmax[a]:
            raise GlbError(f"accessors[{idx}] declared min > max on axis {'xyz'[a]}")
        if amin < dmin[a] - tol or amax > dmax[a] + tol:
            raise GlbError(f"accessors[{idx}] POSITION data [{amin:g}, {amax:g}] on axis "
                           f"{'xyz'[a]} lies outside declared [{dmin[a]:g}, {dmax[a]:g}]")
        if amin > dmin[a] + tol or amax < dmax[a] - tol:
            warnings.append(f"accessors[{idx}] declared {'xyz'[a]} bounds are looser than data")


# ---------------------------------------------------------------- checks

def load_manifest_ids(path):
    with open(path, newline="", encoding="utf-8") as fh:
        rows = list(csv.DictReader(fh))
    if not rows or "card_id" not in rows[0]:
        raise ValueError(f"{path} has no card_id column")
    return {r["card_id"]: r.get("type", "") for r in rows}


def check_glb(path, card_id=None, manifest_ids=None, expected_height=1.8,
              height_tolerance=0.10, feet_tolerance=0.05, require_materials=False):
    """Return a report dict. status is PASS, FAIL or REJECTED; never writes."""
    report = {"file": path, "status": None, "checks": [], "warnings": [],
              "not_proven": NOT_PROVEN}

    def add(name, ok, detail):
        report["checks"].append({"check": name, "status": "PASS" if ok else "FAIL",
                                 "detail": detail})

    try:
        gltf, bin_chunk, info = read_glb(path)
        bounds, topo, material_names, warnings = analyze(gltf, bin_chunk)
    except GlbError as exc:
        report["status"] = "REJECTED"
        report["error"] = str(exc)
        return report
    except OSError as exc:
        report["status"] = "REJECTED"
        report["error"] = f"cannot read file: {exc}"
        return report

    report["container"] = info
    report["warnings"] = warnings
    lo, hi = bounds["min"], bounds["max"]
    size = [hi[a] - lo[a] for a in range(3)]
    report["bounds"] = {"min": lo, "max": hi, "size": size,
                        "center_xz": [(lo[0] + hi[0]) / 2, (lo[2] + hi[2]) / 2],
                        "tallest_axis": "xyz"[size.index(max(size))]}
    report["topology"] = topo
    report["materials"] = material_names
    add("glb_container", True, f"glTF 2 GLB, {info['bytes']} bytes, generator "
                               f"{info['generator']!r}")

    stem = os.path.splitext(os.path.basename(path))[0]
    problems = []
    if not path.lower().endswith(".glb"):
        problems.append("extension is not .glb")
    if not CARD_ID_RE.match(stem):
        problems.append(f"stem {stem!r} is not a snake_case card_id")
    if card_id is not None and stem != card_id:
        problems.append(f"stem {stem!r} != expected card_id {card_id!r}")
    if manifest_ids is not None:
        if stem in manifest_ids:
            report["manifest_type"] = manifest_ids[stem] or None
        else:
            problems.append(f"{stem!r} not found in manifest card_id column")
    add("filename_card_id", not problems, "; ".join(problems) or f"{stem!r}")

    height = size[1]
    low, high = expected_height * (1 - height_tolerance), expected_height * (1 + height_tolerance)
    add("height", low <= height <= high,
        f"Y extent {height:.4f}; expected {expected_height:g} +/- "
        f"{height_tolerance:.0%} [{low:.4f}, {high:.4f}]")
    add("feet_at_origin", abs(lo[1]) <= feet_tolerance,
        f"min Y {lo[1]:.4f}; expected within +/- {feet_tolerance:g} of 0")

    if require_materials:
        # Out-of-range material indices were already REJECTED in analyze();
        # this only proves each rendered primitive references some material.
        missing = topo["primitives_missing_material"]
        if missing:
            shown = ", ".join(missing[:10]) + (f", ... (+{len(missing) - 10})"
                                               if len(missing) > 10 else "")
            detail = (f"{topo['primitives_without_material']} of {topo['primitives']} "
                      f"rendered primitive(s) have no material: {shown}")
        else:
            detail = (f"all {topo['primitives']} rendered primitive(s) reference a valid "
                      f"material ({', '.join(topo['materials_used'])}); "
                      "material style/separation not assessed")
        add("materials_assigned", not missing, detail)

    report["status"] = "PASS" if all(c["status"] == "PASS" for c in report["checks"]) else "FAIL"
    return report


def format_report(r):
    out = [f"== {r['file']}", f"status: {r['status']}"]
    if r["status"] == "REJECTED":
        out.append(f"error: {r['error']}")
        return "\n".join(out)
    b, t = r["bounds"], r["topology"]
    fmt = lambda v: "(" + ", ".join(f"{x:.4f}" for x in v) + ")"
    for c in r["checks"]:
        out.append(f"  [{c['status']}] {c['check']}: {c['detail']}")
    if r.get("manifest_type"):
        out.append(f"  manifest type: {r['manifest_type']}")
    out.append(f"  bounds min {fmt(b['min'])} max {fmt(b['max'])} size {fmt(b['size'])}")
    out.append(f"  center XZ {fmt(b['center_xz'])}; tallest axis {b['tallest_axis']}")
    out.append(f"  topology: {t['mesh_instances']} mesh instance(s) of {t['meshes_defined']} "
               f"defined, {t['primitives']} primitive(s) {t['modes']}, "
               f"{t['triangles']} triangles, {t['vertices']} vertices, "
               f"{t['degenerate_triangles']} degenerate triangles")
    out.append(f"  materials ({len(r['materials'])}): {', '.join(r['materials']) or 'none'}; "
               f"used: {', '.join(t['materials_used']) or 'none'}; "
               f"primitives without material: {t['primitives_without_material']}; "
               f"textures {t['textures']}, images {t['images']}")
    for w in r["warnings"]:
        out.append(f"  warning: {w}")
    out.append("  not proven: " + " | ".join(r["not_proven"]))
    return "\n".join(out)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("glb", nargs="+", help="GLB file(s) to check (read-only)")
    ap.add_argument("--card-id", help="expected card_id (single file only)")
    ap.add_argument("--manifest", default=DEFAULT_MANIFEST,
                    help="manifest CSV with a card_id column (default: %(default)s)")
    ap.add_argument("--no-manifest", action="store_true", help="skip manifest lookup")
    ap.add_argument("--expected-height", type=float, default=1.8)
    ap.add_argument("--height-tolerance", type=float, default=0.10,
                    help="fractional tolerance (default 0.10 = 10%%)")
    ap.add_argument("--feet-tolerance", type=float, default=0.05,
                    help="max |min Y| in units (default 0.05)")
    ap.add_argument("--require-materials", action="store_true",
                    help="fail if any rendered primitive has no material (opt-in)")
    ap.add_argument("--json", action="store_true", help="print JSON reports")
    args = ap.parse_args(argv)

    if args.card_id and len(args.glb) > 1:
        print("error: --card-id applies to a single file", file=sys.stderr)
        return 2
    for name in ("expected_height", "height_tolerance", "feet_tolerance"):
        v = getattr(args, name)
        if not math.isfinite(v) or v < 0 or (name == "expected_height" and v == 0):
            print(f"error: --{name.replace('_', '-')} must be finite and positive", file=sys.stderr)
            return 2
    manifest_ids = None
    if not args.no_manifest:
        try:
            manifest_ids = load_manifest_ids(args.manifest)
        except (OSError, ValueError, csv.Error) as exc:
            print(f"error: cannot load manifest: {exc} (use --no-manifest to skip)",
                  file=sys.stderr)
            return 2

    reports = [check_glb(p, args.card_id, manifest_ids, args.expected_height,
                         args.height_tolerance, args.feet_tolerance,
                         args.require_materials) for p in args.glb]
    if args.json:
        print(json.dumps(reports if len(reports) > 1 else reports[0], indent=2))
    else:
        print("\n\n".join(format_report(r) for r in reports))
    codes = {"PASS": 0, "FAIL": 1, "REJECTED": 2}
    return max(codes[r["status"]] for r in reports)


if __name__ == "__main__":
    sys.exit(main())
