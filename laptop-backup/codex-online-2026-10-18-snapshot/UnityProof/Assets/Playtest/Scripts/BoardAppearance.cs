using System;
using System.Linq;
using UnityEngine;
namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        bool appearanceFromPause;
        CoinSkin[] appearanceCoins;
        Vector2 appearanceScroll;
        GUIStyle appearanceHeading,appearanceLabel,appearanceChoice,appearanceSmall,appearanceHint,appearanceCoin;
        public static float AppearanceShift(float width)=>-width*.20f;
        public static Rect AppearancePanel(float width,float height)=>new Rect(18,Mathf.Max(72,(height-510)/2),Mathf.Clamp(width*.35f,340,410),Mathf.Min(510,height-90));
        void DrawBoardAppearance(Action back) {
            MenuStyles();
            if(appearanceHeading==null){
                appearanceHeading=new GUIStyle(menuHeading){fontSize=23,alignment=TextAnchor.MiddleCenter};
                appearanceLabel=new GUIStyle(menuBody){fontSize=14};
                appearanceChoice=new GUIStyle(menuButton){fontSize=15};
                appearanceSmall=new GUIStyle(appearanceLabel){fontSize=12,wordWrap=true};
                appearanceHint=new GUIStyle(appearanceSmall){alignment=TextAnchor.MiddleCenter};
                appearanceCoin=new GUIStyle(appearanceChoice);
            }
            if(rig!=null)rig.ScreenShiftPx=AppearanceShift(Screen.width);
            var rect=AppearancePanel(Screen.width,Screen.height);GUI.Box(rect,"",panelStyle);
            GUI.Label(new Rect(rect.x+12,rect.y+10,rect.width-24,35),"Battlefield style",appearanceHeading);
            var area=new Rect(rect.x+14,rect.y+52,rect.width-28,rect.height-113);
            appearanceScroll=GUI.BeginScrollView(area,appearanceScroll,new Rect(0,0,area.width-18,397));
            float w=area.width-18;
            GUI.Label(new Rect(4,4,116,24),"Background",appearanceLabel);
            if(GUI.Button(new Rect(122,0,w-122,35),TabletopPalette.Names[TabletopPalette.Selected]+"  >",appearanceChoice)){
                TabletopPalette.Select((TabletopPalette.Selected+1)%TabletopPalette.Names.Length);
                board.GetComponentInChildren<TabletopAppearance>()?.Apply();sfx.PlayUi("click");
            }
            GUI.Label(new Rect(4,38,w-8,22),"Your view only · saved automatically",appearanceSmall);
            for(int seat=0;seat<2;seat++){
                float y=66+seat*66;GUI.Label(new Rect(4,y+4,116,24),Faction(seat)+" hexes",appearanceLabel);
                var skin=BoardSkin.Selected(seat);bool old=GUI.enabled;GUI.enabled=old&&!MatchCosmetics.Active;
                if(GUI.Button(new Rect(122,y,w-122,35),(skin!=null?skin.displayName:"Default")+"  >",appearanceChoice)){
                    int index=Array.IndexOf(BoardSkin.Presets,skin!=null?skin.name:"");BoardSkin.Select(seat,BoardSkin.Presets[(index+1+BoardSkin.Presets.Length)%BoardSkin.Presets.Length]);board.ApplySkins();sfx.PlayUi("click");
                }
                GUI.enabled=old;
                GUI.Label(new Rect(4,y+38,w-8,22),MatchCosmetics.Active?"Chosen by this player for this match":"Your preset when playing as "+Faction(seat),appearanceSmall);
            }
            GUI.Label(new Rect(4,202,116,24),"Your coin",appearanceLabel);
            if(appearanceCoins==null)appearanceCoins=Resources.LoadAll<CoinSkin>("CoinSkins").OrderBy(s=>s.name).ToArray();
            var coin=CoinSkin.Selected;
            if(GUI.Button(new Rect(122,198,w-122,35),CoinSkin.Label(coin)+"  >",appearanceCoin)&&appearanceCoins.Length>0){
                CoinSkin.Select(appearanceCoins[(Array.IndexOf(appearanceCoins,coin)+1)%appearanceCoins.Length].name);coin=CoinSkin.Selected;sfx.PlayUi("click");
            }
            GUI.Label(new Rect(4,237,w-8,22),"Personal coin finish · saved on this device",appearanceSmall);
            if(coin!=null){
                var prior=GUI.color;GUI.color=coin.faceTint;
                if(coin.front!=null)GUI.DrawTexture(new Rect(w/2-80,266,70,70),coin.front,ScaleMode.ScaleToFit,true,0,Color.white,0,35);
                if(coin.back!=null)GUI.DrawTexture(new Rect(w/2+10,266,70,70),coin.back,ScaleMode.ScaleToFit,true,0,Color.white,0,35);
                GUI.color=prior;
            }
            GUI.Label(new Rect(4,350,w-8,44),"Cosmetics never change the rules or opening coin result.",appearanceHint);
            GUI.EndScrollView();
            if(GUI.Button(new Rect(rect.x+rect.width/2-100,rect.yMax-49,200,35),"Back",appearanceChoice)){if(rig!=null)rig.ScreenShiftPx=0;sfx.PlayCore("ui_back",.2f);back();}
        }
    }
}
