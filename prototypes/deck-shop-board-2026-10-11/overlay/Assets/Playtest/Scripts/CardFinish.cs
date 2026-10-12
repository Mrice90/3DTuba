using UnityEngine;

namespace InfiniteConquest.Playtest {
    // Reusable printed-card materials. Textures are created once, never per GUI pass.
    public static class CardFinish {
        static Texture2D rounded, paper, medallion;
        static void Load() {
            if (rounded != null) return;
            rounded = Make(128,180,11,false); paper = Make(128,180,8,true); medallion = Make(64,64,32,false);
        }
        static Texture2D Make(int w,int h,float radius,bool grain) {
            var t=new Texture2D(w,h,TextureFormat.RGBA32,false); t.hideFlags=HideFlags.HideAndDontSave;
            var colors=new Color[w*h];
            for(int y=0;y<h;y++) for(int x=0;x<w;x++) {
                float dx=Mathf.Max(radius-x, x-(w-1-radius),0), dy=Mathf.Max(radius-y,y-(h-1-radius),0);
                float alpha=Mathf.Clamp01(radius-Mathf.Sqrt(dx*dx+dy*dy));
                float light=grain ? .96f + .008f*Mathf.Sin(x*113.77f+y*79.33f) + .015f*y/h : 1;
                colors[y*w+x]=new Color(light,light,light,alpha);
            }
            t.SetPixels(colors);t.Apply(false,true); return t;
        }
        public static void Round(Rect r,Color color,bool parchment=false) {
            Load(); GUI.DrawTexture(r,parchment?paper:rounded,ScaleMode.StretchToFill,true,0,color,0,0);
        }
        public static void Frame(Rect r,Color faction,bool highlighted) {
            Round(new Rect(r.x+3,r.y+6,r.width+3,r.height+3),new Color(0,0,0,.36f));
            if(highlighted) Round(new Rect(r.x-5,r.y-5,r.width+10,r.height+10),new Color(faction.r,faction.g,faction.b,.65f));
            Round(r,new Color(.075f,.065f,.055f));
            Round(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),new Color(.74f,.62f,.39f));
            Round(new Rect(r.x+4,r.y+4,r.width-8,r.height-8),Color.Lerp(faction,new Color(.13f,.12f,.12f),.74f),true);
            Round(new Rect(r.x+6,r.y+6,r.width-12,2),new Color(1,.92f,.67f,.65f));
        }
        public static void Coin(Rect r) {
            Load();GUI.DrawTexture(new Rect(r.x+1,r.y+2,r.width,r.height),medallion,ScaleMode.StretchToFill,true,0,new Color(0,0,0,.5f),0,0);
            GUI.DrawTexture(r,medallion,ScaleMode.StretchToFill,true,0,new Color(.88f,.71f,.35f),0,0);
            GUI.DrawTexture(new Rect(r.x+2,r.y+2,r.width-4,r.height-4),medallion,ScaleMode.StretchToFill,true,0,new Color(.98f,.88f,.58f),0,0);
        }
    }
}
