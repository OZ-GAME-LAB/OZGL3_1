#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Offline import, persistence and genuine skinned-model rendering only.
// No game controller, AnimatorController, animation corrections or VFX.
public static class MonsterUnityReview
{
    const string Root="Assets/Art/Monsters/Stage01/Melee01/v001";
    const string Fbx=Root+"/FBX/Stage01_Melee01.fbx";
    const string Prefab=Root+"/Prefabs/Stage01_Melee01.prefab";
    const int Rate=30, Width=960, Height=540;
    static readonly string[] Names={"Idle","Move","Attack","Hit"};
    static readonly string[] Views={"Side","Front","Opposite","Back"};
    static string SourceRoot => Path.GetFullPath(Path.Combine(Application.dataPath,"../../.."));
    static string Output => Path.Combine(SourceRoot,"QA/UnityRendered");
    [Serializable] sealed class ClipInfo { public string name,asset; public float seconds;public int frames,curves; public bool loop;public float endpointMeshDifference,minimumGroundY;public Vector3 firstRoot,lastRoot; }
    [Serializable] sealed class ConnectionInfo { public string first,second; public float blendSeconds,rawEndStartMeshDifference;public int frames; }
    [Serializable] sealed class NativeDurations { public float Idle,Move,Attack,Hit; }
    [Serializable] sealed class NativeManifest { public NativeDurations durations_seconds; }
    [Serializable] sealed class Report { public string unity,method,fbx,prefab,fbxSHA256,prefabSHA256; public bool success,gameplayIntegrated,controllerCreated,visualApproval;public int width,height,rate,skins,bones,skinBoneBindings,vertices;public Bounds worldBounds;public Vector3[] cameraPositions;public string[] views;public float orthographicSize,opaqueFloorTopY,groundReferenceY;public ClipInfo[] clips;public ConnectionInfo[] connections;public string scope; }

    public static void BuildAndRender(){BuildAndRenderInternal(false);}
    public static void RenderSaved(){BuildAndRenderInternal(true);}
    public static void DiagnoseSaved()
    {
        try{
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            var rows=new List<string>();
            foreach(string name in Names){var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+name+".anim");
                for(int i=0;i<=Mathf.CeilToInt(clip.length*Rate);i++){
                    float t=Mathf.Min(clip.length,i/(float)Rate);clip.SampleAnimation(actor,t);
                    foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
                        var mesh=new Mesh();skin.BakeMesh(mesh,true);var points=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();
                        var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);Object.DestroyImmediate(mesh);
                        rows.Add(name+" t="+t+" skin="+skin.name+" center="+b.center.ToString("F4")+" size="+b.size.ToString("F4")+" objectScale="+skin.transform.lossyScale.ToString("F4"));
                    }
                    if(i==0||i==15)foreach(var tr in actor.GetComponentsInChildren<Transform>(true))rows.Add("BONE "+name+" t="+t+" "+AnimationUtility.CalculateTransformPath(tr,actor.transform)+" p="+tr.localPosition.ToString("F5")+" s="+tr.localScale.ToString("F5")+" q="+tr.localRotation.ToString("F5"));
                }
                foreach(var binding in AnimationUtility.GetCurveBindings(clip)){var keys=AnimationUtility.GetEditorCurve(clip,binding).keys;float min=keys.Min(k=>k.value),max=keys.Max(k=>k.value);if(binding.propertyName.Contains("Scale")||binding.propertyName.Contains("Position"))rows.Add("CURVE "+name+" "+binding.path+" "+binding.propertyName+" min="+min+" max="+max+" n="+keys.Length);}
            }
            File.WriteAllLines(Path.Combine(Output,"diagnostic.txt"),rows);Object.DestroyImmediate(actor);Debug.Log("MONSTER_DIAGNOSTIC_READY");
        }catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    static void BuildAndRenderInternal(bool resumeSaved)
    {
        try
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null) throw new InvalidOperationException("A real graphics device is required. Omit -nographics.");
            Directory.CreateDirectory(Output);
            foreach(var folder in new[]{"Clips","Prefabs","Materials"}) Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if(!resumeSaved)
            {
                ConfigureImport();
                var imported=AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
                foreach(string name in Names)
                {
                    var choices=imported.Where(c=>c.name==name||c.name.EndsWith("|"+name,StringComparison.Ordinal)||c.name.EndsWith("_"+name,StringComparison.Ordinal)).ToArray();
                    if(choices.Length!=1) throw new InvalidOperationException("Expected one native clip '"+name+"', got "+choices.Length+". Available: "+string.Join(",",imported.Select(c=>c.name)));
                    var clip=Object.Instantiate(choices[0]); clip.name=name;
                    var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=name=="Idle"||name=="Move"; settings.loopBlend=false; AnimationUtility.SetAnimationClipSettings(clip,settings);
                    string path=Root+"/Clips/"+name+".anim";
                    var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if(existing){EditorUtility.CopySerialized(clip,existing);existing.name=name;EditorUtility.SetDirty(existing);Object.DestroyImmediate(clip);}
                    else AssetDatabase.CreateAsset(clip,path);
                }
                BuildPrefab();
            }
            AssetDatabase.SaveAssets();
            foreach(string name in Names) AssetDatabase.ImportAsset(Root+"/Clips/"+name+".anim",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(Prefab,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            var clips=Names.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+n+".anim")).ToArray();
            if(clips.Any(c=>!c)||!AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)) throw new InvalidOperationException("Saved assets failed reload.");
            var native=JsonUtility.FromJson<NativeManifest>(File.ReadAllText(Path.Combine(SourceRoot,"QA/build-manifest.json"))).durations_seconds;
            var expected=new[]{native.Idle,native.Move,native.Attack,native.Hit};
            for(int i=0;i<clips.Length;i++)if(Mathf.Abs(clips[i].length-expected[i])>1e-4f)throw new InvalidOperationException("Native timing mismatch: "+Names[i]+" saved="+clips[i].length+" expected="+expected[i]);
            using(var stage=new ReviewStage())
            {
                var actor=stage.Instantiate();
                var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if(skins.Length==0)throw new InvalidOperationException("No actual imported SkinnedMeshRenderer.");
                var info=new List<ClipInfo>(); Bounds bounds=new Bounds();bool haveBounds=false;
                foreach(var clip in clips)
                {
                    var row=new ClipInfo{name=clip.name,asset=AssetDatabase.GetAssetPath(clip),seconds=clip.length,frames=Mathf.CeilToInt(clip.length*Rate)+1,curves=AnimationUtility.GetCurveBindings(clip).Length,loop=AnimationUtility.GetAnimationClipSettings(clip).loopTime,minimumGroundY=float.PositiveInfinity};
                    clip.SampleAnimation(actor,0);var first=WorldVertices(actor);row.firstRoot=actor.transform.localPosition;
                    for(int i=0;i<row.frames;i++)
                    {
                        clip.SampleAnimation(actor,Mathf.Min(clip.length,i/(float)Rate));
                        foreach(var v in WorldVertices(actor))
                        {
                            if(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z))throw new InvalidOperationException("Non-finite skinned vertex.");
                            if(!haveBounds){bounds=new Bounds(v,Vector3.zero);haveBounds=true;}else bounds.Encapsulate(v);row.minimumGroundY=Mathf.Min(row.minimumGroundY,v.y);
                        }
                    }
                    clip.SampleAnimation(actor,clip.length);row.lastRoot=actor.transform.localPosition;row.endpointMeshDifference=MaxDifference(first,WorldVertices(actor));info.Add(row);
                }
                stage.Frame(bounds);
                for(int k=0;k<clips.Length;k++)
                {
                    var clip=clips[k];
                    for(int i=0;i<info[k].frames;i++)
                    {
                        clip.SampleAnimation(actor,Mathf.Min(clip.length,i/(float)Rate));
                        foreach(string view in Views)stage.Save(Path.Combine(Output,clip.name,view,i.ToString("D4")+".png"),view);
                    }
                }
                var connections=new List<ConnectionInfo>();
                foreach(var pair in new[]{new[]{0,0},new[]{1,1},new[]{0,1},new[]{1,2},new[]{2,0},new[]{3,0}})
                    connections.Add(stage.RenderConnection(actor,clips[pair[0]],clips[pair[1]]));
                var report=new Report{success=true,unity=Application.unityVersion,method="Saved/reloaded native Generic FBX clips and independent prefab. Actual SkinnedMeshRenderer rendered by Unity Camera.Render into RenderTexture at 30fps in four fixed views. AnimationClip.SampleAnimation and an offline Playables mixer only; no bone-pose corrections or gameplay transition changes. World-space audit uses BakeMesh(mesh,true) to compensate FBX renderer scale.",fbx=Fbx,prefab=Prefab,fbxSHA256=Hash(Fbx),prefabSHA256=Hash(Prefab),width=Width,height=Height,rate=Rate,skins=skins.Length,bones=skins.SelectMany(s=>s.bones).Distinct().Count(),skinBoneBindings=skins.Sum(s=>s.bones.Length),vertices=skins.Sum(s=>s.sharedMesh.vertexCount),worldBounds=bounds,views=Views,cameraPositions=Views.Select(stage.Position).ToArray(),orthographicSize=stage.Size,opaqueFloorTopY=-.05f,groundReferenceY=0,clips=info.ToArray(),connections=connections.ToArray(),gameplayIntegrated=false,controllerCreated=false,visualApproval=false,scope="Rendered output must be visually reviewed. Opaque floor is deliberately lowered to Y=-0.05m; amber off-actor markers indicate intended ground Y=0 so toe penetration and running airborne phases remain visible. Endpoint displacement and finite-vertex measurements are diagnostics only. Connections use explicit preview-only 0.12s blends and do not certify in-game Animator integration."};
                File.WriteAllText(Path.Combine(Output,"report.json"),JsonUtility.ToJson(report,true));
                stage.SaveScene();
                Object.DestroyImmediate(actor);
            }
            Debug.Log("MONSTER_UNITY_RENDER_SUCCESS "+Output);
        }
        catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }

    static void ConfigureImport()
    {
        if(!File.Exists(Fbx))throw new FileNotFoundException("Awaiting authored native-action FBX",Fbx);
        AssetDatabase.ImportAsset(Fbx,ImportAssetOptions.ForceSynchronousImport);
        var m=(ModelImporter)AssetImporter.GetAtPath(Fbx);
        m.animationType=ModelImporterAnimationType.Generic;m.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;m.importAnimation=true;m.resampleCurves=false;m.animationCompression=ModelImporterAnimationCompression.Off;
        m.optimizeGameObjects=false;m.preserveHierarchy=true;m.importCameras=false;m.importLights=false;m.importConstraints=false;m.isReadable=true;m.globalScale=1;m.useFileScale=true;
        m.meshCompression=ModelImporterMeshCompression.Off;m.skinWeights=ModelImporterSkinWeights.Custom;m.maxBonesPerVertex=4;m.minBoneWeight=0;m.importNormals=ModelImporterNormals.Import;
        m.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        m.SaveAndReimport();
    }

    static void BuildPrefab()
    {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);if(!model)throw new InvalidOperationException("FBX import failed.");
        var actor=Object.Instantiate(model);actor.name="Stage01_Melee01";
        try
        {
            foreach(var c in actor.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(c);
            foreach(var c in actor.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            foreach(var c in actor.GetComponentsInChildren<Animation>(true))Object.DestroyImmediate(c);
            var animator=actor.GetComponent<Animator>();if(!animator)animator=actor.AddComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.enabled=true;
            var shader=Shader.Find("ProjectM/Monster Native Atlas");if(!shader||!shader.isSupported)throw new InvalidOperationException("Included native-atlas shader is unsupported.");
            var cache=new Dictionary<string,Material>();
            var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/monster_palette.png")??AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/skeleton_texture.png");
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer is SkinnedMeshRenderer skin){skin.updateWhenOffscreen=true;skin.quality=SkinQuality.Bone4;}
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.sharedMaterials=renderer.sharedMaterials.Select(original=>{
                    string name=original?original.name.Replace(" (Instance)",""):"NativePalette";
                    if(cache.TryGetValue(name,out var material))return material;
                    string safe=new string(name.Select(c=>char.IsLetterOrDigit(c)||c=='_'?c:'_').ToArray());
                    string materialPath=Root+"/Materials/"+safe+".mat";
                    material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);bool create=!material;
                    if(create)material=new Material(shader){name=safe};else material.shader=shader;
                    bool eye=name.IndexOf("glow",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("eye",StringComparison.OrdinalIgnoreCase)>=0;
                    bool flat=name.IndexOf("UrbanTeal",StringComparison.OrdinalIgnoreCase)>=0;
                    Texture texture=original?original.mainTexture:null;if(!texture&&!eye&&!flat)texture=atlas;if(flat)texture=null;
                    Color color=original&&original.HasProperty("_Color")?original.color:Color.white;
                    if(eye){color=new Color(.95f,.34f,.07f,1);texture=null;}
                    material.SetColor("_Color",color);material.SetTexture("_MainTex",texture);
                    if(create)AssetDatabase.CreateAsset(material,materialPath);else EditorUtility.SetDirty(material);cache[name]=material;return material;
                }).ToArray();
            }
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/Idle.anim").SampleAnimation(actor,0);
            bool saved;PrefabUtility.SaveAsPrefabAsset(actor,Prefab,out saved);if(!saved)throw new IOException("Independent prefab save failed.");
        }
        finally{Object.DestroyImmediate(actor);}
    }

    static Vector3[] WorldVertices(GameObject actor)
    {
        var vertices=new List<Vector3>();
        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh=new Mesh();skin.BakeMesh(mesh,true);vertices.AddRange(mesh.vertices.Select(v=>skin.transform.TransformPoint(v)));Object.DestroyImmediate(mesh);
        }
        foreach(var filter in actor.GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh)vertices.AddRange(filter.sharedMesh.vertices.Select(v=>filter.transform.TransformPoint(v)));
        return vertices.ToArray();
    }
    static float MaxDifference(Vector3[] a,Vector3[] b){if(a.Length!=b.Length)throw new InvalidOperationException("Mesh topology changed during saved-clip sampling.");float m=0;for(int i=0;i<a.Length;i++)m=Mathf.Max(m,Vector3.Distance(a[i],b[i]));return m;}
    static string Hash(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}

    sealed class ReviewStage:IDisposable
    {
        readonly Scene scene;readonly Camera camera;readonly RenderTexture rt;readonly Texture2D pixels;readonly GameObject orientation;Vector3 center;float distance;
        public float Size=>camera.orthographicSize;
        public ReviewStage()
        {
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SceneManager.SetActiveScene(scene);
            orientation=new GameObject("Review orientation outside clip root");orientation.transform.rotation=Quaternion.Euler(0,90,0);
            camera=new GameObject("ReviewCamera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.095f,.12f);camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.allowHDR=false;camera.allowMSAA=false;
            rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){antiAliasing=1};rt.Create();camera.targetTexture=rt;pixels=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Lowered review floor Y=-0.05 (not intended ground)";floor.transform.position=new Vector3(0,-.08f,0);floor.transform.localScale=new Vector3(8,.06f,8);Object.DestroyImmediate(floor.GetComponent<Collider>());
            Directory.CreateDirectory("Assets/Review");
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Review/ReviewFloor.mat");if(!mat){mat=new Material(Shader.Find("ProjectM/Monster Native Atlas"));mat.SetColor("_Color",new Color(.19f,.23f,.27f));AssetDatabase.CreateAsset(mat,"Assets/Review/ReviewFloor.mat");}floor.GetComponent<Renderer>().sharedMaterial=mat;
            var groundMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Review/GroundReference.mat");if(!groundMat){groundMat=new Material(Shader.Find("ProjectM/Monster Native Atlas"));groundMat.SetColor("_Color",new Color(.76f,.48f,.18f));AssetDatabase.CreateAsset(groundMat,"Assets/Review/GroundReference.mat");}
            foreach(float x in new[]{-.70f,.70f})foreach(float z in new[]{-.70f,.70f}){var mark=GameObject.CreatePrimitive(PrimitiveType.Cube);mark.name="Intended ground Y=0 reference";mark.transform.position=new Vector3(x,0,z);mark.transform.localScale=new Vector3(.16f,.003f,.16f);Object.DestroyImmediate(mark.GetComponent<Collider>());mark.GetComponent<Renderer>().sharedMaterial=groundMat;}
        }
        public GameObject Instantiate(){var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));actor.transform.SetParent(orientation.transform,false);
            // Only the offline review clone disables Animator while sampling.
            // The delivered prefab keeps it enabled for normal later integration.
            actor.GetComponent<Animator>().enabled=false;
            // Many poses are rendered inside ONE editor update. Unity otherwise
            // caches skin matrices while the rigid weapon transform still updates.
            // This documented flag refreshes the actual SkinnedMeshRenderer per render.
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.forceMatrixRecalculationPerRender=true;
            return actor;}
        public void Frame(Bounds bounds){center=bounds.center;center.y=Mathf.Max(center.y,bounds.size.y*.5f);distance=7;camera.orthographicSize=Mathf.Max(bounds.size.y*.62f,Mathf.Max(bounds.size.x,bounds.size.z)*.62f/(Width/(float)Height));}
        public Vector3 Position(string view){switch(view){case "Side":return center+new Vector3(0,.20f,-distance);case "Opposite":return center+new Vector3(0,.20f,distance);case "Front":return center+new Vector3(distance,.10f,0);case "Back":return center+new Vector3(-distance,.10f,0);default:throw new ArgumentException(view);}}
        public void Save(string path,string view)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));camera.transform.position=Position(view);camera.transform.LookAt(center,Vector3.up);camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply(false);RenderTexture.active=old;File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        public ConnectionInfo RenderConnection(GameObject actor,AnimationClip first,AnimationClip second)
        {
            first.SampleAnimation(actor,first.length);var end=WorldVertices(actor);second.SampleAnimation(actor,0);var start=WorldVertices(actor);
            var row=new ConnectionInfo{first=first.name,second=second.name,blendSeconds=.12f,rawEndStartMeshDifference=MaxDifference(end,start)};
            string folder=Path.Combine(Output,"Connections",first.name+"_to_"+second.name);
            // Raw endpoint switches are saved explicitly, independent of the smoothed preview.
            first.SampleAnimation(actor,first.length);Save(Path.Combine(folder,"raw_end.png"),"Side");second.SampleAnimation(actor,0);Save(Path.Combine(folder,"raw_start.png"),"Side");
            var animator=actor.GetComponent<Animator>();animator.enabled=true;
            var graph=PlayableGraph.Create("Offline native clip connection preview");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            try
            {
                var mixer=AnimationMixerPlayable.Create(graph,2);var a=AnimationClipPlayable.Create(graph,first);var b=AnimationClipPlayable.Create(graph,second);a.SetApplyFootIK(false);b.SetApplyFootIK(false);a.SetApplyPlayableIK(false);b.SetApplyPlayableIK(false);graph.Connect(a,0,mixer,0);graph.Connect(b,0,mixer,1);
                var output=AnimationPlayableOutput.Create(graph,"Actual imported skinned actor",animator);output.SetSourcePlayable(mixer);graph.Play();
                float before=Mathf.Min(.30f,first.length),after=Mathf.Min(.40f,second.length);float duration=before+row.blendSeconds+after;row.frames=Mathf.CeilToInt(duration*Rate)+1;
                for(int i=0;i<row.frames;i++)
                {
                    float t=Mathf.Min(duration,i/(float)Rate),blend=Mathf.Clamp01((t-before)/row.blendSeconds);a.SetTime(Mathf.Min(first.length,first.length-before+t));b.SetTime(Mathf.Clamp(t-before,0,second.length));mixer.SetInputWeight(0,1-blend);mixer.SetInputWeight(1,blend);graph.Evaluate(0);Save(Path.Combine(folder,"Blend",i.ToString("D4")+".png"),"Side");
                }
            }
            finally{graph.Destroy();animator.enabled=false;}
            return row;
        }
        public void SaveScene(){Directory.CreateDirectory("Assets/Review");camera.targetTexture=null;EditorSceneManager.SaveScene(scene,"Assets/Review/Stage01_Melee01_Review.unity");camera.targetTexture=rt;}
        public void Dispose(){camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
    }
}
#endif
