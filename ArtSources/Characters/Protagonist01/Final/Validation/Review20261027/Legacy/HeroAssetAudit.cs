#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

// Disposable actual-asset audit. Never saves an asset, importer or scene.
public static class HeroAssetAudit
{
 const string Hero="Assets/Art/Characters/Protagonist01";
 const string Output="ArtSources/Characters/Protagonist01/Final/Validation/Review20261027/Legacy";
 const string Raw="Logs/AssetAudit20261027/Hero";
 const int Size=480, Hz=60;
 [Serializable] public class MeshInfo {public string name,asset;public int vertices,triangles,blendShapes;public string[] bones,materials,shaders;public int missingBones,missingMaterials,invalidIndices,nonFiniteRestVertices;[NonSerialized]public int[] trianglesIndices;[NonSerialized]public int[] dominantBone;[NonSerialized]public Vector3[] restVertices;}
 [Serializable] public class RawMesh {public string name;public int[] triangles,dominantBone;public Vector3[] restVertices;public string[] bones;}
 [Serializable] public class RawSchema {public RawMesh[] meshes;}
 [Serializable] public class StaticModel {public string asset,label,sha256;public MeshInfo[] meshes;public int nonFiniteWorldVertices;public Vector3 boundsCenter,boundsSize;}
 [Serializable] public class Frame {public int index;public float time,minY,maxY,maxVertexStep,maxLocalRotationStep,maxLocalPositionStep;public string rotationNode,positionNode;public int nonFiniteValues,zeroScaleAxes,collapsedTriangles;public Vector3 boundsCenter,boundsSize;public float minTriangleArea;public float maxBoneScale;}
 [Serializable] public class ClipAudit {public string set,variant,clip,clipPath,clipSHA256,prefab,prefabSHA256;public float duration,frameRate,maximumVertexStep,maximumLocalRotationStep,loopSeamVertexDistance;public bool loop;public int samples,bindingCount,missingBindingTargets;public string[] missingBindings;public MeshInfo[] meshes;public Frame[] frames;public string rawMeshFile;}
 [Serializable] public class Render {public string set,variant,clip,view,path;public float time;public int index;public Vector3 cameraPosition,cameraTarget;public float orthoSize;}
 [Serializable] public class Report {public string unityVersion,renderPipeline,graphicsDevice,method;public bool assetsModified,runtimeChanged;public ClipAudit[] clips;public Render[] renders;public string[] errors;}
 static string SHA(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
 static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
 static void Write(string path,object value){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(value,true));}
 static string Safe(string s)=>string.Concat(s.Select(c=>char.IsLetterOrDigit(c)||c=='_'?c:'_'));
 static readonly List<Render> rendered=new List<Render>();

 sealed class Skin : IDisposable {
  public readonly Renderer original;public readonly Mesh baked;public readonly MeshRenderer display;public readonly MeshInfo info;readonly SkinnedMeshRenderer skinned;readonly GameObject copy;readonly bool wasEnabled;public Vector3[] world;
  public Skin(Renderer skin){original=skin;skinned=skin as SkinnedMeshRenderer;var m=skinned?skinned.sharedMesh:skin.GetComponent<MeshFilter>().sharedMesh;wasEnabled=skin.enabled;baked=skinned?new Mesh(){name="DisposableAuditMesh"}:Object.Instantiate(m);baked.MarkDynamic();copy=new GameObject("AuditCpuSkin",typeof(MeshFilter),typeof(MeshRenderer));SceneManager.MoveGameObjectToScene(copy,skin.gameObject.scene);copy.transform.SetParent(skin.transform,false);copy.GetComponent<MeshFilter>().sharedMesh=baked;display=copy.GetComponent<MeshRenderer>();display.sharedMaterials=skin.sharedMaterials;display.shadowCastingMode=ShadowCastingMode.Off;display.receiveShadows=false;skin.enabled=false;
   var weights=m.boneWeights;var dominant=new int[m.vertexCount];for(int i=0;i<Math.Min(weights.Length,dominant.Length);i++){var w=weights[i];var idx=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var values=new[]{w.weight0,w.weight1,w.weight2,w.weight3};dominant[i]=idx[Array.IndexOf(values,values.Max())];}
   info=new MeshInfo{name=skin.name,asset=AssetDatabase.GetAssetPath(m),vertices=m.vertexCount,triangles=m.triangles.Length/3,blendShapes=m.blendShapeCount,bones=skinned?skinned.bones.Select(b=>b?b.name:"MISSING").ToArray():new string[0],materials=skin.sharedMaterials.Select(x=>x?x.name:"MISSING").ToArray(),shaders=skin.sharedMaterials.Select(x=>x&&x.shader?x.shader.name:"MISSING").ToArray(),missingBones=skinned?skinned.bones.Count(x=>!x):0,missingMaterials=skin.sharedMaterials.Count(x=>!x||!x.shader),invalidIndices=m.triangles.Count(x=>x<0||x>=m.vertexCount),nonFiniteRestVertices=m.vertices.Count(x=>!Finite(x)),trianglesIndices=m.triangles,dominantBone=dominant,restVertices=m.vertices};
  }
  public void Bake(){if(skinned)skinned.BakeMesh(baked);baked.RecalculateBounds();world=baked.vertices.Select(v=>original.transform.TransformPoint(v)).ToArray();}
  public void Dispose(){if(original)original.enabled=wasEnabled;Object.DestroyImmediate(copy);Object.DestroyImmediate(baked);}
 }
 sealed class Stage : IDisposable {
  public readonly Scene scene;public readonly Camera camera;readonly RenderTexture rt,previous;readonly Texture2D pixels;readonly Material floorMat;readonly bool async;
  public Stage(){async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;scene=EditorSceneManager.NewPreviewScene();var cam=new GameObject("AuditCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cam,scene);camera=cam.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.aspect=1;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.25f,.29f);camera.nearClipPlane=.02f;camera.farClipPlane=50;camera.allowHDR=false;camera.allowMSAA=true;rt=new RenderTexture(Size,Size,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();camera.targetTexture=rt;previous=RenderTexture.active;pixels=new Texture2D(Size,Size,TextureFormat.RGB24,false);floorMat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));floorMat.SetColor("_BaseColor",new Color(.16f,.185f,.215f));var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);SceneManager.MoveGameObjectToScene(floor,scene);Object.DestroyImmediate(floor.GetComponent<Collider>());floor.transform.localScale=new Vector3(1,1,1);floor.GetComponent<MeshRenderer>().sharedMaterial=floorMat;
   var light=new GameObject("AuditKey",typeof(Light));SceneManager.MoveGameObjectToScene(light,scene);var key=light.GetComponent<Light>();key.type=LightType.Directional;key.color=new Color(1,.96f,.91f);key.intensity=1.5f;key.shadows=LightShadows.None;light.transform.rotation=Quaternion.Euler(40,-30,0);
   var fill=new GameObject("AuditFill",typeof(Light));SceneManager.MoveGameObjectToScene(fill,scene);var f=fill.GetComponent<Light>();f.type=LightType.Directional;f.color=new Color(.72f,.82f,1);f.intensity=.65f;f.shadows=LightShadows.None;fill.transform.rotation=Quaternion.Euler(25,140,0);
  }
  public void View(string view,Vector3 center,float size){Vector3 offset=view=="Side"?new Vector3(1.05f,2,-10):view=="Front"?new Vector3(10,1.6f,0):view=="Opposite"?new Vector3(-1.05f,2,10):new Vector3(7,3,-7);camera.transform.position=center+offset;camera.transform.LookAt(center);camera.orthographicSize=size;}
  public void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path));camera.Render();camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,Size,Size),0,0);pixels.Apply(false,false);File.WriteAllBytes(path,pixels.EncodeToPNG());}
  public void Dispose(){RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);Object.DestroyImmediate(floorMat);EditorSceneManager.ClosePreviewScene(scene);ShaderUtil.allowAsyncCompilation=async;}
 }
 static GameObject Actor(string prefab,Stage stage){var asset=AssetDatabase.LoadAssetAtPath<GameObject>(prefab);if(!asset)throw new Exception("Missing prefab "+prefab);var go=Object.Instantiate(asset);SceneManager.MoveGameObjectToScene(go,stage.scene);go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);go.transform.localScale=Vector3.one;foreach(var a in go.GetComponentsInChildren<Animator>(true))a.enabled=false;return go;}
 static ClipAudit Audit(string set,string variant,string prefab,string clipPath,bool fullRender,int renderHz=30){
  using(var stage=new Stage()){
   var actor=Actor(prefab,stage);var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);if(!clip)throw new Exception("Missing clip "+clipPath);var skins=actor.GetComponentsInChildren<Renderer>(true).Where(s=>s is SkinnedMeshRenderer||s.GetComponent<MeshFilter>()).Select(s=>new Skin(s)).ToArray();if(skins.Length==0)throw new Exception("No actual skin "+prefab);
   var transforms=actor.GetComponentsInChildren<Transform>(true).Where(t=>!t.name.StartsWith("Audit")).ToArray();var bindings=AnimationUtility.GetCurveBindings(clip);var missing=new List<string>();foreach(var b in bindings){var t=string.IsNullOrEmpty(b.path)?actor.transform:actor.transform.Find(b.path);if(!t)missing.Add(b.path+" : "+b.propertyName);else if(b.propertyName.StartsWith("blendShape.")){var skin=t.GetComponent<SkinnedMeshRenderer>();if(!skin||skin.sharedMesh.GetBlendShapeIndex(b.propertyName.Substring(11))<0)missing.Add(b.path+" : "+b.propertyName);}}
   var frames=new List<Frame>();var frameCount=Mathf.RoundToInt(clip.length*Hz)+1;var previousPositions=new Vector3[transforms.Length];var previousRotations=new Quaternion[transforms.Length];Vector3[][] previousVertices=null,firstVertices=null;float maxVertex=0,maxRotation=0,seam=0;string raw=Raw+"/Meshes/"+set+"_"+variant+"_"+clip.name+".bin.gz";Directory.CreateDirectory(Path.GetDirectoryName(raw));
   Write(raw+".schema.json",new RawSchema{meshes=skins.Select(s=>new RawMesh{name=s.info.name,triangles=s.info.trianglesIndices,dominantBone=s.info.dominantBone,restVertices=s.info.restVertices,bones=s.info.bones}).ToArray()});
   try {using(var stream=new FileStream(raw,FileMode.Create,FileAccess.Write))using(var zip=new GZipStream(stream,System.IO.Compression.CompressionLevel.Fastest))using(var writer=new BinaryWriter(zip)){
    writer.Write(frameCount);writer.Write(skins.Length);foreach(var s in skins)writer.Write(s.info.vertices);
    for(int index=0;index<frameCount;index++){
     float time=index==frameCount-1?clip.length:index/(float)Hz;clip.SampleAnimation(actor,time);var frame=new Frame{index=index,time=time,minY=float.PositiveInfinity,maxY=float.NegativeInfinity,minTriangleArea=float.PositiveInfinity};
     for(int j=0;j<transforms.Length;j++){var t=transforms[j];var p=t.localPosition;var r=t.localRotation;var s=t.localScale;if(!Finite(p)||!Finite(s)||!Finite(r.x)||!Finite(r.y)||!Finite(r.z)||!Finite(r.w))frame.nonFiniteValues++;if(Mathf.Abs(s.x)<1e-7f)frame.zeroScaleAxes++;if(Mathf.Abs(s.y)<1e-7f)frame.zeroScaleAxes++;if(Mathf.Abs(s.z)<1e-7f)frame.zeroScaleAxes++;frame.maxBoneScale=Mathf.Max(frame.maxBoneScale,Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z));if(index>0){var angle=Quaternion.Angle(previousRotations[j],r);var dist=Vector3.Distance(previousPositions[j],p);if(angle>frame.maxLocalRotationStep){frame.maxLocalRotationStep=angle;frame.rotationNode=t.name;}if(dist>frame.maxLocalPositionStep){frame.maxLocalPositionStep=dist;frame.positionNode=t.name;}}previousPositions[j]=p;previousRotations[j]=r;}
     writer.Write(time);Bounds bounds=new Bounds();bool initialized=false;var vertices=new Vector3[skins.Length][];
     for(int m=0;m<skins.Length;m++){var skin=skins[m];skin.Bake();vertices[m]=skin.world;foreach(var v in skin.world){if(!Finite(v))frame.nonFiniteValues++;if(!initialized){bounds=new Bounds(v,Vector3.zero);initialized=true;}else bounds.Encapsulate(v);frame.minY=Mathf.Min(frame.minY,v.y);frame.maxY=Mathf.Max(frame.maxY,v.y);writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);}if(previousVertices!=null)for(int j=0;j<skin.world.Length;j++)frame.maxVertexStep=Mathf.Max(frame.maxVertexStep,Vector3.Distance(skin.world[j],previousVertices[m][j]));var tri=skin.info.trianglesIndices;for(int j=0;j<tri.Length;j+=3){float area=Vector3.Cross(skin.world[tri[j+1]]-skin.world[tri[j]],skin.world[tri[j+2]]-skin.world[tri[j]]).magnitude*.5f;frame.minTriangleArea=Mathf.Min(frame.minTriangleArea,area);if(area<1e-10f)frame.collapsedTriangles++;}}
     if(index==0)firstVertices=vertices.Select(v=>(Vector3[])v.Clone()).ToArray();if(index==frameCount-1)for(int m=0;m<vertices.Length;m++)for(int j=0;j<vertices[m].Length;j++)seam=Mathf.Max(seam,Vector3.Distance(firstVertices[m][j],vertices[m][j]));previousVertices=vertices;frame.boundsCenter=bounds.center;frame.boundsSize=bounds.size;frames.Add(frame);maxVertex=Mathf.Max(maxVertex,frame.maxVertexStep);maxRotation=Mathf.Max(maxRotation,frame.maxLocalRotationStep);
    }
   }
   var settings=AnimationUtility.GetAnimationClipSettings(clip);var result=new ClipAudit{set=set,variant=variant,clip=clip.name,clipPath=clipPath,clipSHA256=SHA(clipPath),prefab=prefab,prefabSHA256=SHA(prefab),duration=clip.length,frameRate=clip.frameRate,loop=settings.loopTime,samples=frameCount,bindingCount=bindings.Length,missingBindingTargets=missing.Count,missingBindings=missing.ToArray(),meshes=skins.Select(s=>s.info).ToArray(),frames=frames.ToArray(),maximumVertexStep=maxVertex,maximumLocalRotationStep=maxRotation,loopSeamVertexDistance=seam,rawMeshFile=raw};
   Write(Output+"/Metrics/"+set+"_"+variant+"_"+clip.name+".json",result);
   // Presentation rotates only the disposable parent; no source pose correction.
   actor.transform.rotation=Quaternion.Euler(0,90,0);Vector3 center=set.StartsWith("Combat")?new Vector3(0,.58f,0):new Vector3(0,.78f,0);float size=set.StartsWith("Combat")?.85f:1.05f;
   foreach(string view in fullRender?new[]{"Side","Front","ThreeQuarter","Opposite"}:new[]{"Front","ThreeQuarter"}){
    bool full=fullRender&&view=="Side";int count=full?Mathf.Max(2,Mathf.RoundToInt(clip.length*renderHz)):fullRender?9:3;
    for(int i=0;i<count;i++){float time=full?i/(float)renderHz:clip.length*i/(count-1);clip.SampleAnimation(actor,Mathf.Min(time,clip.length));foreach(var skin in skins)skin.Bake();stage.View(view,center,size);string path=Raw+"/Frames/"+set+"/"+variant+"/"+clip.name+"/"+view+"/"+i.ToString("D4")+".png";stage.Save(path);rendered.Add(new Render{set=set,variant=variant,clip=clip.name,view=view,path=path,time=time,index=i,cameraPosition=stage.camera.transform.position,cameraTarget=center,orthoSize=size});}
   }
   Debug.Log("HERO_AUDITED "+set+" "+variant+" "+clip.name+" frames="+frameCount+" missingBindings="+missing.Count);return result;
   } finally {foreach(var s in skins)s.Dispose();Object.DestroyImmediate(actor);}
  }
 }
 static void AuditStatic(string asset,string label,string meshOverride=null){using(var stage=new Stage()){var actor=Actor(asset,stage);if(meshOverride!=null){var source=AssetDatabase.LoadAssetAtPath<Mesh>(meshOverride);if(!source)throw new Exception("Missing authoring mesh "+meshOverride);foreach(var s in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))s.sharedMesh=source;}actor.transform.rotation=Quaternion.Euler(0,90,0);var skins=actor.GetComponentsInChildren<Renderer>(true).Where(s=>s is SkinnedMeshRenderer||s.GetComponent<MeshFilter>()).Select(s=>new Skin(s)).ToArray();try{foreach(var s in skins)s.Bake();var vertices=skins.SelectMany(s=>s.world).ToArray();if(vertices.Length==0)throw new Exception("Empty retained model "+asset);var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);Write(Output+"/StaticModels/"+label+".json",new StaticModel{asset=asset,label=label,sha256=SHA(asset),meshes=skins.Select(s=>s.info).ToArray(),nonFiniteWorldVertices=vertices.Count(v=>!Finite(v)),boundsCenter=bounds.center,boundsSize=bounds.size});foreach(var view in new[]{"Side","Front","ThreeQuarter","Opposite"}){var center=bounds.center;var size=Mathf.Max(bounds.extents.y,bounds.extents.x,bounds.extents.z)*1.25f;stage.View(view,center,size);var path=Raw+"/Frames/Static/"+label+"/"+view+"/0000.png";stage.Save(path);rendered.Add(new Render{set="Static",variant=label,clip="Rest",view=view,path=path,index=0,time=0,cameraPosition=stage.camera.transform.position,cameraTarget=center,orthoSize=size});}}finally{foreach(var s in skins)s.Dispose();Object.DestroyImmediate(actor);}}}
 public static void RunCombatRetouched(){rendered.Clear();var rows=new List<ClipAudit>();foreach(string clip in Directory.GetFiles(Hero+"/Combat/Clips","*.anim").OrderBy(p=>p))rows.Add(Audit("CombatRetouched","Combat",Hero+"/Combat/Prefabs/Protagonist_Combat.prefab",clip,true,60));Write(Output+"/combat-retouched-audit.json",new Report{unityVersion=Application.unityVersion,renderPipeline=GraphicsSettings.currentRenderPipeline?GraphicsSettings.currentRenderPipeline.name:"BuiltIn",graphicsDevice=SystemInfo.graphicsDeviceName,method="Targeted saved-prefab post-weight-fix validation. Every 60 Hz pose and full Side at 60 Hz, nine phases in other views. No motion or geometry corrections during sampling/render.",assetsModified=false,runtimeChanged=false,clips=rows.ToArray(),renders=rendered.ToArray(),errors=new string[0]});Debug.Log("COMBAT_RETOUCHED_UNITY_READY");}
 public static void Run(){
  Directory.CreateDirectory(Output);Directory.CreateDirectory(Raw);var rows=new List<ClipAudit>();var errors=new List<string>();
  try {
   string legacy=Hero+"/ChibiMotion_v007";foreach(string variant in new[]{"MotionReview","SwordGrip","StaffGrip"})foreach(string clip in Directory.GetFiles(legacy+"/Clips","*.anim").OrderBy(p=>p))rows.Add(Audit("Legacy",variant,legacy+"/Prefabs/Protagonist_Chibi_"+variant+".prefab",clip,variant=="MotionReview"));
   foreach(string clip in Directory.GetFiles(Hero+"/Combat/Clips","*.anim").OrderBy(p=>p))rows.Add(Audit("Combat","Combat",Hero+"/Combat/Prefabs/Protagonist_Combat.prefab",clip,true));
   AuditStatic(Hero+"/ChibiCap_v006/Prefabs/Protagonist_ChibiCap_Rig.prefab","V006_RigPrefab");AuditStatic(Hero+"/ChibiCap_v006/Rig/Protagonist_ChibiCap_Rig.fbx","V006_ImportedRest");AuditStatic(Hero+"/ChibiCap_v006/Prefabs/Protagonist_ChibiCap_Rig.prefab","V006_FistsSource",Hero+"/ChibiCap_v006/Meshes/Protagonist_ChibiCap_Fists.asset");AuditStatic(Hero+"/Runtime/Protagonist01_Visual.prefab","ExistingRuntimeReference");
  }catch(Exception e){errors.Add(e.ToString());Debug.LogException(e);}
  Write(Output+"/hero-audit.json",new Report{unityVersion=Application.unityVersion,renderPipeline=GraphicsSettings.currentRenderPipeline?GraphicsSettings.currentRenderPipeline.name:"BuiltIn",graphicsDevice=SystemInfo.graphicsDeviceName,method="New 60 Hz actual saved-clip samples on loaded prefabs. All vertices of actual BakeMesh output checked; disposable CPU-skinned copies rendered at 30 Hz side and phase-sampled other views. No neutral-pose re-authoring, foot correction, interpolation smoothing or runtime integration.",assetsModified=false,runtimeChanged=false,clips=rows.ToArray(),renders=rendered.ToArray(),errors=errors.ToArray()});
  if(errors.Count>0)throw new Exception("Hero audit incomplete: "+errors[0]);Debug.Log("HERO_ASSET_AUDIT_READY "+rows.Count);
 }
}
#endif
