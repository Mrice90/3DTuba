import tempfile
import unittest
from pathlib import Path

import steampipe


def fake_build(root):
    root = Path(root)
    (root / "InfiniteConquestPlaytest_Data").mkdir(parents=True)
    (root / "InfiniteConquestPlaytest_Data" / "level0").write_bytes(b"x")
    (root / "Bridge" / "jre" / "bin").mkdir(parents=True)
    (root / "Bridge" / "jre" / "bin" / "java.exe").write_bytes(b"j")
    (root / "Bridge" / "classes").mkdir(parents=True)
    (root / "Bridge" / "classes" / "RulesBridge.class").write_bytes(b"c")
    (root / steampipe.EXE).write_bytes(b"exe")
    (root / "UnityPlayer.dll").write_bytes(b"dll")
    for junk in ("shot.png", "Player.log", "events.jsonl", "playtest-smoke.json", "steam_appid.txt", "x.pdb"):
        (root / junk).write_bytes(b"junk")
    (root / ("InfiniteConquestPlaytest" + steampipe.BACKUP_SUFFIX)).mkdir()
    return root


class SteamPipeTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.build = fake_build(Path(self.tmp.name) / "build")
        self.work = Path(self.tmp.name) / "work"

    def tearDown(self):
        self.tmp.cleanup()

    def test_stage_ships_game_and_drops_dev_leftovers(self):
        content = steampipe.stage(self.build, self.work)
        names = {p.name for p in content.iterdir()}
        self.assertEqual(names, {steampipe.EXE, "UnityPlayer.dll", "InfiniteConquestPlaytest_Data", "Bridge"})
        self.assertTrue((content / "Bridge/jre/bin/java.exe").exists())

    def test_stage_refuses_build_without_bundled_java(self):
        (self.build / "Bridge/jre/bin/java.exe").unlink()
        with self.assertRaises(SystemExit) as e:
            steampipe.stage(self.build, self.work)
        self.assertIn("Bridge/jre/bin/java.exe", str(e.exception))

    def test_render_fills_every_placeholder_and_previews_by_default(self):
        steampipe.stage(self.build, self.work)
        app, depot = steampipe.render(self.work, 1234560, 1234561, 'RC "0.4.5"')
        text = app.read_text()
        self.assertIn('"Preview" "1"', text)
        self.assertIn('"SetLive" ""', text)
        self.assertIn('RC \\"0.4.5\\"', text)
        self.assertIn('"1234561" "depot_build_windows.vdf"', text)
        self.assertIn('"DepotID" "1234561"', depot.read_text())
        self.assertNotRegex(text + depot.read_text(), r"\{[A-Z_]+\}")

    def test_render_guards(self):
        steampipe.stage(self.build, self.work)
        for kwargs in ({"set_live": "default", "preview": False}, {"set_live": "beta", "preview": True}):
            with self.assertRaises(SystemExit):
                steampipe.render(self.work, 1, 2, "d", **kwargs)
        with self.assertRaises(SystemExit):
            steampipe.render(self.work, "abc", 2, "d")
        app, _ = steampipe.render(self.work, 1, 2, "d", preview=False, set_live="beta")
        self.assertIn('"SetLive" "beta"', app.read_text())

    def test_upload_needs_confirmation(self):
        steampipe.stage(self.build, self.work)
        steampipe.render(self.work, 1, 2, "d")
        with self.assertRaises(SystemExit) as e:
            steampipe.upload(self.work, "someone", "steamcmd", confirm=False)
        self.assertIn("--confirm-upload", str(e.exception))


if __name__ == "__main__":
    unittest.main()
