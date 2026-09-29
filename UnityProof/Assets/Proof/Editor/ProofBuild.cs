using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using InfiniteConquest.Proof;

public static class ProofBuild {
    const string ScenePath="Assets/Scenes/MovementProof.unity";
    [Serializable] class Fixture { public string pinned_commit; public Case[] cases; }
    [Serializable] class Case { public string id; public Expected expected; }
    [Serializable] class Expected { public bool accepted; public int movementSpent; public Position position; }
    [Serializable] class Position { public int x,y; }
    static int assertions;
    static void Check(bool value,string name) { if(!value) throw new Exception("FAILED: "+name); assertions++; Debug.Log("PASS: "+name); }
    public static void Validate() {
        assertions=0;
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
        // HEX geometry (MatchRules.hex): from even row 0, (1,1) is two steps away and (0,1) is a neighbour.
        Check(!m.CanMove(1,1)&&MovementState.HexDistance(0,0,1,1)==2,"hex: (1,1) is not adjacent to (0,0)");
        Check(MovementState.HexDistance(1,2,0,2)==1&&MovementState.HexDistance(1,2,0,1)==1&&MovementState.HexDistance(1,2,2,1)==2
            &&MovementState.HexDistance(1,3,2,2)==1&&MovementState.HexDistance(1,3,0,2)==2,"hex: odd-row offset neighbours");
        int interior=0; for(int x=0;x<MovementState.Width;x++) for(int y=0;y<MovementState.Height;y++) if(MovementState.HexDistance(1,2,x,y)==1) interior++;
        Check(interior==6,"hex: interior cell has six neighbours");
        Check(m.TryMove(0,1)&&m.Spent==1,"hex neighbour costs one");
        Check(!m.TryMove(1,1)&&m.X==0&&m.Y==1,"budget exhausted");
        m.Reset(true);Check(m.X==0&&m.Y==0&&m.Spent==0&&!m.CanMove(1,0),"reset blocked case");
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/validation.json","{\"passed\":true,\"assertions\":"+assertions+",\"scope\":\"AI-036 two fixture cases plus boundary and HEX geometry checks\"}");
    }
    [MenuItem("Infinite Conquest/Build movement proof")]
    public static void Build() {
        Validate();
        // Open the existing scene so hand-authored changes survive; generate it only when it is missing.
        var scene=File.Exists(ScenePath)
            ? EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single)
            : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var proof=UnityEngine.Object.FindFirstObjectByType<MovementProof>();
        if(proof==null) proof=new GameObject("Movement proof").AddComponent<MovementProof>();
        if(proof.ProofShader==null) proof.ProofShader=Shader.Find("Universal Render Pipeline/Lit");
        Check(proof.ProofShader!=null,"render shader included by scene reference");
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene,ScenePath);
        // Make sure the proof scene is in the build list without dropping any other scenes.
        var listed=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if(!listed.Exists(s=>s.path==ScenePath)) {
            listed.Insert(0,new EditorBuildSettingsScene(ScenePath,true));
            EditorBuildSettings.scenes=listed.ToArray();
        }
        PlayerSettings.defaultScreenWidth=1280;
        PlayerSettings.defaultScreenHeight=800;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes=new[]{ScenePath},
            locationPathName="Build/Windows/InfiniteConquestProof.exe",
            target=BuildTarget.StandaloneWindows64, options=BuildOptions.None
        });
        if(report.summary.result!=BuildResult.Succeeded) throw new Exception("Proof build failed: "+report.summary.result);
        Debug.Log("PROOF_BUILD PASS");
    }
}

