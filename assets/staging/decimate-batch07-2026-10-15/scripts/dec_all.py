import subprocess, glob, os, sys
from concurrent.futures import ThreadPoolExecutor
S = "/mnt/user-data/uploads/AI Dev/Infinite Conquest/3DTuba/assets/staging/meshy/batch-07-poseidon-humans-lite/textured"
here = os.path.dirname(os.path.abspath(__file__)); O = os.path.join(here, "out4")
os.makedirs(O, exist_ok=True)
files = [f for f in sorted(glob.glob(S + "/*.glb")) if "reef_tunneler" not in f]
def run(f):
    log = os.path.join(O, "log_" + os.path.basename(f) + ".txt")
    with open(log, "w") as fh:
        r = subprocess.run([sys.executable, "-I", os.path.join(here, "dec_run.py"), f, O, "30000"], stdout=fh, stderr=subprocess.STDOUT, cwd="/tmp")
    return os.path.basename(f), r.returncode
with ThreadPoolExecutor(3) as ex:
    for n, rc in ex.map(run, files): print(n, rc)
