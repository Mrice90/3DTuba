using System;
using UnityEngine;
namespace InfiniteConquest.Playtest {
    public static class TabletopPalette {
        public static readonly string[] Names={"Citadel stone","Moonlit marble","Abyssal jade","Obsidian dusk"};
        public static readonly Color[] Colors={new Color(.55f,.60f,.66f),new Color(1.65f,1.72f,1.85f),new Color(.26f,.85f,.72f),new Color(.48f,.27f,.65f)};
        public static readonly Color[] Metals={new Color(.31f,.24f,.13f),new Color(.55f,.64f,.75f),new Color(.44f,.36f,.17f),new Color(.58f,.35f,.55f)};
        public static int Selected=>Mathf.Clamp(PlayerPrefs.GetInt("ic.tabletop",0),0,Names.Length-1);
        public static void Select(int value){PlayerPrefs.SetInt("ic.tabletop",Mathf.Clamp(value,0,Names.Length-1));PlayerPrefs.Save();}
    }
    public sealed class TabletopAppearance:MonoBehaviour {
        Material surface,inlay;
        public void Initialize(Material stone,Material metal){surface=stone;inlay=metal;Apply();}
        public void Apply(){if(surface!=null)surface.color=TabletopPalette.Colors[TabletopPalette.Selected];if(inlay!=null)inlay.color=TabletopPalette.Metals[TabletopPalette.Selected];}
    }
}
