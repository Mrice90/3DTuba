import subprocess,sys
si=subprocess.STARTUPINFO();si.dwFlags|=subprocess.STARTF_USESHOWWINDOW;si.wShowWindow=4
p=subprocess.Popen(sys.argv[1:],startupinfo=si)
try:code=p.wait(250)
except subprocess.TimeoutExpired:p.kill();code=p.wait()
finally:
 if p.poll() is None:p.kill()
sys.exit(code)
