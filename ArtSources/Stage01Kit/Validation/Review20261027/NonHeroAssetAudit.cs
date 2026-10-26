// Disposable Editor-only inspection helper. Never saves or changes an asset/importer/scene.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class NonHeroAssetAudit
{
    const string Root="Assets/Art/Stage01Kit";
    const string Output=@"C:\Users\me\Desktop\OZGameLab3\OZGL3_1\ArtSources\Stage01Kit\Validation\Review20261027";
    [Serializable] class MeshInfo { public string name,path; public int vertices,triangles,degenerateTriangles,nonfiniteVertices,invalidIndices; public bool readable; }
    [Serializable] class PrefabInfo { public string name,path; public int renderers,colliders,lodGroups,missingComponents; public Bounds bounds; public MeshInfo[] meshes; public string[] materialIssues; public string[] pictures; public int[] lodRendererCounts; }
    [Serializable] class Report { public string unity,method,scope; public int prefabCount; public PrefabInfo[] prefabs; public string[] issues; public string orbTransparencyImage; public bool assetsModified=false; }
    static List<string> issues=new List<string>();
    static bool Finite(float f){return !float.IsNaN(f)&&!float.IsInfinity(f);}
    static MeshInfo Inspect(Mesh m)
    {
        var q=new MeshInfo{name=m.name,path=AssetDatabase.GetAssetPath(m),vertices=m.vertexCount,readable=m.isReadable};
        try { var v=m.vertices; q.nonfiniteVertices=v.Count(p=>!Finite(p.x)||!Finite(p.y)||!Finite(p.z));
            for(int sub=0;sub<m.subMeshCount;sub++){var ids=m.GetIndices(sub);if(m.GetTopology(sub)!=MeshTopology.Triangles)continue;q.triangles+=ids.Length/3;
                for(int i=0;i+2<ids.Length;i+=3){int a=ids[i],b=ids[i+1],c=ids[i+2];if(a<0||b<0||c<0||a>=v.Length||b>=v.Length||c>=v.Length){q.invalidIndices++;continue;}if(Vector3.Cross(v[b]-v[a],v[c]-v[a]).sqrMagnitude<1e-16f)q.degenerateTriangles++;}}
        } catch(Exception e){issues.Add("Mesh read unavailable: "+m.name+" "+e.Message);}
        return q;
    }
    static Bounds GetBounds(GameObject go)
    {
        var rr=go.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
        if(rr.Length==0)throw new InvalidOperationException("No visible renderer: "+go.name);
        var b=rr[0].bounds;foreach(var r in rr.Skip(1))b.Encapsulate(r.bounds);return b;
    }
    static Vector3[] Corners(Bounds b){var a=new List<Vector3>();foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})a.Add(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));return a.ToArray();}
    static void Capture(Camera cam,RenderTexture rt,Bounds bounds,Vector3 direction,string path)
    {
        cam.transform.position=bounds.center+direction.normalized*(bounds.size.magnitude*2+10);cam.transform.LookAt(bounds.center);
        float h=0,w=0;foreach(var p in Corners(bounds)){var d=p-bounds.center;h=Mathf.Max(h,Mathf.Abs(Vector3.Dot(d,cam.transform.up)));w=Mathf.Max(w,Mathf.Abs(Vector3.Dot(d,cam.transform.right)));}
        cam.orthographicSize=Mathf.Max(.07f,Mathf.Max(h,w/cam.aspect)*1.14f);cam.Render();cam.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=previous;
    }
    public static void Run()
    {
        if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Actual rendering requires graphics; do not run -nographics.");
        Directory.CreateDirectory(Output);Directory.CreateDirectory(Path.Combine(Output,"StaticFrames"));issues.Clear();
        var scene=EditorSceneManager.NewPreviewScene();var previousAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        var previousAmbient=RenderSettings.ambientMode;var previousAmbientLight=RenderSettings.ambientLight;var previousIntensity=RenderSettings.ambientIntensity;var previousSun=RenderSettings.sun;
        var created=new List<Object>();var rows=new List<PrefabInfo>();string orbImage=null;
        try{
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.42f,.46f,.51f);RenderSettings.ambientIntensity=1;
            var cameraGo=new GameObject("Audit_Camera");SceneManager.MoveGameObjectToScene(cameraGo,scene);created.Add(cameraGo);var cam=cameraGo.AddComponent<Camera>();cam.scene=scene;cam.orthographic=true;cam.nearClipPlane=.02f;cam.farClipPlane=1000;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.11f,.13f,.17f);cam.allowHDR=false;cam.allowMSAA=true;
            var rt=new RenderTexture(480,480,24,RenderTextureFormat.ARGB32){antiAliasing=4};created.Add(rt);rt.Create();cam.targetTexture=rt;cam.aspect=1;
            foreach(var spec in new[]{new Vector4(48,-38,0,1.1f),new Vector4(30,135,0,.6f)}){var go=new GameObject("Audit_Light");SceneManager.MoveGameObjectToScene(go,scene);created.Add(go);var l=go.AddComponent<Light>();l.type=LightType.Directional;l.intensity=spec.w;l.shadows=LightShadows.None;go.transform.rotation=Quaternion.Euler(spec.x,spec.y,spec.z);if(RenderSettings.sun==null)RenderSettings.sun=l;}
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x).ToArray();
            foreach(var path in paths){
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!prefab){issues.Add("Cannot load prefab "+path);continue;}
                var go=Object.Instantiate(prefab);SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.identity;
                foreach(var b in go.GetComponentsInChildren<MonoBehaviour>(true))if(b)b.enabled=false;
                foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;
                foreach(var lod in go.GetComponentsInChildren<LODGroup>(true))lod.ForceLOD(0);
                var renderers=go.GetComponentsInChildren<Renderer>(true);var mats=new List<string>();
                foreach(var r in renderers){if(r.sharedMaterials.Length==0)mats.Add(r.name+" no materials");foreach(var m in r.sharedMaterials)if(!m||!m.shader||m.shader.name=="Hidden/InternalErrorShader")mats.Add(r.name+" missing material/shader");}
                var meshes=go.GetComponentsInChildren<MeshFilter>(true).Where(x=>x.sharedMesh).Select(x=>x.sharedMesh).Distinct().Select(Inspect).ToArray();
                int missing=go.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                var bounds=GetBounds(go);string slug=Path.GetFileNameWithoutExtension(path);var pics=new List<string>();
                foreach(var view in new[]{"Side","ThreeQuarter"}){string img=Path.Combine(Output,"StaticFrames",slug+"_"+view+".png");Capture(cam,rt,bounds,view=="Side"?new Vector3(.12f,.125f,-1):new Vector3(.65f,.3f,-1),img);pics.Add(img);}
                rows.Add(new PrefabInfo{name=slug,path=path,renderers=renderers.Length,colliders=go.GetComponentsInChildren<Collider>(true).Length,lodGroups=go.GetComponentsInChildren<LODGroup>(true).Length,missingComponents=missing,bounds=bounds,meshes=meshes,materialIssues=mats.ToArray(),pictures=pics.ToArray(),lodRendererCounts=go.GetComponentsInChildren<LODGroup>(true).SelectMany(l=>l.GetLODs()).Select(l=>l.renderers.Length).ToArray()});
                if(missing>0||mats.Count>0||meshes.Any(m=>m.nonfiniteVertices>0||m.invalidIndices>0))issues.Add(slug+" has structural issues; see details.");
                if(slug=="Staff"){
                    var orb=renderers.FirstOrDefault(r=>r.name.IndexOf("orb",StringComparison.OrdinalIgnoreCase)>=0);if(!orb)orb=renderers.FirstOrDefault(r=>r.sharedMaterials.Any(m=>m&&m.shader&&m.shader.name.Contains("Staff Orb")));
                    if(orb){var plate=new GameObject("Audit_TransparencyWitness");SceneManager.MoveGameObjectToScene(plate,scene);var unlit=Shader.Find("Universal Render Pipeline/Unlit");if(!unlit)unlit=Shader.Find("Unlit/Color");
                        foreach(int side in new[]{-1,1}){var tile=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(tile,scene);tile.transform.SetParent(plate.transform,false);tile.transform.position=orb.bounds.center+new Vector3(side*.045f,0,.12f);tile.transform.localScale=new Vector3(.09f,.2f,.015f);var material=new Material(unlit);created.Add(material);material.color=side<0?new Color(1,.75f,.05f):new Color(.15f,.25f,1);if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",material.color);tile.GetComponent<Renderer>().sharedMaterial=material;}
                        orbImage=Path.Combine(Output,"StaticFrames","Staff_Orb_Transparency.png");var close=new Bounds(orb.bounds.center,new Vector3(.32f,.32f,.32f));Capture(cam,rt,close,new Vector3(0,0,-1),orbImage);Object.DestroyImmediate(plate);
                    }else issues.Add("Staff orb renderer could not be identified.");
                }
                Object.DestroyImmediate(go);Debug.Log("NONHERO_AUDITED "+slug);
            }
            var report=new Report{unity=Application.unityVersion,method="Read-only actual retained prefabs. Temporary scene; actual shared meshes and shaders; deterministic orthographic side/three-quarter Camera.Render. LOD0 forced only on disposable instances. No asset/importer/scene saved.",scope="All Stage01Kit delivered prefabs including two assembled blocks. Static geometry/material completeness, not gameplay collision/path traversal or target-device performance certification.",prefabCount=rows.Count,prefabs=rows.ToArray(),issues=issues.ToArray(),orbTransparencyImage=orbImage};
            File.WriteAllText(Path.Combine(Output,"unity-static-audit.json"),JsonUtility.ToJson(report,true));Debug.Log("NONHERO_STATIC_AUDIT_COMPLETE "+rows.Count+" issues="+issues.Count);
        }finally{RenderSettings.ambientMode=previousAmbient;RenderSettings.ambientLight=previousAmbientLight;RenderSettings.ambientIntensity=previousIntensity;RenderSettings.sun=previousSun;ShaderUtil.allowAsyncCompilation=previousAsync;foreach(var o in created.AsEnumerable().Reverse())if(o)Object.DestroyImmediate(o);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
