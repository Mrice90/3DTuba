using UnityEngine;
namespace InfiniteConquest.Playtest {
    // Decorative environment only: deliberately no colliders or rules coordinates.
    public static class BattleTabletop {
        public static GameObject Build(Transform parent,TokenFactory factory,float boardRadius) {
            var root=new GameObject("Conquest tabletop");root.transform.SetParent(parent,false);
            var stone=Resources.Load<Texture2D>("Environment/ConquestStone");
            var material=new Material(factory.Lit(new Color(.55f,.60f,.66f),.24f,.08f)){name="Honed conquest stone"};
            if(stone!=null){material.SetTexture("_BaseMap",stone);material.mainTexture=stone;material.mainTextureScale=new Vector2(24,24);}
            var surface=new Mesh {name="Tabletop surface"};
            surface.vertices=new[]{new Vector3(-100,0,-100),new Vector3(-100,0,100),new Vector3(100,0,100),new Vector3(100,0,-100)};
            surface.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};
            surface.triangles=new[]{0,1,2,0,2,3};surface.RecalculateNormals();surface.RecalculateBounds();
            PlaytestMeshes.Make("Honed stone table",surface,material,root.transform,new Vector3(0,-.86f,0));
            PlaytestMeshes.Make("Board support",PlaytestMeshes.Prism(6,boardRadius*.97f,boardRadius*.93f,.4f,0),factory.Lit(new Color(.045f,.055f,.07f),.35f,.55f),root.transform,new Vector3(0,-.85f,0));
            var brass=factory.Lit(new Color(.31f,.24f,.13f),.45f,.75f);
            PlaytestMeshes.Make("Outer brass inlay",Ring(boardRadius+1.15f,.07f),brass,root.transform,new Vector3(0,-.84f,0));
            PlaytestMeshes.Make("Inner brass inlay",Ring(boardRadius+.92f,.04f),brass,root.transform,new Vector3(0,-.839f,0));
            root.AddComponent<TabletopAppearance>().Initialize(material,brass);
            return root;
        }
        static Mesh Ring(float radius,float width) {
            const int count=144;var vertices=new Vector3[count*2];var triangles=new int[count*6];
            for(int i=0;i<count;i++) {
                float angle=i*Mathf.PI*2/count;var ray=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                vertices[i*2]=ray*radius;vertices[i*2+1]=ray*(radius-width);
                int n=((i+1)%count)*2,t=i*6;triangles[t]=i*2;triangles[t+1]=i*2+1;triangles[t+2]=n;triangles[t+3]=n;triangles[t+4]=i*2+1;triangles[t+5]=n+1;
            }
            var mesh=new Mesh{name="Inlaid brass circle"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
