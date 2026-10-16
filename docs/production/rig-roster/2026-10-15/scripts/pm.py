# pm.py out.png card1 card2 ... : stack front+side probe panels (with grid) vertically
import sys
from PIL import Image
out=sys.argv[1]; ims=[Image.open(f'probe2/{c}_probe.png').crop((0,0,1536,768)) for c in sys.argv[2:]]
o=Image.new('RGB',(1536,768*len(ims)))
for i,im in enumerate(ims): o.paste(im,(0,768*i))
o.save(out)
