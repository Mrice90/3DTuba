using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using InfiniteConquest.Native;
using InfiniteConquest.RulesCore.Core;
using InfiniteConquest.RulesCore.Data;
if(args[0]=="--geometry") {
    int checks=0;
    void Check(bool value,string message) {if(!value)throw new Exception(message);checks++;}
    var cells=new BoardState().Positions();
    Check(cells.Count==37,"37 playable cells");
    Check(cells.Count(c=>c.IsOnPlayerSide(0))==15 && cells.Count(c=>c.IsOnPlayerSide(1))==15,"15 home cells each");
    Check(cells.Count(c=>!c.IsOnPlayerSide(0)&&!c.IsOnPlayerSide(1))==7,"7 neutral cells");
    foreach(var a in cells) {
      var neighbors=BoardGeometry.HEX.Neighbors(a);
      Check(neighbors.Count>=3&&neighbors.Count<=6,"edge degrees");
      foreach(var b in cells) {
        Check(BoardGeometry.HEX.Distance(a,b)==BoardGeometry.HEX.Distance(b,a),"symmetric distance");
        Check(neighbors.Contains(b)==(BoardGeometry.HEX.Distance(a,b)==1),"neighbors");
        foreach(var n in new[]{.000001,-.000001}) {
          var trace=BoardGeometry.HEX.HexTrace(a,b,n);
          Check(trace.SequenceEqual(BoardGeometry.HEX.HexTrace(b,a,n)),"symmetric sight trace");
          Check(trace.All(c=>cells.Contains(c)),"sight stays inside board");
          Check(trace.Count==Math.Max(0,BoardGeometry.HEX.Distance(a,b)-1),"unbroken sight trace");
        }
      }
    }
    for(int y=-1;y<=7;y++)for(int x=-1;x<=7;x++) {
      bool legal=BoardPosition.Contains(x,y),constructed=false;
      try {new BoardPosition(x,y);constructed=true;}catch(ArgumentException){}
      Check(legal==constructed,"masked coordinates rejected");
    }
    Console.WriteLine(JsonSerializer.Serialize(new {passed=true,checks,cells=cells.Count}));return;
}
if(args[0]=="--export-faces") {
    var defs=Directory.GetFiles(Path.Combine(args[1],"cards"),"*.json").SelectMany(f=>CardCatalog.LoadFromFile(f).Definitions()).Concat(FactionTutorExpansion.Cards()).ToArray();
    var policy=new SummonCapacityPolicy(File.ReadAllLines(Path.Combine(args[1],"structure-slots.properties")).Where(l=>l.Trim().Length>0&&!l.TrimStart().StartsWith("#")).Select(l=>l.Split(new[]{'='},2)).ToDictionary(p=>p[0].Trim(),p=>p[1].Trim()));
    File.WriteAllText(args[2],JsonSerializer.Serialize(defs.Select(c=>new {
        id=c.Id,name=c.Name,type=c.Type.ToString(),faction=c.Faction,cost=c.Cost,developmentGoldCost=c.DevelopmentGoldCost,
        attack=c.Attack,defense=c.Defense,range=c.Range,movement=c.Movement,hitPoints=c.HitPoints,gpGeneration=c.Income(),purchasePrice=PurchaseRules.Price(c),
        slotSupply=policy.SlotSupply(c),slotCost=policy.SlotCost(c),keywords=c.Keywords.Select(k=>k.ToString()).ToArray(),
        drawNotes=c.Abilities.Where(a=>a.Effect==AbilityEffectType.DRAW_CARD || a.Effect==AbilityEffectType.DRAW_CHARACTER || a.Effect==AbilityEffectType.DRAW_STRUCTURE).Select(a=>"Draw "+a.Amount+" random "+(a.Effect==AbilityEffectType.DRAW_CARD?"card from the full remaining deck":a.Effect==AbilityEffectType.DRAW_CHARACTER?"Character from the remaining deck":"Structure from the remaining deck")+" (free; bypasses buy limit).").ToArray()
    })));
    return;
}
var session=new NativeRulesSession(args[0]);
string line;
while((line=Console.ReadLine())!=null) {Console.WriteLine(session.Handle(line));Console.Out.Flush();}
