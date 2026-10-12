using System.Linq;
using UnityEngine;
namespace InfiniteConquest.Playtest {
 public sealed partial class PlaytestGame {
  Rect marketRect; bool marketOpen; Vector2 marketScroll;
  int marketTab,marketSort,marketMin,marketMax=10;bool marketAffordable;
  string marketSearch="",marketSelected;
  BridgeMarketCard[] marketSource,marketFiltered=new BridgeMarketCard[0];string marketQueryKey;
  static readonly string[] MarketSortNames={"Price: low first","Price: high first","Name: A-Z","Copies remaining"};
  void DrawPurchaseMarket(float W,float H) {
   marketRect=Rect.zero;
   if(mode!=Mode.Live || liveState==null || !liveState.purchase_economy)return;
   var source=liveState.market??new BridgeMarketCard[0];
   foreach(var item in source)CardFaces.SetPurchasePrice(item.card_id,item.price);
   var toggle=new Rect(12,H-94,180,30);
   if(!marketOpen && GUI.Button(toggle,"Buy from deck",buttonStyle))marketOpen=true;
   marketRect=toggle;
   if(!marketOpen)return;
   GUI.DrawTexture(new Rect(0,0,W,H),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(0,0,0,.5f),0,0);
   var panel=new Rect(Mathf.Max(12,(W-1000)/2),76,Mathf.Min(1000,W-24),Mathf.Max(330,H-110));
   marketRect=new Rect(0,0,W,H);
   CardFinish.Round(panel,new Color(.025f,.035f,.055f,.99f));GUI.Box(panel,"",panelStyle);
   int gold=liveState.players[humanSeat].gp;
   GUI.Label(new Rect(panel.x+16,panel.y+10,panel.width-150,28),"YOUR DECK   |   "+gold+" gold",titleStyle);
   if(GUI.Button(new Rect(panel.xMax-98,panel.y+10,82,28),"Close",buttonStyle))marketOpen=false;
   GUI.Label(new Rect(panel.x+16,panel.y+42,panel.width-32,22),"Buy once, play free  |  One purchase per category each turn  |  Free draws bypass the limit",smallStyle);
   float tabW=(panel.width-32)/5;
   for(int i=0;i<5;i++) {
    string category=DeckMarketQuery.Categories[i];int count=source.Where(c=>i==0 || c.category==category).Sum(c=>c.remaining);
    bool used=i>0 && source.Any(c=>c.category==category && c.bought>0);
    Color prior=GUI.backgroundColor;GUI.backgroundColor=marketTab==i?new Color(.32f,.65f,.85f):Color.white;
    if(GUI.Button(new Rect(panel.x+16+i*tabW,panel.y+70,tabW-4,30),(i==0?"All":category.Substring(0,1)+category.Substring(1).ToLowerInvariant()+"s")+" ("+count+")"+(used?" (used)":""),buttonStyle))marketTab=i;
    GUI.backgroundColor=prior;
   }
   float controls=panel.y+110;
   GUI.Label(new Rect(panel.x+16,controls,50,25),"Search",smallStyle);
   marketSearch=GUI.TextField(new Rect(panel.x+68,controls,Mathf.Max(100,panel.width*.27f),26),marketSearch);
   float sx=panel.x+panel.width*.40f;
   if(GUI.Button(new Rect(sx,controls,panel.width*.25f,26),MarketSortNames[marketSort]+" (change)",buttonStyle))marketSort=(marketSort+1)%4;
   marketAffordable=GUI.Toggle(new Rect(panel.x+panel.width*.68f,controls,140,26),marketAffordable,"Affordable only");
   float py=controls+34;GUI.Label(new Rect(panel.x+16,py,95,25),"Price range",smallStyle);
   marketMin=Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(panel.x+112,py+8,105,18),marketMin,0,10));
   marketMax=Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(panel.x+235,py+8,105,18),marketMax,marketMin,10));
   marketMax=Mathf.Max(marketMin,marketMax);
   GUI.Label(new Rect(panel.x+352,py,120,24),marketMin+"-"+marketMax+" gold",smallStyle);
   if(GUI.Button(new Rect(panel.x+490,py,100,26),"Clear filters",buttonStyle)){marketSearch="";marketMin=0;marketMax=10;marketAffordable=false;marketTab=0;marketSort=0;}
   string key=marketTab+"|"+marketSearch+"|"+marketMin+"|"+marketMax+"|"+marketAffordable+"|"+gold+"|"+marketSort;
   if(source!=marketSource || key!=marketQueryKey){marketSource=source;marketQueryKey=key;marketFiltered=DeckMarketQuery.Filter(source,marketTab,marketSearch,marketMin,marketMax,marketAffordable,gold,marketSort);marketScroll=Vector2.zero;}
   bool preview=panel.width>=760;
   var area=new Rect(panel.x+14,py+36,preview?panel.width-300:panel.width-28,panel.yMax-py-78);
   GUI.Label(new Rect(area.x,panel.yMax-32,area.width,24),marketFiltered.Length+" card choices  |  "+marketFiltered.Sum(c=>c.remaining)+" copies  |  Click a row to inspect",smallStyle);
   marketScroll=GUI.BeginScrollView(area,marketScroll,new Rect(0,0,area.width-22,Mathf.Max(area.height,marketFiltered.Length*68)));
   for(int i=0;i<marketFiltered.Length;i++) {
    var item=marketFiltered[i];var r=new Rect(0,i*68,area.width-25,64);
    CardFinish.Round(r,item.card_id==marketSelected?new Color(.10f,.19f,.26f):i%2==0?new Color(.065f,.08f,.11f):new Color(.04f,.055f,.085f));
    var tex=CardFaces.Art(item.card_id);if(tex!=null)GUI.DrawTexture(new Rect(6,r.y+6,44,52),tex,ScaleMode.ScaleAndCrop);
    var action=liveActions.FirstOrDefault(a=>a.type=="buy" && a.card_id==item.card_id);
    GUI.Label(new Rect(58,r.y+3,r.width-175,24),item.name,labelStyle);
    string reason=item.bought>0?"Category purchased this turn":item.price>gold?"Need "+(item.price-gold)+" more gold":action==null?"Wait for your purchase phase":"Ready to buy";
    GUI.Label(new Rect(58,r.y+28,r.width-175,31),item.remaining+" left  |  "+item.price+" gold\n"+reason,smallStyle);
    if(r.Contains(Event.current.mousePosition)){if(!preview)hoverCardId=item.card_id;if(Event.current.type==EventType.MouseDown && Event.current.mousePosition.x<r.width-117){marketSelected=item.card_id;Event.current.Use();}}
    bool enabled=GUI.enabled;GUI.enabled=!paused && !waiting && !MatchOver && action!=null;
    if(GUI.Button(new Rect(r.width-110,r.y+15,102,34),"Buy  |  "+item.price,buttonStyle)){marketSelected=item.card_id;ActLive(action);}
    GUI.enabled=enabled;
   }
   if(marketFiltered.Length==0)GUI.Label(new Rect(12,12,area.width-36,70),"No cards match these filters.\nTry a wider price range or clear filters.",labelStyle);
   GUI.EndScrollView();
   if(preview){var chosen=marketFiltered.FirstOrDefault(c=>c.card_id==marketSelected)??marketFiltered.FirstOrDefault();if(chosen!=null){float cw=238,ch=Mathf.Min(area.height,356);CardFaces.Draw(new Rect(panel.xMax-268,area.y,cw,ch),chosen.card_id,false);}}
  }
 }
}
