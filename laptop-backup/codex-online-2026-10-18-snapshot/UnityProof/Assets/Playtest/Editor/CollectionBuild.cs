using System;
using System.IO;
using System.Linq;
using InfiniteConquest.Playtest;
public static class CollectionBuild {
    public static void Build(){
        int checks=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("COLLECTION FAIL "+message);checks++;};
        var all=CollectionCards.Query("","","",false);check(all.Length==139,"full playable catalogue");check(all.Select(c=>c.id).Distinct().Count()==139,"no duplicates");
        check(CardFaces.CleanText("turn \u00e2\u20ac\u201d 3 GP")=="turn - 3 GP","legacy corrupted dash repaired");
        foreach(var c in all){check(CardFaces.Art(c.id)!=null,"art "+c.id);check(CollectionCards.Query(c.faction,c.type,c.name,false).Any(q=>q.id==c.id),"reachable through filters "+c.id);}
        foreach(string faction in new[]{"ZEUS","POSEIDON"})check(CollectionCards.Query(faction,"","",false).All(c=>c.faction==faction),"faction filtering");
        var price=CollectionCards.Query("","","",true);check(price.Zip(price.Skip(1),(a,b)=>CardFaces.PurchasePrice(a)<=CardFaces.PurchasePrice(b)).All(x=>x),"price order");
        check(CollectionCards.Query("","","zzzz-no-card-match",false).Length==0,"empty search");
        check(CollectionCards.Query("","","  ",false).Length==139,"blank trimmed search");
        Directory.CreateDirectory("Build");File.WriteAllText("Build/collection-validation.json","{\"passed\":true,\"checks\":"+checks+",\"cards\":139}");
        CosmeticsBuild.Build();
    }
}
