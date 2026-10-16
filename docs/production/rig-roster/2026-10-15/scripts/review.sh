#!/bin/sh
# review.sh <card> <facing> clip:frame ...  -> out/<card>_review.png (two angles per shot)
n=$1; y=$2; shift 2; d=$(dirname $0)/..
args=""; for s in "$@"; do c=${s%%:*}; f=${s##*:}; args="$args $c:$f:$((y+25)) $c:$f:$((y+100))"; done
rm -f $d/out/rv_*.png
python3 -I $d/scripts/still.py $d/out/$n/${n}_rigged.fbx $d/out/rv $args >/dev/null 2>&1
python3 -c "
import glob,sys; from PIL import Image
fs=sorted(glob.glob('$d/out/rv_*.png'),key=lambda p:p); ims=[Image.open(f).resize((360,360)) for f in fs]
cols=6; s=Image.new('RGB',(360*cols,360*((len(ims)+cols-1)//cols)))
[s.paste(im,((i%cols)*360,(i//cols)*360)) for i,im in enumerate(ims)]; s.save('$d/out/${n}_review.png')"
