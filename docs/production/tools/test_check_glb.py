#!/usr/bin/env python3
"""Synthetic-fixture tests for check_glb.py (stdlib unittest; fixtures live in a temp dir)."""

import io
import json
import math
import os
import struct
import sys
import tempfile
import unittest
from contextlib import redirect_stdout, redirect_stderr

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import check_glb  # noqa: E402

CARD = "zeus_ability_skyline_seer"
MANIFEST = {CARD: "CHARACTER"}


def box(w=0.5, h=1.8, d=0.4, y0=0.0):
    """8 corners and 12 triangles of an axis-aligned box standing on y = y0."""
    xs, zs = (-w / 2, w / 2), (-d / 2, d / 2)
    pos = [(x, y, z) for y in (y0, y0 + h) for z in zs for x in xs]
    tris = [0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5, 0, 4, 5, 0, 5, 1,
            2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3]
    return pos, tris


def build(pos=None, tris=None, node=None, materials=("robe", "armor"), mode=None,
          material=0):
    """Return (gltf_json_dict, bin_bytes) for a single-mesh scene.

    material=None omits the primitive's material reference."""
    if pos is None:
        pos, tris = box()
    flat = [c for p in pos for c in p]
    pbytes = struct.pack(f"<{len(flat)}f", *flat)
    ibytes = struct.pack(f"<{len(tris)}H", *tris) if tris is not None else b""
    ibytes += b"\x00" * (-len(ibytes) % 4)
    finite = [p for p in pos if all(math.isfinite(c) for c in p)] or [(0, 0, 0)]
    prim = {"attributes": {"POSITION": 0}}
    if material is not None:
        prim["material"] = material
    if tris is not None:
        prim["indices"] = 1
    if mode is not None:
        prim["mode"] = mode
    gltf = {
        "asset": {"version": "2.0", "generator": "test_check_glb"},
        "scene": 0, "scenes": [{"nodes": [0]}],
        "nodes": [dict(node or {}, mesh=0)],
        "meshes": [{"primitives": [prim]}],
        "materials": [{"name": n} for n in materials],
        "buffers": [{"byteLength": len(pbytes) + len(ibytes)}],
        "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": len(pbytes)}],
        "accessors": [{"bufferView": 0, "componentType": 5126, "count": len(pos),
                       "type": "VEC3",
                       "min": [min(p[a] for p in finite) for a in range(3)],
                       "max": [max(p[a] for p in finite) for a in range(3)]}],
    }
    if tris is not None:
        gltf["bufferViews"].append({"buffer": 0, "byteOffset": len(pbytes),
                                    "byteLength": len(tris) * 2})
        gltf["accessors"].append({"bufferView": 1, "componentType": 5123,
                                  "count": len(tris), "type": "SCALAR"})
    return gltf, pbytes + ibytes


def pack(gltf, binary, json_text=None):
    j = (json_text if json_text is not None else json.dumps(gltf)).encode("utf-8")
    j += b" " * (-len(j) % 4)
    binary += b"\x00" * (-len(binary) % 4)
    body = struct.pack("<II", len(j), 0x4E4F534A) + j
    if binary:
        body += struct.pack("<II", len(binary), 0x004E4942) + binary
    return struct.pack("<4sII", b"glTF", 2, 12 + len(body)) + body


class Base(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.dir = self._tmp.name

    def tearDown(self):
        self._tmp.cleanup()

    def write(self, data, name=CARD + ".glb"):
        path = os.path.join(self.dir, name)
        with open(path, "wb") as fh:
            fh.write(data)
        return path

    def run_check(self, data, name=CARD + ".glb", **kw):
        kw.setdefault("card_id", CARD)
        kw.setdefault("manifest_ids", MANIFEST)
        return check_glb.check_glb(self.write(data, name), **kw)

    def statuses(self, report):
        return {c["check"]: c["status"] for c in report["checks"]}

    def assertRejected(self, report, fragment):
        self.assertEqual(report["status"], "REJECTED", report)
        self.assertIn(fragment, report["error"])


class ValidFixture(Base):
    def test_valid_passes_and_reports(self):
        r = self.run_check(pack(*build()))
        self.assertEqual(r["status"], "PASS", r)
        self.assertEqual(r["manifest_type"], "CHARACTER")
        self.assertAlmostEqual(r["bounds"]["size"][1], 1.8, places=5)
        self.assertAlmostEqual(r["bounds"]["min"][1], 0.0, places=6)
        t = r["topology"]
        self.assertEqual((t["triangles"], t["vertices"], t["primitives"]), (12, 8, 1))
        self.assertEqual(t["degenerate_triangles"], 0)
        self.assertEqual(r["materials"], ["robe", "armor"])
        self.assertEqual(t["materials_used"], ["robe"])
        self.assertTrue(any("orientation" in n for n in r["not_proven"]))

    def test_node_scale_is_applied(self):
        pos, tris = box(h=1.0)
        r = self.run_check(pack(*build(pos, tris, node={"scale": [1, 1.8, 1]})))
        self.assertEqual(r["status"], "PASS", r)

    def test_node_matrix_translation_fixes_origin(self):
        pos, tris = box(y0=-0.9)
        m = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0.9, 0, 1]  # column-major, ty = 0.9
        r = self.run_check(pack(*build(pos, tris, node={"matrix": m})))
        self.assertEqual(r["status"], "PASS", r)

    def test_rotation_moves_height_off_y(self):
        s = math.sqrt(0.5)  # 90 degrees about X: model now lies along Z
        r = self.run_check(pack(*build(node={"rotation": [s, 0, 0, s]})))
        self.assertEqual(r["status"], "FAIL")
        self.assertEqual(self.statuses(r)["height"], "FAIL")
        self.assertEqual(r["bounds"]["tallest_axis"], "z")

    def test_unindexed_triangles(self):
        pos, tris = box()
        r = self.run_check(pack(*build([pos[i] for i in tris], None)))
        self.assertEqual(r["status"], "PASS", r)
        self.assertEqual(r["topology"]["triangles"], 12)

    def test_degenerate_triangles_counted(self):
        pos, tris = box()
        r = self.run_check(pack(*build(pos, tris + [0, 0, 1])))
        self.assertEqual(r["topology"]["degenerate_triangles"], 1)

    def test_original_file_unchanged(self):
        data = pack(*build())
        path = self.write(data)
        check_glb.check_glb(path, CARD, MANIFEST)
        with open(path, "rb") as fh:
            self.assertEqual(fh.read(), data)


class WrongFixtures(Base):
    def test_wrong_filename(self):
        r = self.run_check(pack(*build()), name="Skyline-Seer_v2.glb")
        self.assertEqual(r["status"], "FAIL")
        self.assertEqual(self.statuses(r)["filename_card_id"], "FAIL")
        self.assertEqual(self.statuses(r)["height"], "PASS")

    def test_card_id_mismatch(self):
        r = self.run_check(pack(*build()), name="poseidon_abyss_gate.glb")
        self.assertIn("!= expected card_id", r["checks"][1]["detail"])

    def test_not_in_manifest(self):
        r = self.run_check(pack(*build()), name="zeus_unknown_card.glb", card_id=None)
        self.assertEqual(self.statuses(r)["filename_card_id"], "FAIL")
        self.assertIn("not found in manifest", r["checks"][1]["detail"])

    def test_wrong_scale_centimetres(self):
        pos, tris = box(w=50, h=180, d=40)
        r = self.run_check(pack(*build(pos, tris)))
        self.assertEqual(self.statuses(r)["height"], "FAIL")
        self.assertEqual(self.statuses(r)["feet_at_origin"], "PASS")

    def test_scale_tolerance_edges(self):
        for h, expect in ((1.63, "PASS"), (1.97, "PASS"), (1.61, "FAIL"), (1.99, "FAIL")):
            pos, tris = box(h=h)
            r = self.run_check(pack(*build(pos, tris)))
            self.assertEqual(self.statuses(r)["height"], expect, h)

    def test_wrong_origin_centered(self):
        pos, tris = box(y0=-0.9)
        r = self.run_check(pack(*build(pos, tris)))
        self.assertEqual(self.statuses(r)["feet_at_origin"], "FAIL")
        self.assertEqual(self.statuses(r)["height"], "PASS")

    def test_wrong_origin_via_node_translation(self):
        r = self.run_check(pack(*build(node={"translation": [0, 0.5, 0]})))
        self.assertEqual(self.statuses(r)["feet_at_origin"], "FAIL")


class MalformedFixtures(Base):
    def test_nan_position(self):
        pos, tris = box()
        pos[3] = (float("nan"), 1.8, 0.2)
        self.assertRejected(self.run_check(pack(*build(pos, tris))), "non-finite")

    def test_inf_position(self):
        pos, tris = box()
        pos[5] = (0.25, float("inf"), 0.2)
        self.assertRejected(self.run_check(pack(*build(pos, tris))), "non-finite")

    def test_data_outside_declared_bounds(self):
        g, b = build()
        g["accessors"][0]["max"][1] = 1.0
        self.assertRejected(self.run_check(pack(g, b)), "outside declared")

    def test_nonfinite_declared_bounds_json(self):
        g, b = build()
        text = json.dumps(g).replace('"max": [0.25, 1.8', '"max": [0.25, NaN', 1)
        self.assertIn("NaN", text)
        self.assertRejected(self.run_check(pack(g, b, json_text=text)), "NaN")

    def test_declared_min_greater_than_max(self):
        g, b = build()
        g["accessors"][0]["min"][0], g["accessors"][0]["max"][0] = 1.0, -1.0
        self.assertRejected(self.run_check(pack(g, b)), "min > max")

    def test_accessor_past_buffer_view(self):
        g, b = build()
        g["accessors"][0]["count"] = 1000
        self.assertRejected(self.run_check(pack(g, b)), "reads past the end")

    def test_buffer_view_past_buffer(self):
        g, b = build()
        g["bufferViews"][0]["byteLength"] = 10 ** 6
        self.assertRejected(self.run_check(pack(g, b)), "outside buffers[0]")

    def test_buffer_longer_than_bin_chunk(self):
        g, b = build()
        g["buffers"][0]["byteLength"] = len(b) + 64
        self.assertRejected(self.run_check(pack(g, b)), "exceeds BIN chunk")

    def test_index_out_of_range(self):
        pos, tris = box()
        self.assertRejected(self.run_check(pack(*build(pos, tris[:-1] + [99]))), "out of range")

    def test_nonfinite_node_transform(self):
        g, b = build(node={"translation": [0, 0, 0]})
        text = json.dumps(g).replace('"translation": [0, 0, 0]', '"translation": [0, Infinity, 0]')
        self.assertRejected(self.run_check(pack(g, b, json_text=text)), "Infinity")

    def test_overflowing_node_transform(self):
        g, b = build(node={"scale": [1, 1e300, 1], "children": [1]})
        g["nodes"].append({"scale": [1, 1e300, 1]})
        self.assertRejected(self.run_check(pack(g, b)), "non-finite")

    def test_node_cycle(self):
        g, b = build(node={"children": [0]})
        self.assertRejected(self.run_check(pack(g, b)), "not a tree")

    def test_truncated_file(self):
        self.assertRejected(self.run_check(pack(*build())[:-20]), "header declares")

    def test_bad_magic(self):
        data = bytearray(pack(*build()))
        data[0:4] = b"gltf"
        self.assertRejected(self.run_check(bytes(data)), "bad magic")

    def test_version_one(self):
        data = bytearray(pack(*build()))
        data[4:8] = struct.pack("<I", 1)
        self.assertRejected(self.run_check(bytes(data)), "version 1")

    def test_tiny_file(self):
        self.assertRejected(self.run_check(b"glTF"), "12")

    def test_chunk_length_past_end(self):
        data = bytearray(pack(*build()))
        data[12:16] = struct.pack("<I", 10 ** 6)
        self.assertRejected(self.run_check(bytes(data)), "past end of file")

    def test_json_garbage(self):
        g, b = build()
        self.assertRejected(self.run_check(pack(g, b, json_text="{not json")), "does not parse")

    def test_missing_bin_chunk(self):
        g, _ = build()
        self.assertRejected(self.run_check(pack(g, b"")), "no BIN chunk")


class UnsupportedFixtures(Base):
    def test_points_primitive(self):
        pos, _ = box()
        self.assertRejected(self.run_check(pack(*build(pos, None, mode=0))), "POINTS")

    def test_draco_required(self):
        g, b = build()
        g["extensionsRequired"] = ["KHR_draco_mesh_compression"]
        self.assertRejected(self.run_check(pack(g, b)), "KHR_draco_mesh_compression")

    def test_quantized_positions(self):
        g, b = build()
        g["accessors"][0]["componentType"] = 5123
        self.assertRejected(self.run_check(pack(g, b)), "componentType")

    def test_sparse_accessor(self):
        g, b = build()
        g["accessors"][0]["sparse"] = {"count": 1}
        self.assertRejected(self.run_check(pack(g, b)), "sparse")

    def test_external_buffer(self):
        g, b = build()
        g["buffers"][0]["uri"] = "../../secret.bin"
        self.assertRejected(self.run_check(pack(g, b)), "external")

    def test_no_geometry(self):
        g, b = build()
        del g["nodes"][0]["mesh"]
        self.assertRejected(self.run_check(pack(g, b)), "no mesh geometry")


def with_second_primitive(gltf, material):
    """Add a second primitive (same geometry) to mesh 0; material=None omits it."""
    prim = dict(gltf["meshes"][0]["primitives"][0])
    prim.pop("material", None)
    if material is not None:
        prim["material"] = material
    gltf["meshes"][0]["primitives"].append(prim)
    return gltf


class RequireMaterials(Base):
    def check(self, g, b, **kw):
        return self.run_check(pack(g, b), require_materials=True, **kw)

    def test_default_does_not_add_material_check(self):
        r = self.run_check(pack(*build(material=None)))
        self.assertEqual(r["status"], "PASS", r)
        self.assertNotIn("materials_assigned", self.statuses(r))
        self.assertEqual(r["topology"]["primitives_without_material"], 1)

    def test_valid_materials_pass(self):
        g, b = with_second_primitive(build()[0], 1), build()[1]
        r = self.check(g, b)
        self.assertEqual(r["status"], "PASS", r)
        self.assertEqual(self.statuses(r)["materials_assigned"], "PASS")
        self.assertEqual(r["topology"]["materials_used"], ["armor", "robe"])
        self.assertIn("not assessed", r["checks"][-1]["detail"])

    def test_unnamed_material_still_counts_as_assigned(self):
        g, b = build()
        g["materials"] = [{}]
        r = self.check(g, b)
        self.assertEqual(self.statuses(r)["materials_assigned"], "PASS")
        self.assertEqual(r["materials"], ["<unnamed #0>"])

    def test_missing_material_fails(self):
        r = self.check(*build(material=None))
        self.assertEqual(r["status"], "FAIL")
        self.assertEqual(self.statuses(r)["materials_assigned"], "FAIL")
        self.assertIn("meshes[0].primitives[0]", r["checks"][-1]["detail"])
        self.assertEqual(self.statuses(r)["height"], "PASS")

    def test_no_materials_array_fails(self):
        g, b = build(material=None)
        del g["materials"]
        r = self.check(g, b)
        self.assertEqual(self.statuses(r)["materials_assigned"], "FAIL")

    def test_one_of_two_primitives_missing_fails(self):
        g, b = build()
        r = self.check(with_second_primitive(g, None), b)
        self.assertEqual(self.statuses(r)["materials_assigned"], "FAIL")
        detail = r["checks"][-1]["detail"]
        self.assertIn("1 of 2", detail)
        self.assertIn("meshes[0].primitives[1]", detail)
        self.assertNotIn("primitives[0]", detail)

    def test_unrendered_mesh_without_material_is_ignored(self):
        g, b = build()
        g["meshes"].append({"primitives": [{"attributes": {"POSITION": 0}}]})
        r = self.check(g, b)
        self.assertEqual(self.statuses(r)["materials_assigned"], "PASS", r)

    def test_out_of_range_material_rejected_in_both_modes(self):
        for flag in (False, True):
            r = self.run_check(pack(*build(material=5)), require_materials=flag)
            self.assertRejected(r, "invalid materials index 5")

    def test_negative_or_non_integer_material_rejected(self):
        for bad in (-1, "0", True, 1.0):
            r = self.check(*build(material=bad))
            self.assertRejected(r, "invalid materials index")

    def test_material_reference_without_materials_array_rejected(self):
        g, b = build()
        del g["materials"]
        self.assertRejected(self.check(g, b), "'materials' is missing")


class Cli(Base):
    def cli(self, *argv):
        out, err = io.StringIO(), io.StringIO()
        with redirect_stdout(out), redirect_stderr(err):
            code = check_glb.main(list(argv))
        return code, out.getvalue(), err.getvalue()

    def test_exit_codes(self):
        good = self.write(pack(*build()))
        pos, tris = box(y0=-0.9)
        bad = self.write(pack(*build(pos, tris)), "zeus_offset_card.glb")
        broken = self.write(b"not a glb at all", "zeus_broken_card.glb")
        self.assertEqual(self.cli(good, "--no-manifest", "--card-id", CARD)[0], 0)
        self.assertEqual(self.cli(bad, "--no-manifest")[0], 1)
        self.assertEqual(self.cli(broken, "--no-manifest")[0], 2)
        self.assertEqual(self.cli(good, bad, broken, "--no-manifest")[0], 2)

    def test_require_materials_flag(self):
        good = self.write(pack(*build()))
        bare = self.write(pack(*build(material=None)), "zeus_bare_card.glb")
        broken = self.write(pack(*build(material=9)), "zeus_badmat_card.glb")
        self.assertEqual(self.cli(bare, "--no-manifest")[0], 0)  # default unchanged
        code, out, _ = self.cli(bare, "--no-manifest", "--require-materials")
        self.assertEqual(code, 1)
        self.assertIn("[FAIL] materials_assigned", out)
        code, out, _ = self.cli(good, "--no-manifest", "--require-materials")
        self.assertEqual(code, 0)
        self.assertIn("[PASS] materials_assigned", out)
        self.assertEqual(self.cli(broken, "--no-manifest")[0], 2)
        self.assertEqual(self.cli(broken, "--no-manifest", "--require-materials")[0], 2)
        code, out, _ = self.cli(bare, "--no-manifest", "--require-materials", "--json")
        self.assertEqual(json.loads(out)["checks"][-1]["check"], "materials_assigned")

    def test_json_output(self):
        good = self.write(pack(*build()))
        code, out, _ = self.cli(good, "--no-manifest", "--json")
        self.assertEqual(code, 0)
        self.assertEqual(json.loads(out)["status"], "PASS")

    def test_card_id_with_many_files_is_usage_error(self):
        good = self.write(pack(*build()))
        self.assertEqual(self.cli(good, good, "--card-id", CARD, "--no-manifest")[0], 2)

    def test_missing_manifest_is_usage_error(self):
        good = self.write(pack(*build()))
        code, _, err = self.cli(good, "--manifest", os.path.join(self.dir, "nope.csv"))
        self.assertEqual(code, 2)
        self.assertIn("--no-manifest", err)

    def test_default_manifest_contains_card(self):
        ids = check_glb.load_manifest_ids(check_glb.DEFAULT_MANIFEST)
        self.assertEqual(ids.get(CARD), "CHARACTER")


if __name__ == "__main__":
    unittest.main()
