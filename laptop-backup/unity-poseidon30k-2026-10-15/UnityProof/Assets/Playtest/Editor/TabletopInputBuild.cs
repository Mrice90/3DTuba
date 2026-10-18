using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;
public static class TabletopInputBuild {
    public static void Build() {
        var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Playtest/Resources/Environment/ConquestStone.png");
        if(importer==null)throw new Exception("Missing tabletop texture");
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.wrapMode=TextureWrapMode.Repeat;
        importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.maxTextureSize=2048;importer.SaveAndReimport();
        if(Resources.Load<Texture2D>("Environment/ConquestStone")==null)throw new Exception("Tabletop material not loadable");
        int checks=1;
        var host=new GameObject("Tabletop validation");
        try {
            var factory=new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"),Shader.Find("Universal Render Pipeline/Unlit"),Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            var table=BattleTabletop.Build(host.transform,factory,6);
            if(table.GetComponentsInChildren<Collider>().Length!=0)throw new Exception("Decoration intercepts board input");checks++;
            foreach(var size in new[]{new Vector2(1280,720),new Vector2(960,640)}){
                var inspect=PlaytestGame.CardInspectRect(size.x,size.y);
                if(inspect.xMax>=270||inspect.yMax>size.y-60)throw new Exception("Card inspection covers hand or footer");checks++;
            }
        }finally{UnityEngine.Object.DestroyImmediate(host);}
        Directory.CreateDirectory("Build");File.WriteAllText("Build/tabletop-input-validation.json","{\"passed\":true,\"checks\":"+checks+"}");
        GroundSkinsBuild.Build();
    }
}
