#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Re-runnable editor import of immutable original KayKit FBXs.
// No generated poses, retargeting, mesh edits, bone edits or curve rewrites.
public static class SkeletonPackImport
{
    public const string Root="Assets/Art/Monsters/Stage01/SkeletonsPack01/v001";
    public const float WrapperScale=.585289224f;
    const string ShaderName="ProjectM/Skeleton Native Atlas";
    static readonly Color NativeYellow=new Color(1f,1f,.185752083f,1f); // pinned original glTF Glow.emissiveFactor
    static readonly string[] Roles={"Rogue","Warrior","Mage"};
    static readonly string[][] Clips={
        new[]{"Idle_Combat","Running_A","2H_Ranged_Aiming","2H_Ranged_Shoot","2H_Ranged_Reload","Hit_A"},
        new[]{"Idle_Combat","Walking_B","1H_Melee_Attack_Chop","1H_Melee_Attack_Slice_Diagonal","Hit_A"},
        new[]{"Idle_Combat","Walking_D_Skeletons","Spellcast_Shoot","Spellcast_Raise","Hit_A"}
    };
    static readonly string[] Weapons={"Skeleton_Crossbow","Skeleton_Axe","Skeleton_Staff"};

    [Serializable] public sealed class ClipReport { public string name,path,sourceTakeName,sourceCurveSHA256,savedCurveSHA256;public float seconds,frameRate;public int bindings;public bool loop,curvesIdentical; }
    [Serializable] public sealed class RoleReport { public string role,source,sourceSHA256,prefab,accessory,accessorySHA256,socketPath;public int embeddedClipCount,nativeNodes,nativeSkinRenderers,uniqueSkinBones;public bool sourceBytesUnchanged,avatarPresent;public Vector3 socketLocalPosition,accessoryImportedPosition,accessoryImportedEuler,accessoryImportedScale,attachmentEuler;public Quaternion socketLocalRotation;public ClipReport[] clips; }
    [Serializable] public sealed class ImportReport { public string unity,method,attachmentScope,root;public float wrapperScale;public bool success,geometryEdited,rigEdited,animationKeysEdited,gameControllerCreated,visualApproval;public RoleReport[] roles;public Color glowColor; }

    [MenuItem("Project M/Art/Import Original Skeletons Pack")]
    public static void Build()
    {
        try
        {
            foreach(string folder in new[]{"Materials","Prefabs","Clips"})Directory.CreateDirectory(Root+"/"+folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shader=Shader.Find(ShaderName);if(!shader||!shader.isSupported)throw new InvalidOperationException("Missing or unsupported shared source atlas shader: "+ShaderName);
            string atlasPath=FindSingle("skeleton_texture",".png");
            ConfigureAtlas(atlasPath);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);if(!texture)throw new InvalidOperationException("Native atlas failed import.");
            var body=SharedMaterial("Skeleton_Native_Atlas",shader,texture,Color.white);
            var glow=SharedMaterial("Skeleton_Native_Glow",shader,null,NativeYellow);
            var results=new List<RoleReport>();
            for(int index=0;index<Roles.Length;index++)
            {
                string role=Roles[index],source=Root+"/FBX/Skeleton_"+role+".fbx",sourceHash=HashFile(source),weaponPath=FindSingle(Weapons[index],".fbx"),weaponHash=HashFile(weaponPath);
                ConfigureFBX(source,true);ConfigureFBX(weaponPath,false);
                var model=AssetDatabase.LoadAssetAtPath<GameObject>(source);if(!model)throw new InvalidOperationException("Native model missing after import: "+source);
                var nativeClips=AssetDatabase.LoadAllAssetsAtPath(source).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).ToArray();
                if(nativeClips.Length!=95)throw new InvalidOperationException("Original95 takes must be retained: "+role+" has "+nativeClips.Length);
                Directory.CreateDirectory(Root+"/Clips/"+role);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var clipReports=new List<ClipReport>();
                foreach(string name in Clips[index])
                {
                    var matches=nativeClips.Where(c=>NativeName(c.name)==name).ToArray();if(matches.Length!=1)throw new InvalidOperationException("Ambiguous/missing native take "+role+"/"+name+" from "+string.Join(",",nativeClips.Select(c=>c.name)));
                    var original=matches[0];string path=Root+"/Clips/"+role+"/"+name+".anim";string originalCurveHash=CurveHash(original);
                    var copy=Object.Instantiate(original);copy.name=name;
                    var settings=AnimationUtility.GetAnimationClipSettings(copy);settings.loopTime=IsLoop(name);AnimationUtility.SetAnimationClipSettings(copy,settings);
                    var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if(existing){EditorUtility.CopySerialized(copy,existing);existing.name=name;EditorUtility.SetDirty(existing);Object.DestroyImmediate(copy);}else AssetDatabase.CreateAsset(copy,path);
                    AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                    var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);string savedHash=CurveHash(saved);
                    if(originalCurveHash!=savedHash)throw new InvalidOperationException("Native curve fidelity failed: "+path);
                    if(Mathf.Abs(saved.length-original.length)>1e-5f)throw new InvalidOperationException("Native duration changed: "+path);
                    clipReports.Add(new ClipReport{name=name,path=path,sourceTakeName=original.name,sourceCurveSHA256=originalCurveHash,savedCurveSHA256=savedHash,seconds=saved.length,frameRate=saved.frameRate,bindings=AnimationUtility.GetCurveBindings(saved).Length,loop=IsLoop(name),curvesIdentical=true});
                }
                var wrapper=new GameObject("Skeleton_"+role);wrapper.transform.localScale=Vector3.one*WrapperScale;
                try
                {
                    var native=Object.Instantiate(model);native.name="Skeleton_"+role;native.transform.SetParent(wrapper.transform,false); // keep imported native root TRS
                    var animator=native.GetComponent<Animator>();if(!animator)animator=native.AddComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.enabled=true;
                    AssignMaterials(native,body,glow);
                    var skins=native.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    string socketName=role=="Mage"?"handslot.l":"handslot.r";
                    var sockets=native.GetComponentsInChildren<Transform>(true).Where(t=>t.name==socketName).ToArray();if(sockets.Length!=1)throw new InvalidOperationException("Expected one original hand slot in "+role);
                    var socket=sockets[0];int nativeNodes=native.GetComponentsInChildren<Transform>(true).Length;
                    var accessoryFrame=new GameObject(Weapons[index]+"_NativeSocketFrame");accessoryFrame.transform.SetParent(socket,false); // Unity Transform origin is the socket head, no Blender tail offset
                    // Verified static attachment axes, separate from every original bone and curve.
                    var attachmentEuler=index==0?new Vector3(0,-90,0):index==1?Vector3.zero:new Vector3(0,0,90);
                    accessoryFrame.transform.localRotation=Quaternion.Euler(attachmentEuler);
                    var accessory=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(weaponPath));accessory.name=Weapons[index];accessory.transform.SetParent(accessoryFrame.transform,false); // original FBX root orientation and file-unit scale remain intact
                    AssignMaterials(accessory,body,glow);
                    string prefab=Root+"/Prefabs/Skeleton_"+role+".prefab";bool savedPrefab;PrefabUtility.SaveAsPrefabAsset(wrapper,prefab,out savedPrefab);if(!savedPrefab)throw new IOException("Prefab save failed: "+prefab);
                    results.Add(new RoleReport{role=role,source=source,sourceSHA256=sourceHash,prefab=prefab,accessory=weaponPath,accessorySHA256=weaponHash,socketPath=AnimationUtility.CalculateTransformPath(socket,native.transform),embeddedClipCount=nativeClips.Length,nativeNodes=nativeNodes,nativeSkinRenderers=skins.Length,uniqueSkinBones=skins.SelectMany(s=>s.bones).Distinct().Count(),sourceBytesUnchanged=sourceHash==HashFile(source)&&weaponHash==HashFile(weaponPath),avatarPresent=animator.avatar,socketLocalPosition=socket.localPosition,socketLocalRotation=socket.localRotation,accessoryImportedPosition=accessory.transform.localPosition,accessoryImportedEuler=accessory.transform.localEulerAngles,accessoryImportedScale=accessory.transform.localScale,attachmentEuler=attachmentEuler,clips=clipReports.ToArray()});
                }
                finally{Object.DestroyImmediate(wrapper);}
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var report=new ImportReport{success=true,unity=Application.unityVersion,root=Root,wrapperScale=WrapperScale,glowColor=NativeYellow,geometryEdited=false,rigEdited=false,animationKeysEdited=false,gameControllerCreated=false,visualApproval=false,roles=results.ToArray(),method="Direct original FBX Generic import, all95 embedded takes retained, no animation or mesh compression, hierarchy/file units preserved. Selected clips retain native names and exactly identical imported curve keys/tangents/weights. Only clip loop metadata, shared original atlas material, external prefab scale and static accessory-parent wrappers are set.",attachmentScope="Static accessory wrappers: Crossbow at right handslot Y=-90; Axe at right handslot identity; Staff at left handslot Z=+90 to leave the casting hand free. Native accessory geometry/root TRS retained. Warrior uses original one-handed clips suitable for the original short axe. Rogue idle/hit crossbow ground intersection and Mage staff/hat proximity remain under unedited source motions. See visual QA; structural fidelity is not animation-quality approval."};
            string reviewRoot=Environment.GetEnvironmentVariable("SKELETON_REVIEW_ROOT");string reportPath=string.IsNullOrWhiteSpace(reviewRoot)?Path.GetFullPath("Temp/SkeletonPackImportReport.json"):Path.Combine(Path.GetFullPath(reviewRoot),"QA","unity-import-report.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
            Debug.Log("SKELETON_ORIGINAL_IMPORT_SUCCESS "+reportPath);
        }
        catch(Exception exception){Debug.LogException(exception);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }

    static void ConfigureFBX(string path,bool animation)
    {
        if(!File.Exists(path))throw new FileNotFoundException("Expected original pinned FBX",path);
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
        var importer=AssetImporter.GetAtPath(path) as ModelImporter;if(!importer)throw new InvalidOperationException("Missing FBX importer: "+path);
        importer.animationType=animation?ModelImporterAnimationType.Generic:ModelImporterAnimationType.None;importer.avatarSetup=animation?ModelImporterAvatarSetup.CreateFromThisModel:ModelImporterAvatarSetup.NoAvatar;
        importer.importAnimation=animation;importer.clipAnimations=Array.Empty<ModelImporterClipAnimation>();importer.animationCompression=ModelImporterAnimationCompression.Off;importer.resampleCurves=false;
        importer.optimizeGameObjects=false;importer.preserveHierarchy=true;importer.globalScale=1;importer.useFileScale=true;importer.bakeAxisConversion=false;
        importer.meshCompression=ModelImporterMeshCompression.Off;importer.optimizeMeshPolygons=false;importer.optimizeMeshVertices=false;importer.weldVertices=false;
        importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.Import;importer.importBlendShapes=true;
        importer.skinWeights=ModelImporterSkinWeights.Custom;importer.maxBonesPerVertex=255;importer.minBoneWeight=0;
        importer.importCameras=false;importer.importLights=false;importer.importConstraints=false;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
    }
    static void ConfigureAtlas(string path)
    {
        var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new InvalidOperationException("Expected source atlas TextureImporter: "+path);
        importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
    }
    static Material SharedMaterial(string name,Shader shader,Texture atlas,Color color)
    {
        string path=Root+"/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);bool create=!material;if(create)material=new Material(shader){name=name};else material.shader=shader;
        if(material.HasProperty("_Color"))material.SetColor("_Color",color);if(material.HasProperty("_MainTex"))material.SetTexture("_MainTex",atlas);
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",atlas);
        if(create)AssetDatabase.CreateAsset(material,path);else EditorUtility.SetDirty(material);return material;
    }
    static void AssignMaterials(GameObject actor,Material body,Material glow)
    {
        foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m&&m.name.IndexOf("Glow",StringComparison.OrdinalIgnoreCase)>=0?glow:body).ToArray();
            if(renderer is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;
        }
    }
    static bool IsLoop(string name){return name.IndexOf("Idle",StringComparison.Ordinal)>=0||name.StartsWith("Walking",StringComparison.Ordinal)||name.StartsWith("Running",StringComparison.Ordinal);}
    static string NativeName(string name){int separator=name.LastIndexOf('|');return separator<0?name:name.Substring(separator+1);}
    static string FindSingle(string name,string extension)
    {
        string[] matches=AssetDatabase.FindAssets(name,new[]{Root}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>string.Equals(Path.GetFileName(p),name+extension,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(matches.Length!=1)throw new InvalidOperationException("Expected one native "+name+extension+" under "+Root+", found "+matches.Length);return matches[0];
    }
    static string HashFile(string path){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
    static string CurveHash(AnimationClip clip)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            foreach(var binding in AnimationUtility.GetCurveBindings(clip).OrderBy(b=>b.path,StringComparer.Ordinal).ThenBy(b=>b.propertyName,StringComparer.Ordinal).ThenBy(b=>b.type.FullName,StringComparer.Ordinal))
            {
                writer.Write(binding.path);writer.Write(binding.propertyName);writer.Write(binding.type.FullName);
                var curve=AnimationUtility.GetEditorCurve(clip,binding);writer.Write((int)curve.preWrapMode);writer.Write((int)curve.postWrapMode);writer.Write(curve.length);
                foreach(var key in curve.keys){writer.Write(key.time);writer.Write(key.value);writer.Write(key.inTangent);writer.Write(key.outTangent);writer.Write(key.inWeight);writer.Write(key.outWeight);writer.Write((int)key.weightedMode);}
            }
            foreach(var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip).OrderBy(b=>b.path,StringComparer.Ordinal).ThenBy(b=>b.propertyName,StringComparer.Ordinal))
            {
                writer.Write(binding.path);writer.Write(binding.propertyName);foreach(var key in AnimationUtility.GetObjectReferenceCurve(clip,binding)){writer.Write(key.time);writer.Write(key.value?AssetDatabase.GetAssetPath(key.value):string.Empty);}
            }
            writer.Flush();using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
        }
    }
}
#endif
