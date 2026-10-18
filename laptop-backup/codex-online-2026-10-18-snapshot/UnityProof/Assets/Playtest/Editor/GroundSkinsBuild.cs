using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Proof;
using InfiniteConquest.Playtest;

public static class GroundSkinsBuild {
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception("GROUND FAIL " + message); checks++; }
    public static void Build() {
        PlayerSettings.bundleVersion="0.4.5";
        const string dir = "Assets/Resources/BoardSkins";
        System.IO.Directory.CreateDirectory(dir);
        Color[] colors = { new Color(.12f,.18f,.26f), new Color(.08f,.3f,.34f), new Color(.55f,.58f,.62f), new Color(.13f,.09f,.20f) };
        Color[] edges = { new Color(.8f,.57f,.24f), new Color(.25f,.85f,.85f), new Color(.85f,.9f,1), new Color(.63f,.35f,.9f) };
        for(int i=0;i<4;i++) {
            string path=dir+"/"+BoardSkin.Presets[i]+".asset";
            var skin=AssetDatabase.LoadAssetAtPath<BoardSkin>(path);
            if(skin==null) { skin=ScriptableObject.CreateInstance<BoardSkin>(); AssetDatabase.CreateAsset(skin,path); }
            skin.displayName=BoardSkin.Presets[i]; skin.tileColor=colors[i]; skin.checkerColor=colors[i]*1.12f;
            skin.groundColor=colors[i]*1.3f; skin.edgeColor=edges[i]; skin.metallic=.35f; skin.smoothness=.4f;
            EditorUtility.SetDirty(skin);
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        foreach(var size in new[]{new Vector2(1280,720),new Vector2(960,640),new Vector2(800,480)}) {
            var rect=PlaytestGame.AppearancePanel(size.x,size.y);
            Check(rect.xMin>=0&&rect.yMin>=0&&rect.xMax<size.x&&rect.yMax<size.y,"appearance panel inside window");
            Check(rect.height>=348&&rect.width>=340,"all appearance controls fit");
            float viewRight=128+PlaytestGame.LiveStatusWidth(size.x)+116+84+170;
            Check(viewRight<=size.x-12&&PlaytestGame.LiveStatusWidth(size.x)>=0,"live footer keeps view button inside window");
            var projection=CameraRig.ShiftedProjection(Matrix4x4.Perspective(38,size.x/size.y,.1f,100),PlaytestGame.AppearanceShift(size.x),size.x);
            var projected=projection*new Vector4(0,0,-10,1);
            Check(projected.x/projected.w>0,"appearance preview shifts right of controls");
        }
        var factory=new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"),Shader.Find("Universal Render Pipeline/Unlit"),Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        var host=new GameObject("Ground checks"); var board=host.AddComponent<BoardView>(); board.Build(factory);
        var lands=new System.Collections.Generic.List<Piece>();
        string[] keys={"ic.boardSkin.0","ic.boardSkin.1"}; bool[] exists=keys.Select(PlayerPrefs.HasKey).ToArray(); string[] values=keys.Select(k=>PlayerPrefs.GetString(k)).ToArray();
        try {
            foreach(var name in BoardSkin.Presets) Check(BoardSkin.Load(name).name==name,"preset loads "+name);
            BoardSkin.Select(0,"Moonstone"); BoardSkin.Select(1,"Obsidian"); board.ApplySkins();
            Check(BoardSkin.Selected(0).name=="Moonstone"&&BoardSkin.Selected(1).name=="Obsidian","independent saved halves");
            Check(BoardSkin.Load("invalid",1).name=="Tideglass","invalid preset fallback");
            foreach(var cell in BoardConfig.DrawnCells) {
                var go=factory.Create(new CardEntry {id="ground-test",type="LAND",faction="ZEUS"},cell.y%2,out var real);
                var p=go.GetComponent<Piece>(); lands.Add(p); go.transform.rotation=Quaternion.Euler(0,195,0); board.Add(p,cell.x,cell.y);
                Check(Quaternion.Angle(go.transform.rotation,Quaternion.identity)<.001f,"ground remains board aligned");
                Check(go.GetComponent<LandSurface>()!=null&&go.GetComponent<MeshCollider>()!=null,"full hex ground and collider");
            }
            foreach(var cell in BoardConfig.DrawnCells) for(int edge=0;edge<6;edge++) {
                var center=BoardLayout.CellCenter(cell.x,cell.y); var neighbor=center+LandSurface.EdgeNormal(edge)*BoardLayout.Spacing;
                bool adjacent=BoardConfig.DrawnCells.Any(c=>(BoardLayout.CellCenter(c.x,c.y)-neighbor).sqrMagnitude<.00001f);
                Check(((board.GroundConnections(cell.x,cell.y)&(1<<edge))!=0)==adjacent,"shared edge mask");
                if(adjacent) Check((center+LandSurface.Corner(edge)-neighbor-LandSurface.Corner(edge+4)).sqrMagnitude<.00001f,"shared vertices meet");
            }
            Check(board.GroundConnections(0,0)==0,"cut-off corner has no ground connections");
            var neutral=lands.First(p=>p.X==3&&p.Y==3);
            Check(neutral.GetComponent<LandSurface>().AppliedSkinName=="Obsidian","neutral land follows owner preset");
            BoardSkin.Select(1,"Tideglass");board.ApplySkins();
            Check(neutral.GetComponent<LandSurface>().AppliedSkinName=="Tideglass","neutral land updates when owner preset changes");
            var first=lands.First(p=>p.X==2&&p.Y==3); var neighborPiece=lands.First(p=>p.X==3&&p.Y==3);
            int before=neighborPiece.GetComponent<LandSurface>().HiddenEdgeMask; board.Remove(first);
            Check(neighborPiece.GetComponent<LandSurface>().HiddenEdgeMask!=before,"removed land restores neighbor boundary");
            foreach(var name in new[]{"Default","CapitalArtTest"}) for(int winner=0;winner<2;winner++) for(int start=0;start<2;start++) {
                var landing=BoardLayout.CellCenter(2,3)+Vector3.up*BoardLayout.TileTop;
                var flip=CoinFlip3D.Play(winner,CoinSkin.Load(name),Shader.Find("Universal Render Pipeline/Lit"),null,startFace:start,boardPosition:landing);
                flip.StopAllCoroutines(); var pose=typeof(CoinFlip3D).GetMethod("Pose",BindingFlags.Instance|BindingFlags.NonPublic);
                for(float t=0;t<3.8f;t+=.1f) { pose.Invoke(flip,new object[]{t}); Check(flip.Coin.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v=>flip.Coin.TransformPoint(v).y)>=landing.y-.002f,"coin clears board"); }
                Check(flip.OnBoard&&flip.FaceUpSeat==winner,"on-board engine winner retained");
                Check(Vector2.Distance(new Vector2(flip.Coin.position.x,flip.Coin.position.z),new Vector2(landing.x,landing.z))<.001f,"coin lands at board center");
                UnityEngine.Object.DestroyImmediate(flip.gameObject);
            }
            Debug.Log("GROUND_SKINS_CHECKS passed="+checks);
        } finally { for(int i=0;i<2;i++) { if(exists[i]) PlayerPrefs.SetString(keys[i],values[i]); else PlayerPrefs.DeleteKey(keys[i]); } PlayerPrefs.Save(); foreach(var p in lands) UnityEngine.Object.DestroyImmediate(p.gameObject); UnityEngine.Object.DestroyImmediate(host); }
        System.IO.Directory.CreateDirectory("Build");
        System.IO.File.WriteAllText("Build/ground-skins-validation.json","{\"passed\":true,\"checks\":"+checks+",\"cells\":"+BoardConfig.CellCount+"}");
        ReleaseReadinessBuild.Build();
    }
}
