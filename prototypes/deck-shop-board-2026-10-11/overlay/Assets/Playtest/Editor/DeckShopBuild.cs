using System;
using System.IO;
using System.Linq;
using UnityEngine;
using InfiniteConquest.Playtest;
public static class DeckShopBuild {
 static int checks;
 static void Check(bool ok,string label){if(!ok)throw new Exception("SHOP FAIL "+label);checks++;Debug.Log("SHOP PASS "+label);}
 [Serializable] class Entry {public string id;public int copies;}
 [Serializable] class Presets {public Entry[] ZEUS,POSEIDON;}
 public static void Build(){
  var rows=new[]{new BridgeMarketCard{card_id="a",name="Alpha",category="LAND",price=1,remaining=2},new BridgeMarketCard{card_id="b",name="Beta",category="CHARACTER",price=3,remaining=4},new BridgeMarketCard{card_id="c",name="Gamma",category="SPELL",price=2,remaining=1},new BridgeMarketCard{card_id="d",name="Delta",category="STRUCTURE",price=2,remaining=2,bought=1},new BridgeMarketCard{card_id="e",name="Empty",category="LAND",price=1,remaining=0}};
  Check(DeckMarketQuery.Filter(rows,0,"",0,10,false,0,0).Length==4,"sold out omitted");
  for(int tab=1;tab<=4;tab++)Check(DeckMarketQuery.Filter(rows,tab,"",0,10,false,0,0).Single().category==DeckMarketQuery.Categories[tab],"category "+tab);
  Check(DeckMarketQuery.Filter(rows,0,"  bEt  ",0,10,false,0,0).Single().card_id=="b","trimmed case insensitive search");
  Check(DeckMarketQuery.Filter(rows,0,"",2,2,false,0,0).Length==2,"inclusive price bounds");
  Check(DeckMarketQuery.Filter(rows,0,"",0,10,true,1,0).Single().card_id=="a","affordable filter exact boundary");
  Check(DeckMarketQuery.Filter(rows,0,"",0,10,true,0,0).Length==0,"zero gold empty result");
  Check(DeckMarketQuery.Filter(rows,0,"",0,10,false,0,0).First().card_id=="a","ascending price");
  Check(DeckMarketQuery.Filter(rows,0,"",0,10,false,0,1).First().card_id=="b","descending price");
  Check(DeckMarketQuery.Filter(rows,0,"",0,10,false,0,2).Select(c=>c.card_id).SequenceEqual(new[]{"a","b","d","c"}),"alphabetical");
  Check(DeckMarketQuery.Filter(rows,0,"",0,10,false,0,3).First().card_id=="b","remaining copies");
  Check(DeckMarketQuery.Filter(null,0,"",0,10,false,0,0).Length==0,"empty deck");
  Check(rows[3].bought==1 && rows[0].remaining==2,"query does not change game state");
  var previous=Enumerable.Range(0,10).Select(i=>"id"+i).ToArray();var custom=previous.Concat(new[]{"extra"}).ToArray();
  Check(!DeckMarketQuery.KeepSavedDeck(previous.Reverse().ToArray(),previous,custom),"old preset upgrades regardless of order");
  Check(DeckMarketQuery.KeepSavedDeck(custom,previous,custom),"valid custom deck retained");
  Check(!DeckMarketQuery.KeepSavedDeck(custom,previous,previous),"unknown or other faction card rejected");
  Check(!DeckMarketQuery.KeepSavedDeck(null,previous,custom),"null saved deck defaults");
  Check(!DeckMarketQuery.KeepSavedDeck(previous.Concat(Enumerable.Repeat("id0",4)).ToArray(),previous,custom),"five copies rejected on restore");
  Check(!DeckMarketQuery.KeepSavedDeck(previous.Take(9).ToArray(),previous,custom),"too few distinct cards rejected on restore");
  var presets=JsonUtility.FromJson<Presets>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"NativeRules/faction-starters.json")));
  for(int seat=0;seat<2;seat++){
   var p=seat==0?presets.ZEUS:presets.POSEIDON;string faction=seat==0?"ZEUS":"POSEIDON";
   Check(p.Sum(c=>c.copies)==40 && p.Select(c=>c.id).Distinct().Count()==20 && p.All(c=>c.copies==2),"40 cards/20 distinct/two copies "+faction);
   Check(p.All(c=>CardFaces.Get(c.id).faction==faction),"all definitions same faction "+faction);
   foreach(var category in DeckMarketQuery.Categories.Skip(1))Check(p.Where(c=>CardFaces.Get(c.id).type==category).Sum(c=>c.copies)==(category=="CHARACTER"?14:category=="SPELL"?6:10),"strategic curve "+faction+category);
   Check(p.Any(c=>CardFaces.Get(c.id).type=="CHARACTER" && CardFaces.PurchasePrice(CardFaces.Get(c.id))==1),"first turn land + two gold structure + one gold unit path "+faction);
  }
  var legacy=new CardFace{type="STRUCTURE",rulesText="Turn 6. Generates 2 GP. Heals your Capital when deployed."};
  Check(CardFaces.DisplayRules(legacy,true)=="Heals your Capital when deployed.","old unlock/income text stripped in purchase mode");
  Check(CardFaces.DisplayRules(legacy,false)==legacy.rulesText,"legacy display retained");
  Check(CardFaces.DisplayRules(new CardFace{type="CHARACTER",rulesText="BLINK",keywords=new[]{"BLINK"}},true)=="","keyword printed once in header");
  Check(CardFaces.DisplayRules(new CardFace{type="CHARACTER",rulesText="Mole. Heal your Capital 3.",keywords=new[]{"MOLE"}},true)=="Heal your Capital 3.","keyword cleanup preserves attached effect");
  Directory.CreateDirectory("Build");File.WriteAllText("Build/deck-shop-validation.json","{\"passed\":true,\"checks\":"+checks+"}");
  CombinedBuild.Build();
 }
}
