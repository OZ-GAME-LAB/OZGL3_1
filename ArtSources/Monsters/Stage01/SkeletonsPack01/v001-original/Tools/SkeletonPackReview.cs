#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

// Offline inspection only. Does not create or change gameplay Animator logic.
public static class SkeletonPackReview
{
    const string Root="Assets/Art/Monsters/Stage01/SkeletonsPack01/v001";
    const string Old="Assets/Art/Monsters/Stage01/Melee01/v001";
    const string Hero="Assets/Art/Characters/Protagonist01/Combat";
    const int Rate=30,Width=960,Height=540;
    [Serializable] class Paths {public string source,output;}
    [Serializable] class ClipRow {public string name,asset;public float seconds,minimumY,endpointDifference,maximumFrameDisplacement;public bool loop;public int frames,curveCount;public Vector3 firstRoot,lastRoot;}
    [Serializable] class ActorRow {public string role,prefab;public int renderers,skins,bones;public ClipRow[] clips;public string[] connections;}
    [Serializable] class Report {public bool success,visualApproval,gameplayIntegrated;public string unity,method;public float orthographicSize;public int width,height,fps;public ActorRow[] actors;}
    static Paths paths;
    static string Output=>paths.output;
    static string[] Roles={"Minion","Rogue","Warrior","Mage"};
    static string[][] Names={
        new[]{"Idle","Move","Attack","Hit"},
        new[]{"Idle_Combat","Running_A","2H_Ranged_Aiming","2H_Ranged_Shoot","2H_Ranged_Reload","Hit_A"},
        new[]{"Idle_Combat","Walking_B","1H_Melee_Attack_Chop","1H_Melee_Attack_Slice_Diagonal","Hit_A"},
        new[]{"Idle_Combat","Walking_D_Skeletons","Spellcast_Shoot","Spellcast_Raise","Hit_A"}
    };
    static string Prefab(string role)=>role=="Minion"?Old+"/Prefabs/Stage01_Melee01.prefab":Root+"/Prefabs/Skeleton_"+role+".prefab";
    static string ClipPath(int role,string name)=>role==0?Old+"/Clips/"+name+".anim":Root+"/Clips/"+Roles[role]+"/"+name+".anim";
    public static void BuildAndPreview(){Run(true,true);}
    public static void BuildAndRender(){Run(true,false);}
    public static void RenderSaved(){Run(false,false);}
    public static void PreviewSaved(){Run(false,true);}
    public static void ResampleDiagnostics(){
        foreach(string role in new[]{"Rogue","Warrior","Mage"}){var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/FBX/Skeleton_"+role+".fbx");importer.resampleCurves=true;importer.SaveAndReimport();}
        PoseDiagnostics();
    }
    public static void PoseDiagnostics(){
        paths=JsonUtility.FromJson<Paths>(File.ReadAllText("review-paths.json"));var lines=new List<string>();
        foreach(string role in new[]{"Rogue","Warrior","Mage"}){
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/FBX/Skeleton_"+role+".fbx"));
            foreach(var a in actor.GetComponentsInChildren<Animator>(true))a.enabled=false;
            var clips=AssetDatabase.LoadAllAssetsAtPath(Root+"/FBX/Skeleton_"+role+".fbx").OfType<AnimationClip>().Where(c=>c.name.Contains("Idle_Combat")||c.name.Contains("1H_Melee_Attack_Chop")).Where(c=>!c.name.StartsWith("__preview__"));
            foreach(var clip in clips)foreach(float time in new[]{0f,8f/30,16f/30,.8f}){clip.SampleAnimation(actor,time);foreach(var t in actor.GetComponentsInChildren<Transform>(true).Where(t=>new[]{"hand.r","handslot.r","lowerarm.r","upperarm.r","head","root","hips"}.Contains(t.name)))lines.Add(role+" "+clip.name+" t="+time.ToString("F6")+" "+t.name+" p="+t.position.ToString("F6")+" q="+t.rotation.ToString("F6"));}
            Object.DestroyImmediate(actor);
        }
        foreach(string weapon in new[]{"Axe","Staff","Crossbow"}){var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Accessories/Skeleton_"+weapon+".fbx");foreach(var t in asset.GetComponentsInChildren<Transform>(true))lines.Add("WEAPON "+weapon+" "+t.name+" p="+t.localPosition.ToString("F6")+" q="+t.localRotation.ToString("F6")+" s="+t.localScale.ToString("F6"));foreach(var f in asset.GetComponentsInChildren<MeshFilter>(true))lines.Add("BOUNDS "+weapon+" "+f.sharedMesh.bounds.ToString());}
        File.WriteAllLines(Path.Combine(paths.source,"QA/unity-native-pose-diagnostics.txt"),lines);
    }
    public static void AttachmentStudy(){
        paths=JsonUtility.FromJson<Paths>(File.ReadAllText("review-paths.json"));
        using(var stage=new Stage())for(int k=1;k<4;k++){
            var actor=stage.AddSubject(Prefab(Roles[k]));var ar=actor.GetComponentInChildren<Animator>(true).gameObject;
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(k,Names[k][k<3?2:0]));clip.SampleAnimation(ar,k==1?.8f:k==2?8f/30:0);
            var frame=actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name.EndsWith("_NativeSocketFrame"));
            foreach(var angle in new[]{Vector3.zero,new Vector3(90,0,0),new Vector3(-90,0,0),new Vector3(0,90,0),new Vector3(0,-90,0),new Vector3(0,0,90),new Vector3(0,0,-90)}){
                frame.localRotation=Quaternion.Euler(angle);string label=angle.x+"_"+angle.y+"_"+angle.z;
                foreach(string view in new[]{"Side","Front"})stage.Save(Path.Combine(Output,"AttachmentStudy",Roles[k],label+"_"+view+".png"),view);
            }Object.DestroyImmediate(actor);
        }
        Debug.Log("ATTACHMENT_STUDY_COMPLETE");
    }
    public static void MageLeftStudy(){
        paths=JsonUtility.FromJson<Paths>(File.ReadAllText("review-paths.json"));
        using(var stage=new Stage()){
            var actor=stage.AddSubject(Prefab("Mage"));var ar=actor.GetComponentInChildren<Animator>(true).gameObject;
            AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(3,"Idle_Combat")).SampleAnimation(ar,0);
            var frame=actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name.EndsWith("_NativeSocketFrame"));
            frame.SetParent(actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="handslot.l"),false);
            foreach(var angle in new[]{Vector3.zero,new Vector3(90,0,0),new Vector3(-90,0,0),new Vector3(0,90,0),new Vector3(0,-90,0),new Vector3(0,0,90),new Vector3(0,0,-90)}){
                frame.localRotation=Quaternion.Euler(angle);string label=angle.x+"_"+angle.y+"_"+angle.z;
                foreach(string view in new[]{"Side","Front","Opposite","Back"})stage.Save(Path.Combine(Output,"AttachmentStudy","MageLeft",label+"_"+view+".png"),view);
            }Object.DestroyImmediate(actor);
        }
    }
    public static void EquipmentFitStudy(){
        paths=JsonUtility.FromJson<Paths>(File.ReadAllText("review-paths.json"));var note=new List<string>();
        using(var stage=new Stage())foreach(string role in new[]{"Rogue","Mage"}){
            var actor=stage.AddSubject(Prefab(role));var ar=actor.GetComponentInChildren<Animator>(true).gameObject;
            var clips=AssetDatabase.LoadAllAssetsAtPath(Root+"/FBX/Skeleton_"+role+".fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToDictionary(c=>c.name.Split('|').Last());
            var frame=actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name.EndsWith("_NativeSocketFrame"));
            if(role=="Mage"){
                frame.SetParent(actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="handslot.l"),false);
                foreach(float tilt in new[]{0f,15f,25f,35f}){
                    clips["Idle_Combat"].SampleAnimation(ar,0);frame.localRotation=Quaternion.Euler(0,0,90);
                    frame.rotation=Quaternion.AngleAxis(tilt,actor.transform.right)*frame.rotation;
                    note.Add("Mage tilt="+tilt+" localQuaternion="+frame.localRotation.ToString("F8")+" Euler="+frame.localEulerAngles.ToString("F6"));
                    foreach(string clip in new[]{"Idle_Combat","Spellcast_Shoot","Spellcast_Raise","Walking_D_Skeletons","Hit_A"}){
                        clips[clip].SampleAnimation(ar,clip=="Idle_Combat"?0:clips[clip].length*.5f);
                        foreach(string view in new[]{"Side","Front","Opposite","Back"})stage.Save(Path.Combine(Output,"EquipmentFitStudy",role,"Tilt"+tilt,clip+"_"+view+".png"),view);
                    }
                }
            } else foreach(string clip in new[]{"Idle","Idle_B","Idle_Combat","Hit_A","Hit_B","Running_A","Walking_A","Walking_B","Walking_C"}){
                for(int i=0;i<3;i++){clips[clip].SampleAnimation(ar,clips[clip].length*i/2);foreach(string view in new[]{"Side","Front"})stage.Save(Path.Combine(Output,"EquipmentFitStudy",role,clip,i+"_"+view+".png"),view);}
            }
            Object.DestroyImmediate(actor);
        }
        File.WriteAllLines(Path.Combine(paths.source,"QA/equipment-fit-study.txt"),note);
    }
    static void Run(bool build,bool preview)
    {
        try {
            paths=JsonUtility.FromJson<Paths>(File.ReadAllText("review-paths.json"));
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)throw new Exception("Real graphics device required.");
            if(build)SkeletonPackImport.Build();
            AssetDatabase.SaveAssets();
            foreach(var role in Roles)AssetDatabase.ImportAsset(Prefab(role),ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            Directory.CreateDirectory(Output);
            var rows=new List<ActorRow>();
            using(var stage=new Stage()){
                for(int k=0;k<Roles.Length;k++){
                    string role=Roles[k];
                    var actor=stage.AddSubject(Prefab(role));
                    var animRoot=actor.GetComponentInChildren<Animator>(true).gameObject;
                    var clips=Names[k].Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath(k,n))).ToArray();
                    if(clips.Any(c=>!c))throw new Exception("Missing selected clip: "+role);
                    var clipRows=new List<ClipRow>();
                    foreach(var clip in clips){
                        int count=Mathf.CeilToInt(clip.length*Rate)+1;
                        var row=new ClipRow{name=clip.name,asset=AssetDatabase.GetAssetPath(clip),seconds=clip.length,frames=count,loop=AnimationUtility.GetAnimationClipSettings(clip).loopTime,curveCount=AnimationUtility.GetCurveBindings(clip).Length,minimumY=float.PositiveInfinity};
                        clip.SampleAnimation(animRoot,0);var first=Vertices(actor);var previous=first;row.firstRoot=animRoot.transform.localPosition;
                        var frameBounds=new List<string>{"frame,time,minY,maxY,maxFrameDisplacement"};
                        for(int i=0;i<count;i++){
                            float time=Mathf.Min(clip.length,i/(float)Rate);clip.SampleAnimation(animRoot,time);
                            var vertices=Vertices(actor);float min=vertices.Min(v=>v.y),max=vertices.Max(v=>v.y);
                            float delta=Difference(previous,vertices);previous=vertices;row.maximumFrameDisplacement=Mathf.Max(row.maximumFrameDisplacement,delta);row.minimumY=Mathf.Min(row.minimumY,min);
                            frameBounds.Add(i+","+time.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+min.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+max.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+delta.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
                            bool key=i==0||i==count/4||i==count/2||i==3*count/4||i==count-1;
                            if(!preview||key){
                                stage.Save(Path.Combine(Output,preview?"Preview":"Frames",role,clip.name,"Side",i.ToString("D4")+".png"),"Side");
                                // Full auxiliary sequences for actions; loop clips need key poses only.
                                if(!row.loop||key)foreach(string view in new[]{"Front","Opposite","Back"})stage.Save(Path.Combine(Output,preview?"Preview":"Frames",role,clip.name,view,i.ToString("D4")+".png"),view);
                            }
                        }
                        row.lastRoot=animRoot.transform.localPosition;row.endpointDifference=Difference(first,previous);clipRows.Add(row);
                        string folder=Path.Combine(Output,"Measurements",role);Directory.CreateDirectory(folder);File.WriteAllLines(Path.Combine(folder,clip.name+".csv"),frameBounds);
                    }
                    var connections=new List<string>();
                    if(!preview){
                        var pairs=new List<int[]>{new[]{0,0},new[]{1,1},new[]{0,1},new[]{1,2},new[]{2,0},new[]{clips.Length-1,0}};
                        if(role=="Rogue"){pairs.Add(new[]{2,3});pairs.Add(new[]{3,4});pairs.Add(new[]{4,2});}
                        foreach(var pair in pairs){stage.Connection(role,actor,clips[pair[0]],clips[pair[1]]);connections.Add(clips[pair[0]].name+" -> "+clips[pair[1]].name);}
                    }
                    rows.Add(new ActorRow{role=role,prefab=Prefab(role),renderers=actor.GetComponentsInChildren<Renderer>(true).Length,skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,bones=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(s=>s.bones).Distinct().Count(),clips=clipRows.ToArray(),connections=connections.ToArray()});
                    if(!preview)stage.SaveScene(role,clips[0],animRoot);
                    Object.DestroyImmediate(actor);
                }
            }
            var report=new Report{success=true,visualApproval=false,gameplayIntegrated=false,unity=Application.unityVersion,width=Width,height=Height,fps=Rate,orthographicSize=1.1f,actors=rows.ToArray(),method="Saved prefab/clip reload; actual Unity skinned meshes and rigid costume/gear. Fixed orthographic side view and common world units with unchanged current hero reference. Full 30fps side sequences plus action auxiliary views. Preview-only Playables 0.12s blends; no gameplay transition edits. Raw clip endpoints and ground minima are diagnostics, not visual approval."};
            File.WriteAllText(Path.Combine(Output,preview?"preview-report.json":"unity-review-report.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(paths.source,"QA",preview?"unity-preview-report.json":"unity-review-report.json"),JsonUtility.ToJson(report,true));
            Debug.Log("SKELETON_PACK_REVIEW_SUCCESS preview="+preview+" "+Output);
        }catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    static Vector3[] Vertices(GameObject actor){var list=new List<Vector3>();foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){if(!skin.sharedMesh)continue;var mesh=new Mesh();skin.BakeMesh(mesh,true);list.AddRange(mesh.vertices.Select(v=>skin.transform.TransformPoint(v)));Object.DestroyImmediate(mesh);}foreach(var f in actor.GetComponentsInChildren<MeshFilter>(true))if(f.sharedMesh)list.AddRange(f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v)));foreach(var v in list)if(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z))throw new Exception("Non-finite skinned vertex");return list.ToArray();}
    static float Difference(Vector3[] a,Vector3[] b){if(a.Length!=b.Length)throw new Exception("Vertex count changed");float m=0;for(int i=0;i<a.Length;i++)m=Mathf.Max(m,Vector3.Distance(a[i],b[i]));return m;}
    sealed class Stage:IDisposable {
        Scene scene;Camera camera;RenderTexture rt;Texture2D pixels;GameObject hero;GameObject subjectParent;
        readonly Vector3 center=new Vector3(0,.88f,0);
        public Stage(){
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SceneManager.SetActiveScene(scene);
            camera=new GameObject("Fixed 2.5D Review Camera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.1f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.085f,.105f);camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.allowHDR=false;camera.allowMSAA=false;
            rt=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32){antiAliasing=1};rt.Create();camera.targetTexture=rt;pixels=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            Directory.CreateDirectory("Assets/Review");
            var mat=Mat("Floor",new Color(.16f,.20f,.23f));var mark=Mat("GroundReference",new Color(.8f,.5f,.18f));
            Cube("Review floor (top -0.05m)",new Vector3(0,-.1f,0),new Vector3(10,.1f,10),mat);
            foreach(float x in new[]{-1.65f,-.65f,.35f,1.35f})Cube("Y=0 ground reference",new Vector3(x,0,0),new Vector3(.12f,.004f,.15f),mark);
            var light=new GameObject("Review key light",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(45,-35,0);RenderSettings.ambientLight=new Color(.7f,.7f,.7f);
            var hp=new GameObject("Current protagonist reference world units");hp.transform.position=new Vector3(-1.15f,0,0);hp.transform.rotation=Quaternion.Euler(0,90,0);
            hero=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Hero+"/Prefabs/Protagonist_Combat.prefab"),hp.transform);DisableAnimators(hero);
            // The isolated project uses Built-in rendering; the real hero has URP materials.
            // Map only this temporary review instance to a portable atlas shader, retaining
            // source texture/base color. No reference prefab or material asset is changed.
            foreach(var renderer in hero.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=renderer.sharedMaterials.Select(HeroReviewMaterial).ToArray();
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Hero+"/Clips/Longsword_Attack_Chop.anim").SampleAnimation(hero,0);
            subjectParent=new GameObject("Common subject world position and side orientation");subjectParent.transform.position=new Vector3(.85f,0,0);subjectParent.transform.rotation=Quaternion.Euler(0,90,0);
        }
        Material Mat(string name,Color color){string path="Assets/Review/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("ProjectM/Skeleton Native Atlas"));m.color=color;AssetDatabase.CreateAsset(m,path);}return m;}
        Material HeroReviewMaterial(Material original){
            // Read serialized URP values even when its shader is absent in this isolated project.
            var data=new SerializedObject(original);Color color=Color.white;Texture texture=null;
            var colors=data.FindProperty("m_SavedProperties.m_Colors");
            foreach(string key in new[]{"_Color","_BaseColor"})for(int i=0;colors!=null&&i<colors.arraySize;i++){var row=colors.GetArrayElementAtIndex(i);if(row.FindPropertyRelative("first").stringValue==key)color=row.FindPropertyRelative("second").colorValue;}
            var textures=data.FindProperty("m_SavedProperties.m_TexEnvs");
            foreach(string key in new[]{"_MainTex","_BaseMap"})for(int i=0;textures!=null&&i<textures.arraySize;i++){var row=textures.GetArrayElementAtIndex(i);if(row.FindPropertyRelative("first").stringValue==key){var t=row.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;if(t)texture=t;}}
            string path="Assets/Review/Hero_"+original.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);bool create=!m;if(create)m=new Material(Shader.Find("ProjectM/Skeleton Native Atlas"));m.SetTexture("_MainTex",texture);m.SetColor("_Color",color);if(create)AssetDatabase.CreateAsset(m,path);else EditorUtility.SetDirty(m);return m;
        }
        void Cube(string name,Vector3 pos,Vector3 scale,Material mat){var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.position=pos;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(o.GetComponent<Collider>());}
        static void DisableAnimators(GameObject actor){foreach(var a in actor.GetComponentsInChildren<Animator>(true))a.enabled=false;foreach(var s in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){s.forceMatrixRecalculationPerRender=true;s.updateWhenOffscreen=true;}}
        public GameObject AddSubject(string prefab){var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab),subjectParent.transform);DisableAnimators(actor);return actor;}
        public void Save(string path,string view){
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Side comparison keeps both actors; auxiliary views isolate the subject at same scale.
            bool side=view=="Side";hero.transform.parent.gameObject.SetActive(side);
            Vector3 look=side?center:new Vector3(.85f,.88f,0);
            Vector3 offset=view=="Side"?new Vector3(0,.12f,-7):view=="Opposite"?new Vector3(0,.12f,7):view=="Front"?new Vector3(7,.12f,0):new Vector3(-7,.12f,0);
            camera.transform.position=look+offset;camera.transform.LookAt(look,Vector3.up);camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0);pixels.Apply(false);RenderTexture.active=old;File.WriteAllBytes(path,pixels.EncodeToPNG());hero.transform.parent.gameObject.SetActive(true);
        }
        public void Connection(string role,GameObject actor,AnimationClip first,AnimationClip second){
            var animator=actor.GetComponentInChildren<Animator>(true);var ar=animator.gameObject;
            string dir=Path.Combine(Output,"Connections",role,first.name+"_to_"+second.name);
            first.SampleAnimation(ar,first.length);Save(Path.Combine(dir,"raw_end.png"),"Side");second.SampleAnimation(ar,0);Save(Path.Combine(dir,"raw_start.png"),"Side");
            animator.enabled=true;var graph=PlayableGraph.Create("Review only native clip blend");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            try{var mix=AnimationMixerPlayable.Create(graph,2);var a=AnimationClipPlayable.Create(graph,first);var b=AnimationClipPlayable.Create(graph,second);a.SetApplyFootIK(false);b.SetApplyFootIK(false);a.SetApplyPlayableIK(false);b.SetApplyPlayableIK(false);graph.Connect(a,0,mix,0);graph.Connect(b,0,mix,1);var output=AnimationPlayableOutput.Create(graph,"Review",animator);output.SetSourcePlayable(mix);graph.Play();float before=Mathf.Min(.30f,first.length),after=Mathf.Min(.4f,second.length),duration=before+.12f+after;int frames=Mathf.CeilToInt(duration*Rate)+1;for(int i=0;i<frames;i++){float t=Mathf.Min(duration,i/(float)Rate);float blend=Mathf.Clamp01((t-before)/.12f);a.SetTime(Mathf.Min(first.length,first.length-before+t));b.SetTime(Mathf.Clamp(t-before,0,second.length));mix.SetInputWeight(0,1-blend);mix.SetInputWeight(1,blend);graph.Evaluate(0);Save(Path.Combine(dir,"Blend",i.ToString("D4")+".png"),"Side");}}
            finally{graph.Destroy();animator.enabled=false;}
        }
        public void SaveScene(string role,AnimationClip idle,GameObject ar){idle.SampleAnimation(ar,0);camera.targetTexture=null;EditorSceneManager.SaveScene(scene,"Assets/Review/Skeleton_"+role+"_Review.unity");camera.targetTexture=rt;}
        public void Dispose(){camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);}
    }
}
#endif
