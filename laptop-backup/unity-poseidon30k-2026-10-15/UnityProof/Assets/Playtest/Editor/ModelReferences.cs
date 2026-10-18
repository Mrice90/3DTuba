using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using InfiniteConquest.Playtest;

public static class ModelReferences {
    public static void Render() {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string output = "Build/model-references"; Directory.CreateDirectory(output);
        var factory = new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        var sun = new GameObject("Reference key").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 2; sun.transform.rotation = Quaternion.Euler(45,-35,0);
        var fill = new GameObject("Reference fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .7f; fill.transform.rotation = Quaternion.Euler(25,150,0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.45f,.47f,.5f);
        var cam = new GameObject("Model reference camera").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.025f,.04f,.065f);
        cam.orthographic = true;
        var manifest = new StringBuilder("card_id,type,reference_kind,model_resource,image\n");
        foreach (var card in PlaytestCatalog.All) {
            var token = factory.Create(card, 0, out var real);
            var renderers = token.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new System.Exception("No reference geometry " + card.id);
            var b = renderers[0].bounds; foreach (var renderer in renderers) b.Encapsulate(renderer.bounds);
            cam.orthographicSize = Mathf.Max(.15f, b.size.magnitude * .65f);
            cam.transform.position = b.center + new Vector3(1.3f,.8f,-2f).normalized * Mathf.Max(4f,b.size.magnitude*3);
            cam.transform.LookAt(b.center);
            var rt = new RenderTexture(512,512,24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var image = new Texture2D(512,512,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,512,512),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(output,card.id+".png"),image.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(image); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(token);
            manifest.Append(card.id+","+card.type+","+(real?"actual_imported_model":"procedural_standin")+","+card.model+","+card.id+".png\n");
        }
        File.WriteAllText(Path.Combine(output,"model-reference-manifest.csv"),manifest.ToString());
        Debug.Log("MODEL_REFERENCES PASS " + PlaytestCatalog.All.Length);
    }
}
