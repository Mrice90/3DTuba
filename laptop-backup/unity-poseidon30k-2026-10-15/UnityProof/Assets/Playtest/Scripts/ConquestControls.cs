using UnityEngine;
namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        static Texture2D ControlTexture(Color fill,Color edge) {
            var texture=new Texture2D(8,8,TextureFormat.RGBA32,false){name="Conquest control",filterMode=FilterMode.Point,hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[64];
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)pixels[y*8+x]=x==0||y==0||x==7||y==7?edge:fill;
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        GUIStyle ConquestButtonStyle() {
            var style=new GUIStyle(GUI.skin.button){fontSize=14,fontStyle=FontStyle.Bold,border=new RectOffset(1,1,1,1),padding=new RectOffset(8,8,3,3)};
            var normal=ControlTexture(new Color(.035f,.075f,.11f),new Color(.25f,.34f,.40f));
            var hover=ControlTexture(new Color(.09f,.15f,.20f),new Color(.72f,.61f,.40f));
            var selected=ControlTexture(new Color(.17f,.15f,.10f),new Color(.78f,.65f,.40f));
            foreach(var state in new[]{style.normal,style.focused}){state.background=normal;state.textColor=new Color(.87f,.91f,.93f);}
            foreach(var state in new[]{style.hover,style.active}){state.background=hover;state.textColor=Color.white;}
            foreach(var state in new[]{style.onNormal,style.onHover,style.onActive,style.onFocused}){state.background=selected;state.textColor=new Color(1,.86f,.57f);}
            return style;
        }
        public static float LiveStatusWidth(float width)=>Mathf.Max(0,width-128-382);
    }
}
