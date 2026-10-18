using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using InfiniteConquest.Proof;
namespace InfiniteConquest.Playtest {
    public sealed class ReleasePreviewDriver : MonoBehaviour {}
    public sealed partial class PlaytestGame {
        int releaseArtPage=-1;
        IEnumerator CaptureReleasePreview(string dir) {
            Directory.CreateDirectory(dir);sfx.Muted=true;
            int total=0,missing=0;
            foreach(var card in CardFaces.All) {total++;if(CardFaces.Art(card.id)==null)missing++;}
            yield return new WaitForSecondsRealtime(.8f);
            yield return Shot(dir,"01-main-menu.png");
            menuScreen=Screen2.PlaySetup;setupSeat=0;
            yield return new WaitForSecondsRealtime(.2f);yield return Shot(dir,"02-allegiance.png");
            menuScreen=Screen2.HowTo;
            yield return new WaitForSecondsRealtime(.2f);yield return Shot(dir,"03-how-to.png");
            menuScreen=Screen2.Settings;
            yield return new WaitForSecondsRealtime(.2f);yield return Shot(dir,"04-settings.png");
            menuScreen=Screen2.BoardAppearance;
            yield return new WaitForSecondsRealtime(.4f);yield return Shot(dir,"04-board-appearance.png");
            rig.ScreenShiftPx=0;menuScreen=Screen2.Main;
            for(int page=0;page<4;page++) {
                releaseArtPage=page;yield return new WaitForSecondsRealtime(.2f);
                yield return Shot(dir,"05-art-"+page+".png");
            }
            releaseArtPage=-1;
            for(int seat=0;seat<2;seat++) {
                setupSeat=seat;BeginCapitalPlacement(713);
                yield return new WaitForSecondsRealtime(.6f);
                yield return Shot(dir,"06-placement-seat"+seat+".png");
            }
            File.WriteAllText(Path.Combine(dir,"result.json"),"{\"passed\":"+(missing==0).ToString().ToLowerInvariant()+",\"loadedArt\":"+total+",\"missingArt\":"+missing+",\"boardCells\":"+BoardConfig.CellCount+"}");
            Application.Quit(missing==0?0:1);
        }
        void DrawReleaseArtPreview() {
            FrontStyles();FrontFill(new Rect(0,0,Screen.width,Screen.height),new Color(.025f,.04f,.065f));
            string faction=releaseArtPage<2?"zeus":"poseidon";
            string type=releaseArtPage%2==0?"land":"structure";
            GUI.Label(new Rect(32,24,Screen.width-64,34),faction.ToUpperInvariant()+"  /  "+type.ToUpperInvariant()+" COLLECTION",frontKicker);
            CardFaces.PurchaseMode=true;
            float width=Mathf.Min((Screen.width-104)/5f,(Screen.height-130)/1.5f),gap=16;
            float start=(Screen.width-width*5-gap*4)/2;
            for(int i=1;i<=5;i++)CardFaces.Draw(new Rect(start+(i-1)*(width+gap),90,width,width*1.5f),faction+"_tutor_"+type+"_"+i,false);
        }
    }
}
