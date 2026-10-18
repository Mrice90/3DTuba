using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace InfiniteConquest.Playtest {
 public sealed partial class PlaytestGame {
  IEnumerator CaptureDeckShop() {
   string dir=Arg("-shopPreview");Directory.CreateDirectory(dir);
   float end=Time.realtimeSinceStartup+25;
   while((waiting || liveState==null || liveActions.Length==0) && Time.realtimeSinceStartup<end)yield return null;
   if(liveState==null || waiting) {File.WriteAllText(Arg("-playtestResult"),"{\"passed\":false}");Application.Quit(1);yield break;}
   marketOpen=true;sfx.Muted=true;
   for(int stage=0;stage<5;stage++) {
    marketTab=stage==1?1:stage==2?3:0;marketAffordable=stage==2;marketSearch=stage==3?"no matching card":"";marketSort=stage==2?1:0;
    if(stage==4){marketSearch="";var buy=liveActions.FirstOrDefault(a=>a.type=="buy" && a.card_id=="zeus_land_empyrean_current");if(buy!=null){ActLive(buy);while(waiting)yield return null;}}
    yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();
    ScreenCapture.CaptureScreenshot(Path.Combine(dir,new[]{"all","lands","affordable-characters","empty","after-purchase"}[stage]+".png"));
    yield return new WaitForSecondsRealtime(.3f);
   }
   bool passed=liveState.market.Any(c=>c.category=="LAND" && c.bought==1);
   File.WriteAllText(Arg("-playtestResult"),"{\"passed\":"+passed.ToString().ToLowerInvariant()+",\"actualPurchase\":"+passed.ToString().ToLowerInvariant()+"}");Application.Quit(passed?0:1);
  }
 }
}
