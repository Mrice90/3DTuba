"""Packaged checks (copied from reviews/restoration-r3/run_packaged.py; output dir and result name configurable): playback smoke + automated both-seat live matches.

Each live run is the exe's own -gameplayLiveSmoke validation (mouse-path handlers, now also the mulligan UI,
command-center activations, displayed capital HP after capital damage/heals, visible rejection).
Usage: python run_packaged.py  -> writes packaged-results.json and per-run folders under checks/.
"""
import os, concurrent.futures, hashlib, json, pathlib, subprocess, sys, time

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
PKG = ROOT / os.environ.get('IC_PKG', 'playtest/unity-build-interrupt-2026-10-11')
OUT = pathlib.Path(os.environ.get('IC_OUT', str(PKG / 'checks')))
RESULT = os.environ.get('IC_RESULT', 'packaged-results.json')
SEEDS = [int(s) for s in sys.argv[1:]] or [42, 713, 7, 3, 101, 2026]


def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def run(name, args, timeout=300):
    out = OUT / name
    out.mkdir(parents=True, exist_ok=True)
    cmd = [str(PKG / 'InfiniteConquestPlaytest.exe'), '-batchmode', '-nographics',
           '-logFile', str(out / 'player.log'), '-playtestResult', str(out / 'result.json')] + args
    si = subprocess.STARTUPINFO(); si.dwFlags |= subprocess.STARTF_USESHOWWINDOW; si.wShowWindow = subprocess.SW_HIDE
    start = time.time()
    p = subprocess.Popen(cmd, cwd=PKG, startupinfo=si, creationflags=subprocess.CREATE_NO_WINDOW)
    timed_out = False
    try:
        code = p.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        timed_out = True; p.kill(); code = p.wait()
    rp = out / 'result.json'
    result = json.loads(rp.read_text()) if rp.exists() else None
    item = dict(name=name, args=args, exit_code=code, timeout=timed_out, seconds=round(time.time() - start, 1), result=result)
    print(json.dumps({k: item[k] for k in ('name', 'exit_code', 'timeout', 'seconds')} | ({'passed': result.get('passed'), 'error': result.get('error')} if result else {})), flush=True)
    return item


jobs = [('smoke', ['-playtestSmoke'])]
for seed in SEEDS:
    for seat in (0, 1):
        jobs.append((f'live-seed{seed}-seat{seat}', ['-gameplayLiveSmoke', '-humanSeat', str(seat), '-seed', str(seed),
                                                      '-bridgeTranscript', str(OUT / f'live-seed{seed}-seat{seat}' / 'bridge-transcript.jsonl')]))
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
    runs = list(pool.map(lambda j: run(*j), jobs))

live = [r for r in runs if r['name'].startswith('live') and r['result']]
keys = ['actions', 'abilityListChecks', 'commandCenterActivations', 'capitalDamageChecks', 'stormRelayChecks', 'healChecks',
        'losBlockedSeen', 'buriedSourcesSeen', 'hpSnapshotChecks', 'reactionWindows', 'reactionCueChecks']
totals = {k: sum(r['result'].get(k, 0) for r in live) for k in keys}
report = dict(package=str(PKG), assembly_sha256=sha(PKG / 'InfiniteConquestPlaytest_Data/Managed/Assembly-CSharp.dll'),
              bridge_class_sha256=sha(PKG / 'Bridge/classes/RulesBridge.class'), runs=runs, totals=totals,
              passed=all(r['exit_code'] == 0 and r['result'] and r['result'].get('passed') for r in runs),
              mulligan=[dict(name=r['name'], opening=r['result'].get('openingHand'), replaced=r['result'].get('mulliganReplaced'),
                             after=r['result'].get('handAfterMulligan')) for r in live],
              game_over=[dict(name=r['name'], winner=r['result'].get('winner'), turn=r['result'].get('turn')) for r in live])
(HERE / RESULT).write_text(json.dumps(report, indent=2))
print(json.dumps({k: report[k] for k in ('passed', 'totals')}, indent=2))
sys.exit(0 if report['passed'] else 1)
