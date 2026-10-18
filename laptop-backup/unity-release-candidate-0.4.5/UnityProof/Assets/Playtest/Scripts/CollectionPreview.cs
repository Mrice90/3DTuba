using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        IEnumerator CaptureCollection(string dir){
            Directory.CreateDirectory(dir);sfx.Muted=true;OpenCollection();
            yield return new WaitForSecondsRealtime(.3f);yield return Shot(dir,"all.png");
            collectionFaction="POSEIDON";collectionType="CHARACTER";RefreshCollection();
            yield return new WaitForSecondsRealtime(.2f);yield return Shot(dir,"poseidon-characters.png");
            collectionFaction=collectionType="";collectionSearch="line of sight";RefreshCollection();
            yield return new WaitForSecondsRealtime(.2f);yield return Shot(dir,"rules-search.png");
            collectionSearch="zzzz-no-card-match";RefreshCollection();
            yield return new WaitForSecondsRealtime(.2f);yield return Shot(dir,"empty.png");
            collectionSearch="";collectionByPrice=true;RefreshCollection();collectionPage=(collectionCards.Length-1)/8;
            collectionSelected=collectionCards.Last().id;
            yield return new WaitForSecondsRealtime(.2f);yield return Shot(dir,"last-page.png");
            File.WriteAllText(Path.Combine(dir,"result.json"),"{\"passed\":true,\"cards\":"+collectionCards.Length+",\"pages\":"+(collectionPage+1)+"}");Application.Quit();
        }
    }
}
