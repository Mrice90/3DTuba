# runbatch.sh <listfile> : rig + ground check + stretch QA + review sheet for each card in out2/
one() { c=$1; o=out2/$c; mkdir -p $o; f=$(python3 -c "import json;print(int(json.load(open('cfg2/$c.json')).get('facing',0)))")
  python3 -I scripts/rig_posed.py -- src2/$c.glb cfg2/$c.json $o $c > $o/rig.log 2>&1 || { echo FAILRIG $c; return; }
  python3 -I scripts/actz.py $o/${c}_rigged.blend 2>/dev/null | grep ACTZ | sed "s/^/$c /" >> actz2.log
  python3 -I scripts/qa.py $o/${c}_rigged.blend 2>/dev/null | grep '^{' | tail -1 > $o/${c}_qa.json
  python3 -I scripts/sheet2.py $o/${c}_rigged.fbx $o $c $f > $o/sheet.log 2>&1
  echo DONE $c; }
export -f one
cat $1 | xargs -P ${P:-2} -I{} bash -c 'one {}'
echo ALLDONE
