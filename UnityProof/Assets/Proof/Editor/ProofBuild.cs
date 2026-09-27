using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using InfiniteConquest.Proof;

public static class ProofBuild {
    [Serializable] class Fixture { public string pinned_commit; public Case[] cases; }
    [Serializable] class Case { public string id; public Expected expected; }
    [Serializable] class Expected { public bool accepted; public int movementSpent; public Position position; }
    [Serializable] class Position { public int x,y; }
    static void Check(bool value,string name) { if(!value) throw new Exception("FAILED: "+name); Debug.Log("PASS: "+name); }
    public static void Validate() {
        var fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText("Assets/Proof/movement-fixture.json"));
        Check(fixture.pinned_commit=="992bc95c7164416ea0a25a4ce120f6ec0a0a167a","fixture provenance");
        Check(fixture.cases.Length==2,"two pinned cases");
        foreach(var c in fixture.cases) {
            var s=new MovementState();s.Reset(c.id=="illegal-blocked-by-enemy-structure");
            Check(s.TryMove(1,0)==c.expected.accepted,c.id+" acceptance");
            Check(s.X==c.expected.position.x && s.Y==c.expected.position.y && s.Spent==c.expected.movementSpent,c.id+" state");
        }
        var m=new MovementState();m.Reset(false);
        Check(!m.TryMove(-1,0)&&!m.TryMove(4,0)&&!m.TryMove(0,6),"out of bounds rejected");
        Check(!m.TryMove(0,0)&&!m.TryMove(2,0)&&m.Spent==0,"invalid moves do not spend");
        Check(m.TryMove(1,1)&&m.Spent==1,"diagonal costs one");
        Check(!m.TryMove(2,1)&&m.X==1&&m.Y==1,"budget exhausted");
        m.Reset(true);Check(m.X==0&&m.Y==0&&m.Spent==0&&!m.CanMove(1,0),"reset blocked case");
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/validation.json","{\"passed\":true,\"assertions\":11,\"scope\":\"AI-036 two fixture cases plus boundary checks\"}");
    }
    [MenuItem("Infinite Conquest/Build movement proof")]
    public static void Build() {
        Validate();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var proof=new GameObject("Movement proof").AddComponent<MovementProof>();
        proof.ProofShader=Shader.Find("Universal Render Pipeline/Lit");
        Check(proof.ProofShader!=null,"render shader included by scene reference");
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/MovementProof.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/MovementProof.unity",true)};
        PlayerSettings.defaultScreenWidth=1280;
        PlayerSettings.defaultScreenHeight=800;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{"Assets/Scenes/MovementProof.unity"},
            locationPathName="Build/Windows/InfiniteConquestProof.exe",
            target=BuildTarget.StandaloneWindows64, options=BuildOptions.None
        });
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Proof build failed: "+report.summary.result);
        Debug.Log("PROOF_BUILD PASS");
    }
}

