using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;
public static class CosmeticsBuild {
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception("COSMETICS FAIL "+message);checks++;}
    public static void Build(){
        var original=CoinSkin.Load("Default");
        string[] ids={"Moonsteel","AbyssalBronze","ObsidianRose"};
        string[] labels={"Moonsteel","Abyssal bronze","Obsidian rose"};
        Color[] tint={new Color(.62f,.82f,1f),new Color(.3f,.95f,.8f),new Color(.74f,.42f,.92f)};
        for(int i=0;i<ids.Length;i++){
            string path="Assets/Playtest/Resources/CoinSkins/"+ids[i]+".asset";
            var skin=AssetDatabase.LoadAssetAtPath<CoinSkin>(path);
            if(skin==null){skin=ScriptableObject.CreateInstance<CoinSkin>();AssetDatabase.CreateAsset(skin,path);}
            skin.displayName=labels[i];skin.front=original.front;skin.back=original.back;skin.rimTexture=original.rimTexture;
            skin.faceTint=tint[i];skin.faceMetallic=.6f;skin.faceSmoothness=.72f;skin.rimColor=tint[i];skin.rimMetallic=.85f;skin.rimSmoothness=.7f;skin.emission=tint[i]*.2f;EditorUtility.SetDirty(skin);
        }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        string coin=CoinSkin.SelectedName;int background=TabletopPalette.Selected;
        for(int viewer=0;viewer<2;viewer++)foreach(string own in BoardSkin.Presets)foreach(string opponent in BoardSkin.Presets){
            MatchCosmetics.SetForViewer(viewer,own,opponent);
            Check(BoardSkin.Selected(viewer).name==own,"own preset stays with seat");
            Check(BoardSkin.Selected(1-viewer).name==opponent,"opponent preset stays with seat");
            Check(CoinSkin.SelectedName==coin&&TabletopPalette.Selected==background,"opponent cannot replace local coin/background");
        }
        MatchCosmetics.SetSeats("../bad",null);Check(BoardSkin.Selected(0).name=="Stormforge"&&BoardSkin.Selected(1).name=="Tideglass","unknown IDs fall back safely");
        MatchCosmetics.Clear();
        foreach(var skin in Resources.LoadAll<CoinSkin>("CoinSkins"))Check(skin.front!=null&&skin.back!=null,"both faces exist: "+skin.name);
        Check(TabletopPalette.Names.Length==4&&TabletopPalette.Colors.Length==4&&TabletopPalette.Metals.Length==4,"four background palettes");
        Directory.CreateDirectory("Build");File.WriteAllText("Build/cosmetics-validation.json","{\"passed\":true,\"checks\":"+checks+",\"networkExchangeTested\":false}");
        TabletopInputBuild.Build();
    }
}
