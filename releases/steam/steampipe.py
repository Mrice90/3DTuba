#!/usr/bin/env python3
"""SteamPipe helper for Infinite Conquest: stage a Windows build, render the VDF scripts, upload.

  python steampipe.py stage  --build-dir <unity build> --out <work dir>
  python steampipe.py render --work <work dir> --app-id 1234560 --depot-id 1234561 --desc "0.4.5 RC"
  python steampipe.py upload --work <work dir> --steam-user <login> --confirm-upload

stage   copies only what ships (same exclusions as UnityProof/Tools/installer/build_installer.ps1).
render  writes app_build.vdf + depot_build_windows.vdf into <work>/scripts. Preview (dry run) is the default;
        pass --no-preview to make a real build. --set-live only accepts a non-default branch name
        (Steam refuses to set the default branch from a script; that stays a manual click).
upload  runs steamcmd +run_app_build. It refuses without --confirm-upload, and never stores a password.
Stdlib only; Python 3.8+.
"""
import argparse
import re
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
TEMPLATES = HERE / "templates"
EXE = "InfiniteConquestPlaytest.exe"
REQUIRED = [EXE, "InfiniteConquestPlaytest_Data", "Bridge/jre/bin/java.exe", "Bridge/classes/RulesBridge.class"]
EXCLUDED_NAMES = {"playtest-smoke.json", "PLAY-INFINITE-CONQUEST.bat", "steam_appid.txt"}
EXCLUDED_SUFFIXES = (".png", ".log", ".jsonl", ".pdb")
BACKUP_SUFFIX = "_BackUpThisFolder_ButDontShipItWithYourGame"


def shipped(name):
    return not (name.endswith(BACKUP_SUFFIX) or name in EXCLUDED_NAMES or name.lower().endswith(EXCLUDED_SUFFIXES))


def stage(build_dir, work):
    build_dir, content = Path(build_dir), Path(work) / "content"
    missing = [r for r in REQUIRED if not (build_dir / r).exists()]
    if missing:
        raise SystemExit("build is missing: " + ", ".join(missing))
    if content.exists():
        shutil.rmtree(content)
    content.mkdir(parents=True)
    for item in sorted(build_dir.iterdir()):
        if not shipped(item.name):
            continue
        if item.is_dir():
            shutil.copytree(item, content / item.name)
        else:
            shutil.copy2(item, content / item.name)
    files = [p for p in content.rglob("*") if p.is_file()]
    size = sum(p.stat().st_size for p in files)
    print(f"STAGED {len(files)} files, {size / 2**20:.0f} MB -> {content}")
    return content


def vdf_escape(text):
    return text.replace("\\", "\\\\").replace('"', '\\"')


def render(work, app_id, depot_id, desc, preview=True, set_live=""):
    if not re.fullmatch(r"\d+", str(app_id)) or not re.fullmatch(r"\d+", str(depot_id)):
        raise SystemExit("app id and depot id must be numbers from the Steamworks partner site")
    if set_live.lower() == "default":
        raise SystemExit("refusing --set-live default: set the default branch live by hand in Steamworks")
    if set_live and preview:
        raise SystemExit("--set-live needs --no-preview")
    work = Path(work).resolve()
    if not (work / "content" / EXE).exists():
        raise SystemExit(f"run stage first: {work / 'content' / EXE} not found")
    scripts = work / "scripts"
    scripts.mkdir(parents=True, exist_ok=True)
    values = {
        "APP_ID": str(app_id), "DEPOT_ID": str(depot_id), "DESC": vdf_escape(desc),
        "PREVIEW": "1" if preview else "0", "SET_LIVE": vdf_escape(set_live),
        "CONTENT_ROOT": vdf_escape(str(work / "content")), "BUILD_OUTPUT": vdf_escape(str(work / "output")),
    }
    out = []
    for tpl in sorted(TEMPLATES.glob("*.vdf.in")):
        text = tpl.read_text(encoding="utf-8")
        for key, val in values.items():
            text = text.replace("{" + key + "}", val)
        left = re.findall(r"\{[A-Z_]+\}", text)
        if left:
            raise SystemExit(f"{tpl.name}: unfilled {left}")
        dest = scripts / tpl.name[:-3]
        dest.write_text(text, encoding="utf-8")
        out.append(dest)
    print("RENDERED " + ", ".join(p.name for p in out) + (" (preview)" if preview else ""))
    return out


def upload(work, steam_user, steamcmd, confirm):
    app_vdf = Path(work).resolve() / "scripts" / "app_build.vdf"
    if not app_vdf.exists():
        raise SystemExit("run render first")
    if not confirm:
        raise SystemExit("refusing to upload without --confirm-upload")
    # steamcmd prompts for the password and Steam Guard code itself; nothing is stored here.
    cmd = [steamcmd, "+login", steam_user, "+run_app_build", str(app_vdf), "+quit"]
    print("RUN " + " ".join(cmd))
    return subprocess.call(cmd)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("stage")
    s.add_argument("--build-dir", required=True)
    s.add_argument("--out", required=True)
    r = sub.add_parser("render")
    r.add_argument("--work", required=True)
    r.add_argument("--app-id", required=True)
    r.add_argument("--depot-id", required=True)
    r.add_argument("--desc", required=True)
    r.add_argument("--no-preview", action="store_true")
    r.add_argument("--set-live", default="", help="a beta branch name, never 'default'")
    u = sub.add_parser("upload")
    u.add_argument("--work", required=True)
    u.add_argument("--steam-user", required=True)
    u.add_argument("--steamcmd", default="steamcmd")
    u.add_argument("--confirm-upload", action="store_true")
    a = ap.parse_args(argv)
    if a.cmd == "stage":
        stage(a.build_dir, a.out)
    elif a.cmd == "render":
        render(a.work, a.app_id, a.depot_id, a.desc, preview=not a.no_preview, set_live=a.set_live)
    else:
        return upload(a.work, a.steam_user, a.steamcmd, a.confirm_upload)
    return 0


if __name__ == "__main__":
    sys.exit(main())
