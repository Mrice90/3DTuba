using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        IEnumerator CaptureCosmetics(string dir){
            Directory.CreateDirectory(dir);sfx.Muted=true;
            string[] keys={"ic.tabletop","ic.coinSkin","ic.boardSkin.0","ic.boardSkin.1"};
            bool[] exists=keys.Select(PlayerPrefs.HasKey).ToArray();
            string[] saved=keys.Select(k=>k=="ic.tabletop"?PlayerPrefs.GetInt(k).ToString():PlayerPrefs.GetString(k)).ToArray();
            int checks=0;
            try{
                menuScreen=Screen2.BoardAppearance;
                for(int i=0;i<4;i++){
                    TabletopPalette.Select(i);board.GetComponentInChildren<TabletopAppearance>()?.Apply();
                    BoardSkin.Select(0,BoardSkin.Presets[i]);BoardSkin.Select(1,BoardSkin.Presets[(i+1)%4]);board.ApplySkins();
                    yield return new WaitForSecondsRealtime(.4f);yield return Shot(dir,"style-"+i+".png");checks++;
                }
                foreach(var skin in Resources.LoadAll<CoinSkin>("CoinSkins").OrderBy(s=>s.name)){
                    CoinSkin.Select(skin.name);yield return new WaitForSecondsRealtime(.1f);yield return Shot(dir,"coin-menu-"+skin.name+".png");
                    groundPreview=true;groundAppearance=false;rig.ScreenShiftPx=0;rig.ResetView();rig.Snap();
                    var flip=CoinFlip3D.Play(1,CoinSkin.Selected,LitShader,null,boardPosition:InfiniteConquest.Proof.BoardLayout.CellCenter(3,3)+Vector3.up*InfiniteConquest.Proof.BoardLayout.TileTop);
                    yield return new WaitForSecondsRealtime(.75f);yield return Shot(dir,"coin-flip-"+skin.name+".png");
                    while(flip!=null&&!flip.Revealed)yield return null;
                    if(flip==null)throw new System.Exception("Coin ended before capture");
                    if(flip.FaceUpSeat!=1)throw new System.Exception("Cosmetic changed winner");checks++;
                    yield return Shot(dir,"coin-landed-"+skin.name+".png");
                    while(flip!=null&&!flip.Done)yield return null;
                    groundPreview=false;
                }
            }finally{
                MatchCosmetics.Clear();
                for(int i=0;i<keys.Length;i++){if(!exists[i])PlayerPrefs.DeleteKey(keys[i]);else if(i==0)PlayerPrefs.SetInt(keys[i],int.Parse(saved[i]));else PlayerPrefs.SetString(keys[i],saved[i]);}
                PlayerPrefs.Save();
            }
            File.WriteAllText(Path.Combine(dir,"result.json"),"{\"passed\":true,\"checks\":"+checks+",\"backgrounds\":4,\"coins\":5,\"networkExchangeTested\":false}");Application.Quit();
        }
    }
}
