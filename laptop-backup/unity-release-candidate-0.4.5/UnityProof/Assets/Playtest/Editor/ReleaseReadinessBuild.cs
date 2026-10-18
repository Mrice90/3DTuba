using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using InfiniteConquest.Playtest;

public static class ReleaseReadinessBuild {
    public static void Build() {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var host=new GameObject("Release hit testing");host.SetActive(false);
        int checks=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("RELEASE FAIL "+label);checks++;};
        try {
            var game=host.AddComponent<PlaytestGame>();var type=typeof(PlaytestGame);
            var visuals=(IDictionary)type.GetField("handVisuals",flags).GetValue(game);
            var visualType=type.GetNestedType("HandVisual",BindingFlags.NonPublic);
            var visual=Activator.CreateInstance(visualType,true);
            visualType.GetField("rect").SetValue(visual,new Rect(400,300,100,140));
            visuals.Add("test-card",visual);
            var hit=type.GetMethod("PointerOverPrintedHand",flags);
            Func<float,float,bool> at=(x,y)=>(bool)hit.Invoke(game,new object[]{new Vector2(x,y)});
            check(at(450,350),"printed card intercepts pointer");
            check(!at(350,350),"empty hand margin leaves board clickable");
            check(!at(510,350),"gap beside card leaves board clickable");
            check(!at(450,450),"below card leaves board clickable");
            check(at(450,350),"fresh pointer movement immediately intercepts card");
            visualType.GetField("removedAt").SetValue(visual,1f);
            check(!at(450,350),"removed card does not block board during exit animation");
            int total=0;
            foreach(var card in CardFaces.All){total++;check(CardFaces.Art(card.id)!=null,"art for "+card.id);}
            check(total==163,"complete current card-face roster");
        } finally {UnityEngine.Object.DestroyImmediate(host);}
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/release-readiness-validation.json","{\"passed\":true,\"checks\":"+checks+"}");
        Debug.Log("RELEASE_READINESS passed "+checks);
        AudioFollowupBuild.Build();
    }
}
