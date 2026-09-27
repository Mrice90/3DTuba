using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InfiniteConquest.Proof {
    public sealed class ProofCell : MonoBehaviour { public int X, Y; }
    public sealed class MovementProof : MonoBehaviour {
        public Shader ProofShader;
        readonly MovementState state = new MovementState();
        readonly Renderer[,] tiles = new Renderer[4,6];
        Transform runner;
        GameObject wall;
        Camera boardCamera;
        string message = "Select a glowing tile to move the runner.";
        Material floor, legal, occupied, runnerMat, wallMat;
        Material Mat(Color color) {
            var mat = new Material(ProofShader);
            mat.color = color;
            return mat;
        }
        static GameObject Piece(PrimitiveType shape, string name, Vector3 at, Vector3 scale, Material material) {
            var obj=GameObject.CreatePrimitive(shape);
            obj.name=name; obj.transform.position=at; obj.transform.localScale=scale;
            obj.GetComponent<Renderer>().sharedMaterial=material;
            return obj;
        }
        static Vector3 Position(int x,int y) { return new Vector3((x-1.5f)*1.3f,0,(y-2.5f)*1.3f); }
        void Awake() {
            Application.runInBackground=true;
            Application.targetFrameRate=60;
            floor=Mat(new Color(.09f,.15f,.22f));
            legal=Mat(new Color(.12f,.55f,.49f));
            occupied=Mat(new Color(.27f,.37f,.53f));
            runnerMat=Mat(new Color(.92f,.69f,.25f));
            wallMat=Mat(new Color(.72f,.20f,.25f));
            var camObj=new GameObject("Board camera");
            boardCamera=camObj.AddComponent<Camera>();
            boardCamera.transform.position=new Vector3(6,10,-11);
            boardCamera.transform.LookAt(Vector3.zero);
            boardCamera.orthographic=true; boardCamera.orthographicSize=5.8f;
            boardCamera.backgroundColor=new Color(.025f,.04f,.065f);
            boardCamera.clearFlags=CameraClearFlags.SolidColor;
            var sun=new GameObject("Key light").AddComponent<Light>();
            sun.type=LightType.Directional; sun.intensity=2.2f;
            sun.transform.rotation=Quaternion.Euler(50,-35,0);
            RenderSettings.ambientLight=new Color(.45f,.49f,.58f);
            for(int x=0;x<4;x++) for(int y=0;y<6;y++) {
                var tile=Piece(PrimitiveType.Cube,$"Tile {x},{y}",Position(x,y),new Vector3(1.18f,.18f,1.18f),floor);
                var cell=tile.AddComponent<ProofCell>(); cell.X=x;cell.Y=y;
                tiles[x,y]=tile.GetComponent<Renderer>();
            }
            runner=Piece(PrimitiveType.Capsule,"Runner",Position(0,0)+Vector3.up*.67f,new Vector3(.48f,.58f,.48f),runnerMat).transform;
            wall=Piece(PrimitiveType.Cube,"Enemy structure",Position(1,0)+Vector3.up*.55f,new Vector3(.78f,.95f,.78f),wallMat);
            // Pieces also route clicks to their board cell, including illegal moves.
            var rc=runner.gameObject.AddComponent<ProofCell>(); rc.X=0;rc.Y=0;
            var wc=wall.AddComponent<ProofCell>();wc.X=1;wc.Y=0;
            ResetFixture(false);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-proofSmoke")>=0) StartCoroutine(Smoke());
        }
        public void ResetFixture(bool blocked) {
            state.Reset(blocked); wall.SetActive(blocked);
            message=blocked ? "Enemy structure at (1,0). Try moving onto it." : "Select a glowing tile to move the runner.";
            Refresh();
        }
        void Refresh() {
            runner.position=Position(state.X,state.Y)+Vector3.up*.67f;
            var cell=runner.GetComponent<ProofCell>();cell.X=state.X;cell.Y=state.Y;
            for(int x=0;x<4;x++) for(int y=0;y<6;y++)
                tiles[x,y].sharedMaterial=state.CanMove(x,y)?legal:(state.X==x&&state.Y==y?occupied:floor);
        }
        public bool Move(int x,int y) {
            bool accepted=state.TryMove(x,y);
            message=accepted ? $"Moved to ({x},{y}); movement spent: {state.Spent}/1." : "Move rejected. Position and movement budget are unchanged.";
            Refresh();return accepted;
        }
        void Update() {
            if(Mouse.current==null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            var p=Mouse.current.position.ReadValue();
            if(p.y>Screen.height-145) return;
            if(Physics.Raycast(boardCamera.ScreenPointToRay(p),out var hit)) {
                var cell=hit.collider.GetComponent<ProofCell>();if(cell!=null) Move(cell.X,cell.Y);
            }
        }
        void OnGUI() {
            GUI.skin.label.fontSize=18;GUI.skin.button.fontSize=16;
            GUI.Box(new Rect(12,12,Mathf.Min(Screen.width-24,810),128),"");
            GUI.Label(new Rect(26,20,780,28),"INFINITE CONQUEST  |  3D movement proof");
            if(GUI.Button(new Rect(26,55,170,32),"Open board / reset")) ResetFixture(false);
            if(GUI.Button(new Rect(208,55,195,32),"Enemy structure / reset")) ResetFixture(true);
            GUI.Label(new Rect(26,95,780,32),message);
            GUI.Label(new Rect(20,Screen.height-58,Screen.width-40,48),"Gold: runner  •  Teal: legal destination  •  Red: enemy structure\nTwo fixture cases only; combat, turns and full-match rules are not implemented.");
        }
        IEnumerator Smoke() {
            yield return null;
            ResetFixture(false);
            bool legalPass=Move(1,0)&&state.X==1&&state.Y==0&&state.Spent==1;
            ResetFixture(true);
            bool blockedPass=!Move(1,0)&&state.X==0&&state.Y==0&&state.Spent==0;
            bool pass=legalPass&&blockedPass;
            Debug.Log("PROOF_SMOKE "+(pass?"PASS":"FAIL")+" legal="+legalPass+" blocked="+blockedPass);
            var args=Environment.GetCommandLineArgs();
            int i=Array.IndexOf(args,"-proofResult");
            if(i>=0 && i+1<args.Length) File.WriteAllText(args[i+1],"{\"passed\":"+pass.ToString().ToLowerInvariant()+",\"legal\":"+legalPass.ToString().ToLowerInvariant()+",\"blocked\":"+blockedPass.ToString().ToLowerInvariant()+"}");
            int shot=Array.IndexOf(args,"-proofScreenshot");
            if(shot>=0 && shot+1<args.Length) {
                ResetFixture(true);
                yield return new WaitForSecondsRealtime(4);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(args[shot+1]);
                yield return new WaitForSecondsRealtime(1);
            }
            Application.Quit(pass?0:1);
        }
    }
}
