using System.Collections;
using System.IO;
using UnityEngine;
namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        bool cardPreviewCaptureStarted;
        void DrawCardFinishPreview() {
            CardFaces.PurchaseMode=true;
            float W=Screen.width,H=Screen.height;
            GUI.Box(new Rect(0,0,W,H),"",panelStyle);
            GUI.Label(new Rect(40,30,W-80,40),"INFINITE CONQUEST  |  Card finish preview",titleStyle);
            GUI.Label(new Rect(40,75,W-80,30),"Faction frames  |  printed text panels  |  existing artwork  |  free plays from hand",labelStyle);
            var ids=new[]{"zeus_cloudline_courier","poseidon_tidal_pump_station","zeus_olympian_cloudbank","poseidon_restorative_tide"};
            float width=Mathf.Clamp(W*.16f,190,260),height=width*1.5f,gap=28,start=(W-4*width-3*gap)/2;
            for(int i=0;i<4;i++) CardFaces.Draw(new Rect(start+i*(width+gap),140,width,height),ids[i],false,i==0);
            GUI.Label(new Rect(40,H-235,W-80,25),"Same straight hand row",labelStyle);
            for(int i=0;i<4;i++) CardFaces.Draw(new Rect(W/2-240+i*128,H-198,116,161),ids[i],true,i==0);
            if(!cardPreviewCaptureStarted) { cardPreviewCaptureStarted=true;StartCoroutine(CaptureCardFinish()); }
        }
        IEnumerator CaptureCardFinish() {
            yield return new WaitForSecondsRealtime(1);
            yield return new WaitForEndOfFrame();
            string path=Arg("-cardFinishPreview");Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(1);
            File.WriteAllText(Arg("-playtestResult"),"{\"passed\":true,\"preview\":true}");
            Application.Quit();
        }
    }
}
