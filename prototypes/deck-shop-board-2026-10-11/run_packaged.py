"""Packaged checks for the deck shop + board build (from reviews/board-2026-10-11 and reviews/deck-shop-r2-2026-10-11).

Headless: playback smoke + both-seat -gameplayLiveSmoke matches to GAME_OVER on the rules grid, -board 6x8 and -board hex.
Windowed 1280x720 (one at a time): -shopPreview (open shop, Lands tab, affordable characters, empty filter, a real purchase)
and -gameplayLiveShots (board, printed hand, purchase market, ...) on each board, both seats for the live shots.
Usage: python run_packaged.py [seed ...]  -> evidence/packaged-results.json, per-run folders under <package>/checks/.
"""
import concurrent.futures, hashlib, json, os, pathlib, subprocess, sys, time

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
PKG = ROOT / 'playtest/unity-build-deck-shop-2026-10-11'
OUT = PKG / 'checks'
SEEDS = [int(s) for s in sys.argv[1:]] or [42, 713, 7]
BOARDS = {'default': [], '6x8': ['-board', '6x8'], 'hex': ['-board', 'hex']}


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest() if p.exists() else None


def run(name, args, headless=True, timeout=400):
    out = OUT / name
    out.mkdir(parents=True, exist_ok=True)
    cmd = [str(PKG / 'InfiniteConquestPlaytest.exe')] + (['-batchmode', '-nographics'] if headless else
           ['-screen-fullscreen', '0', '-screen-width', '1280', '-screen-height', '720']) + \
          ['-logFile', str(out / 'player.log'), '-playtestResult', str(out / 'result.json')] + args
    start = time.time()
    p = subprocess.Popen(cmd, cwd=PKG)
    timed_out = False
    try:
        code = p.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        timed_out = True; p.kill(); code = p.wait()
    rp = out / 'result.json'
    result = json.loads(rp.read_text()) if rp.exists() else None
    item = dict(name=name, args=args, exit_code=code, timeout=timed_out, seconds=round(time.time() - start, 1), result=result)
    print(json.dumps({k: item[k] for k in ('name', 'exit_code', 'timeout', 'seconds')} | ({'passed': result.get('passed'), 'error': result.get('error'), 'board': result.get('boardDrawn')} if result else {})), flush=True)
    return item


def live(name, seed, seat, extra=()):
    return (name, ['-gameplayLiveSmoke', '-humanSeat', str(seat), '-seed', str(seed),
                   '-bridgeTranscript', str(OUT / name / 'bridge-transcript.jsonl')] + list(extra))


jobs = [('smoke', ['-playtestSmoke'])]
for board, extra in BOARDS.items():
    for seed in (SEEDS if board == 'default' else SEEDS[:2]):
        for seat in (0, 1):
            jobs.append(live(f'{board}-seed{seed}-seat{seat}', seed, seat, extra))
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
    runs = list(pool.map(lambda j: run(*j), jobs))
# Windowed runs one at a time so each owns the screen.
for board, extra in BOARDS.items():
    name = f'shop-{board}-1280x720'
    runs.append(run(name, ['-shopPreview', str(OUT / name)] + extra, headless=False, timeout=150))
    for seat in (0, 1):
        name = f'shots-{board}-seed42-seat{seat}-1280x720'
        n, a = live(name, 42, seat, extra + ['-gameplayLiveShots', str(OUT / name)])
        runs.append(run(n, a, headless=False))

livers = [r for r in runs if r['name'] != 'smoke' and not r['name'].startswith('shop-') and r['result']]
keys = ['actions', 'abilityListChecks', 'commandCenterActivations', 'capitalDamageChecks', 'stormRelayChecks', 'healChecks',
        'hpSnapshotChecks', 'reactionWindows', 'reactionCueChecks']
totals = {k: sum(r['result'].get(k, 0) for r in livers) for k in keys}
report = dict(package=str(PKG), assembly_sha256=sha(PKG / 'InfiniteConquestPlaytest_Data/Managed/Assembly-CSharp.dll'),
              runs=runs, totals=totals,
              passed=all(r['exit_code'] == 0 and r['result'] and r['result'].get('passed') for r in runs),
              game_over=[dict(name=r['name'], board=r['result'].get('boardDrawn'), winner=r['result'].get('winner'),
                              turn=r['result'].get('turn'), actions=r['result'].get('actions')) for r in livers])
(HERE / 'evidence').mkdir(exist_ok=True)
(HERE / 'evidence/packaged-results.json').write_text(json.dumps(report, indent=2))
print(json.dumps({k: report[k] for k in ('passed', 'totals', 'game_over')}, indent=2))
sys.exit(0 if report['passed'] else 1)
