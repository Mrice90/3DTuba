using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        bool deckEditorOpen;Vector2 deckEditorScroll;
        readonly Dictionary<int,List<string>> prototypeDecks=new Dictionary<int,List<string>>();
        string[] SelectedPrototypeDeck(int seat) {
            if(!prototypeDecks.TryGetValue(seat,out var selected)) {
                // Engine and UI share the exact shipped preset; no duplicate handwritten list.
                string text=File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"NativeRules/faction-starters.json"));
                var preset=JsonUtility.FromJson<StarterDecks>(text);
                var defaults=(seat==0?preset.ZEUS:preset.POSEIDON).SelectMany(c=>Enumerable.Repeat(c.id,c.copies)).ToList();
                selected=defaults;
                string saved=PlayerPrefs.GetString("ic.purchase.deck."+seat,"");
                if(!string.IsNullOrEmpty(saved)) {
                    try {
                    var restore=JsonUtility.FromJson<SavedDeck>(saved)?.cards;
                        var oldFile=JsonUtility.FromJson<StarterDecks>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"NativeRules/previous-starters.json")));
                        var old=(seat==0?oldFile.ZEUS:oldFile.POSEIDON).SelectMany(c=>Enumerable.Repeat(c.id,c.copies)).ToArray();
                        var allowed=CardFaces.All.Where(c=>c.faction==(seat==0?"ZEUS":"POSEIDON") && c.type!="CAPITAL").Select(c=>c.id).ToArray();
                        if(DeckMarketQuery.KeepSavedDeck(restore,old,allowed))selected=restore.ToList();
                    } catch(ArgumentException) { Debug.LogWarning("Invalid saved deck; using the strategy preset."); }
                }
                prototypeDecks[seat]=selected;
            }
            return selected.ToArray();
        }
        string PrototypeStrategy(int seat) => seat==0 ? "Zeus: ranged pressure. Grow income, build firing lanes, then threaten the Capital." : "Poseidon: durable defense. Grow income, protect ranged units, and repair your front line.";
        [Serializable] sealed class StarterEntry {public string id;public int copies;}
        [Serializable] sealed class StarterDecks {public StarterEntry[] ZEUS,POSEIDON;}
        [Serializable] sealed class SavedDeck {public string[] cards;}
        void DrawDeckEditor() {
            SelectedPrototypeDeck(setupSeat);
            var selected=prototypeDecks[setupSeat];
            var panel=new Rect(Screen.width*.12f,70,Screen.width*.76f,Screen.height-140);
            CardFinish.Round(panel,new Color(.025f,.035f,.055f,.99f));GUI.Box(panel,"",panelStyle);
            GUI.Label(new Rect(panel.x+16,panel.y+12,panel.width-32,30),"Choose your deck  |  "+selected.Count+" / 40 cards",titleStyle);
            GUI.Label(new Rect(panel.x+16,panel.y+46,panel.width-32,25),PrototypeStrategy(setupSeat),smallStyle);
            var choices=CardFaces.All.Where(c=>c.faction==(setupSeat==0?"ZEUS":"POSEIDON") && c.type!="CAPITAL").OrderBy(c=>c.type).ThenBy(c=>c.name).ToArray();
            GUI.Label(new Rect(panel.x+16,panel.y+73,panel.width-32,25),"Maximum 4 copies per card | At least 10 different cards | Only selected cards can be bought",smallStyle);
            var area=new Rect(panel.x+14,panel.y+106,panel.width-28,panel.height-174);
            deckEditorScroll=GUI.BeginScrollView(area,deckEditorScroll,new Rect(0,0,area.width-24,choices.Length*38));
            for(int i=0;i<choices.Length;i++) {
                var card=choices[i];int count=selected.Count(id=>id==card.id);float y=i*38;
                GUI.Label(new Rect(4,y+4,area.width-200,28),card.name+" | "+card.type+" | "+CardFaces.PurchasePrice(card)+" gold",labelStyle);
                bool prior=GUI.enabled;GUI.enabled=count>0;
                if(GUI.Button(new Rect(area.width-181,y,36,32),"-",buttonStyle))selected.Remove(card.id);
                GUI.enabled=prior;GUI.Label(new Rect(area.width-136,y+4,32,25),count.ToString(),labelStyle);
                GUI.enabled=selected.Count<40 && count<4;
                if(GUI.Button(new Rect(area.width-101,y,36,32),"+",buttonStyle))selected.Add(card.id);
                GUI.enabled=prior;
            }
            GUI.EndScrollView();
            bool enabled=GUI.enabled;GUI.enabled=selected.Count>0 && selected.Count<=40 && selected.Distinct().Count()>=10;
            if(GUI.Button(new Rect(panel.x+16,panel.yMax-54,180,36),"Use this deck",buttonStyle)) { PlayerPrefs.SetString("ic.purchase.deck."+setupSeat,JsonUtility.ToJson(new SavedDeck{cards=selected.ToArray()}));PlayerPrefs.Save();deckEditorOpen=false; }
            GUI.enabled=enabled;
            if(GUI.Button(new Rect(panel.x+210,panel.yMax-54,180,36),"Use strategy preset",buttonStyle)){PlayerPrefs.DeleteKey("ic.purchase.deck."+setupSeat);prototypeDecks.Remove(setupSeat);SelectedPrototypeDeck(setupSeat);}
        }
    }
}
