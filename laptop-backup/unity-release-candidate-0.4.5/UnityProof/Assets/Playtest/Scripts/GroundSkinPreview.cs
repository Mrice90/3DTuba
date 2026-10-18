using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using InfiniteConquest.Proof;
namespace InfiniteConquest.Playtest {
 public sealed partial class PlaytestGame {
  bool groundPreview, groundAppearance;
  void CaptureGroundCamera(string path) {
   var camera=Camera.allCameras.OrderByDescending(c=>c.depth).First();
   var rt=RenderTexture.GetTemporary(Screen.width,Screen.height,24,RenderTextureFormat.ARGB32);
   var old=RenderTexture.active;
   UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination=rt });
   RenderTexture.active=rt; var texture=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
   texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG());
   RenderTexture.active=old; RenderTexture.ReleaseTemporary(rt); Destroy(texture);
  }
  IEnumerator CaptureGround() {
   string dir=Arg("-groundPreview"); Directory.CreateDirectory(dir);
   groundPreview=true; playing=false; mode=Mode.Playback; sfx.Muted=true;
   var lands=PlaytestCatalog.All.Where(c=>c.type=="LAND"&&c.HasModel).ToArray();
   int landCells=0;
   foreach(var cell in BoardConfig.DrawnCells) if(cell.y>0 && cell.y<6) {
    int x=cell.x,y=cell.y;Spawn("preview-"+x+"-"+y,lands[(x+y)%lands.Length].id,y<3?0:1,x,y,false);landCells++;
   }
   for(int phase=0;phase<3;phase++) {
    groundAppearance=phase==2;
    rig.SetHome(Vector3.zero,phase==1?0:-24,phase==1?85:50,11.5f*BoardLayout.CameraScale,true);
    yield return new WaitForSecondsRealtime(.6f); yield return new WaitForEndOfFrame();
    CaptureGroundCamera(Path.Combine(dir,new[]{"connected-ground","aligned-overhead","appearance-board-preview"}[phase]+".png"));
    yield return new WaitForSecondsRealtime(.4f);
   }
   groundAppearance=false; rig.ScreenShiftPx=0;
   var landingCell=BoardConfig.DrawnCells.OrderBy(c=>BoardLayout.CellCenter(c.x,c.y).sqrMagnitude).First();
   // Empty the center for the actual opening presentation: matches flip before any land is placed.
   foreach(var p in pieces.Values.ToArray()) Despawn(p);
   var flip=CoinFlip3D.Play(1,CoinSkin.Load("CapitalArtTest"),LitShader,null,boardPosition:BoardLayout.CellCenter(landingCell.x,landingCell.y)+Vector3.up*BoardLayout.TileTop);
   for(int phase=0;phase<2;phase++) {
    yield return new WaitForSecondsRealtime(phase==0?.8f:1.9f); yield return new WaitForEndOfFrame();
    CaptureGroundCamera(Path.Combine(dir,phase==0?"coin-airborne.png":"coin-on-board.png")); yield return new WaitForSecondsRealtime(.2f);
   }
   while(!flip.Done) yield return null;
   File.WriteAllText(Path.Combine(dir,"result.json"),"{\"passed\":true,\"landCells\":"+landCells+",\"winner\":1}"); Application.Quit();
  }
 }
}
