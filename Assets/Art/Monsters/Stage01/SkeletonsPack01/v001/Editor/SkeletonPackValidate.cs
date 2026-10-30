#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Read-only inspection of already published main-project assets.
// Does not call the factory, reimport assets, change importers or save prefabs.
public static class SkeletonPackValidate
{
    public const string Root="Assets/Art/Monsters/Stage01/SkeletonsPack01/v001";
    const string SourceRelative="ArtSources/Monsters/Stage01/SkeletonsPack01/v001-original";
    const float WrapperScale=.585289224f;
    static readonly string[] Roles={"Rogue","Warrior","Mage"};
    static readonly string[] Weapons={"Skeleton_Crossbow","Skeleton_Axe","Skeleton_Staff"};
    static readonly string[] NativeBones={"root","hips","spine","chest","upperarm.l","lowerarm.l","wrist.l","hand.l","handslot.l","upperarm.r","lowerarm.r","wrist.r","hand.r","handslot.r","head","upperleg.l","lowerleg.l","foot.l","toes.l","upperleg.r","lowerleg.r","foot.r","toes.r","kneeIK.l","control-toe-roll.l","control-heel-roll.l","control-foot-roll.l","heelIK.l","IK-foot.l","IK-toe.l","kneeIK.r","control-toe-roll.r","control-heel-roll.r","control-foot-roll.r","heelIK.r","IK-foot.r","IK-toe.r","elbowIK.l","handIK.l","elbowIK.r","handIK.r"};
    static string Project=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));
    static string Source=>Path.Combine(Project,SourceRelative);

    [Serializable] public sealed class ProtectedFile {public string path,sha256,actualSHA256;public bool matches;}
    [Serializable] sealed class ProtectedList {public ProtectedFile[] files;}
    [Serializable] public sealed class ClipCheck {public string path,name,sourceTake,sourceCurveSHA256,savedCurveSHA256;public float seconds,sourceSeconds,frameRate;public int curveBindings;public bool curvesIdentical,loop;}
    [Serializable] public sealed class RoleCheck {public string role,sourceFBX,prefab,sourceSHA256,rawSHA256,accessoryFBX,accessorySHA256,rawAccessorySHA256,socketPath;public bool success,sourceBytesIdentical,accessoryBytesIdentical,controllerNull,avatarMatchesSource,missingScriptsAbsent,materialsComplete,meshesAndBindposesReferenceOriginal;public int embeddedTakes,originalBonesChecked,nativeNodesChecked,meshReferencesChecked,skinBindingsChecked,selectedClips;public float maximumLocalPositionDifference,maximumLocalScaleDifference,maximumQuaternionComponentDifference;public Vector3 wrapperScale,attachmentPosition,attachmentEuler,accessoryImportedScale;public ClipCheck[] clips;}
    [Serializable] public sealed class Report {public bool success,readOnly,sharedShaderSupported,visualApproval;public string project,unity,graphicsDevice,method,sharedShader,atlasSHA256,rawAtlasSHA256,scope;public int protectedFilesChecked,selectedClips;public RoleCheck[] roles;public ProtectedFile[] protectedFiles;public string[] errors;}

    [MenuItem("Project M/Art/Inspect Published Original Skeletons")]
    public static void Inspect()
    {
        var report=new Report{project=Project,unity=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceType.ToString(),readOnly=true,visualApproval=false,method="Read saved main-project assets only. Compare every original native transform rest TRS, exact persistent FBX mesh references (including original bind-pose storage), skin bone/root mappings, all selected saved animation keys/tangents/weights, raw FBX/atlas hashes and protected pre-existing file hashes. No factory calls, asset reimports, importer changes or prefab saves.",scope="Structural and persistence validation only. Exact mesh-reference identity proves that the prefab uses the original imported FBX mesh and its bind-pose data; no replacement mesh or repaired skin buffer is accepted. This is not a replacement for actual skinned-model visual review or game Animator integration."};
        var errors=new List<string>();var roles=new List<RoleCheck>();var protectedFiles=new List<ProtectedFile>();
        try
        {
            var shader=Shader.Find("ProjectM/Skeleton Native Atlas");report.sharedShader=shader?shader.name:null;report.sharedShaderSupported=shader&&shader.isSupported;
            Require(report.sharedShaderSupported,"Shared original-atlas shader is missing/unsupported in this main project.");
            Require(!ShaderUtil.GetShaderMessages(shader).Any(m=>m.severity.ToString()=="Error"),"Shared shader has compiler errors.");
            string atlas=FindSingle("skeleton_texture",".png");report.atlasSHA256=Hash(atlas);report.rawAtlasSHA256=Hash(Path.Combine(Source,"SourceAssets/Textures/skeleton_texture.png"));Require(report.atlasSHA256==report.rawAtlasSHA256,"Published atlas bytes differ from the original PNG.");
        }
        catch(Exception ex){errors.Add("Shared resources: "+ex.Message);}
        for(int i=0;i<Roles.Length;i++)
        {
            var role=new RoleCheck{role=Roles[i],sourceFBX=Root+"/FBX/Skeleton_"+Roles[i]+".fbx",prefab=Root+"/Prefabs/Skeleton_"+Roles[i]+".prefab"};roles.Add(role);
            try{InspectRole(role,Weapons[i]);role.success=true;}
            catch(Exception ex){errors.Add(role.role+": "+ex.Message);}
        }
        try
        {
            string path=Path.Combine(Source,"QA/protected-reference-hashes.json");var baseline=JsonUtility.FromJson<ProtectedList>("{\"files\":"+File.ReadAllText(path)+"}");
            Require(baseline!=null&&baseline.files!=null&&baseline.files.Length==22,"Expected the22 pre-existing protected-file hash records.");
            foreach(var file in baseline.files)
            {
                protectedFiles.Add(file);string absolute=Path.GetFullPath(Path.Combine(Project,file.path));Require(absolute.StartsWith(Project+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Protected path escapes the project: "+file.path);
                file.actualSHA256=File.Exists(absolute)?Hash(absolute):null;file.matches=string.Equals(file.sha256,file.actualSHA256,StringComparison.OrdinalIgnoreCase);
                if(!file.matches)errors.Add("Protected file changed/missing: "+file.path);
            }
        }
        catch(Exception ex){errors.Add("Protected references: "+ex.Message);}
        report.roles=roles.ToArray();report.protectedFiles=protectedFiles.ToArray();report.protectedFilesChecked=protectedFiles.Count;report.selectedClips=roles.Sum(r=>r.selectedClips);
        if(report.selectedClips!=16)errors.Add("Expected16 selected saved clips across the actual three role folders; found "+report.selectedClips);
        report.errors=errors.ToArray();report.success=errors.Count==0;
        string output=Path.Combine(Source,"QA/main-project-import-validation.json");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
        if(report.success)Debug.Log("SKELETON_MAIN_READONLY_VALIDATION_SUCCESS "+output);
        else{Debug.LogError("SKELETON_MAIN_READONLY_VALIDATION_FAILED "+string.Join("; ",errors)+" Report: "+output);if(Application.isBatchMode)EditorApplication.Exit(1);}
    }

    static void InspectRole(RoleCheck row,string weapon)
    {
        row.sourceSHA256=Hash(row.sourceFBX);row.rawSHA256=Hash(Path.Combine(Source,"SourceAssets/Characters/fbx/Skeleton_"+row.role+".fbx"));row.sourceBytesIdentical=row.sourceSHA256==row.rawSHA256;Require(row.sourceBytesIdentical,"Main FBX bytes do not match the immutable raw source.");
        var importer=AssetImporter.GetAtPath(row.sourceFBX) as ModelImporter;Require(importer&&importer.animationType==ModelImporterAnimationType.Generic&&importer.importAnimation,"Original FBX must be Generic with embedded animations.");
        Require(importer.animationCompression==ModelImporterAnimationCompression.Off&&importer.meshCompression==ModelImporterMeshCompression.Off,"Unexpected animation/mesh compression.");
        Require(importer.preserveHierarchy&&!importer.optimizeGameObjects&&importer.useFileScale,"Hierarchy/file-unit import contract changed.");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(row.sourceFBX);Require(source,"Missing imported native source model.");
        var takes=AssetDatabase.LoadAllAssetsAtPath(row.sourceFBX).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).ToArray();row.embeddedTakes=takes.Length;Require(takes.Length==95,"Expected95 original embedded takes, found "+takes.Length);
        var originalTransforms=source.GetComponentsInChildren<Transform>(true);
        foreach(string name in NativeBones){Require(originalTransforms.Count(t=>t.name==name)==1,"Original41-bone schema is missing/ambiguous: "+name);row.originalBonesChecked++;}

        string folder=Root+"/Clips/"+row.role;var paths=AssetDatabase.FindAssets("t:AnimationClip",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith(".anim",StringComparison.OrdinalIgnoreCase)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
        Require(paths.Length>0,"No saved role clips.");var clips=new List<ClipCheck>();
        foreach(string path in paths)
        {
            var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);Require(saved,"Saved clip failed to load: "+path);string name=Path.GetFileNameWithoutExtension(path);
            Require(saved.name==name,"Saved clip name differs from its original-name filename: "+path);var matches=takes.Where(c=>NativeName(c.name)==name).ToArray();Require(matches.Length==1,"Saved clip has no unique same-name original take: "+name);
            var original=matches[0];var check=new ClipCheck{path=path,name=name,sourceTake=original.name,seconds=saved.length,sourceSeconds=original.length,frameRate=saved.frameRate,curveBindings=AnimationUtility.GetCurveBindings(saved).Length,loop=AnimationUtility.GetAnimationClipSettings(saved).loopTime,sourceCurveSHA256=CurveHash(original),savedCurveSHA256=CurveHash(saved)};
            check.curvesIdentical=check.sourceCurveSHA256==check.savedCurveSHA256;Require(check.curvesIdentical,"Native key/time/tangent/weight mismatch: "+name);Require(Mathf.Abs(saved.length-original.length)<1e-5f,"Native duration mismatch: "+name);clips.Add(check);
        }
        row.clips=clips.ToArray();row.selectedClips=clips.Count;
        row.accessoryFBX=FindSingle(weapon,".fbx");row.accessorySHA256=Hash(row.accessoryFBX);row.rawAccessorySHA256=Hash(Path.Combine(Source,"SourceAssets/Accessories/fbx/"+weapon+".fbx"));row.accessoryBytesIdentical=row.accessorySHA256==row.rawAccessorySHA256;Require(row.accessoryBytesIdentical,"Accessory FBX differs from the original raw source.");
        var accessorySource=AssetDatabase.LoadAssetAtPath<GameObject>(row.accessoryFBX);Require(accessorySource,"Expected native accessory model is missing.");
        var wrapper=PrefabUtility.LoadPrefabContents(row.prefab);
        try
        {
            row.wrapperScale=wrapper.transform.localScale;Require((row.wrapperScale-Vector3.one*WrapperScale).sqrMagnitude<1e-12f,"External game-unit wrapper scale differs.");
            var candidates=wrapper.transform.Cast<Transform>().Where(t=>HasMeshFrom(t.gameObject,row.sourceFBX)).ToArray();Require(candidates.Length==1,"Expected one original native model directly below the wrapper.");var native=candidates[0];
            var nativeAnimator=native.GetComponent<Animator>();Require(nativeAnimator,"Animator must be on the native model child.");
            var sourceAnimator=source.GetComponent<Animator>();row.avatarMatchesSource=sourceAnimator&&nativeAnimator.avatar==sourceAnimator.avatar;Require(row.avatarMatchesSource,"Native Generic Avatar reference changed.");
            row.controllerNull=wrapper.GetComponentsInChildren<Animator>(true).All(a=>!a.runtimeAnimatorController);Require(row.controllerNull,"A gameplay Animator controller was attached.");
            string socketName=row.role=="Mage"?"handslot.l":"handslot.r";
            var slots=native.GetComponentsInChildren<Transform>(true).Where(t=>t.name==socketName).ToArray();Require(slots.Length==1,"Original accessory socket is missing/ambiguous.");row.socketPath=AnimationUtility.CalculateTransformPath(slots[0],native);
            string framePath=row.socketPath+"/"+weapon+"_NativeSocketFrame";var frame=native.Find(framePath);Require(frame,"Expected static accessory wrapper is missing from the socket head.");row.attachmentPosition=frame.localPosition;row.attachmentEuler=frame.localEulerAngles;
            var accessoryCandidates=frame.Cast<Transform>().Where(t=>HasMeshFrom(t.gameObject,row.accessoryFBX)).ToArray();Require(accessoryCandidates.Length==1,"Expected one original accessory model under its static wrapper.");row.accessoryImportedScale=accessoryCandidates[0].localScale;
            CompareNative(source.transform,native,row,framePath);CompareNative(accessorySource.transform,accessoryCandidates[0],row,null);row.meshesAndBindposesReferenceOriginal=true;
            row.missingScriptsAbsent=wrapper.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0);Require(row.missingScriptsAbsent,"Prefab has a missing script.");
            var renderers=wrapper.GetComponentsInChildren<Renderer>(true);row.materialsComplete=renderers.Length>0&&renderers.All(r=>r.sharedMaterials.Length>0&&r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported&&m.shader.name=="ProjectM/Skeleton Native Atlas"&&AssetDatabase.GetAssetPath(m).StartsWith(Root+"/Materials/",StringComparison.Ordinal)));
            Require(row.materialsComplete,"Prefab has missing, unsupported or unexpected shared materials.");
        }
        finally{PrefabUtility.UnloadPrefabContents(wrapper);}
    }

    static void CompareNative(Transform original,Transform actual,RoleCheck row,string allowedExtraPrefix)
    {
        var sourceNodes=original.GetComponentsInChildren<Transform>(true);var paths=new HashSet<string>(sourceNodes.Select(t=>AnimationUtility.CalculateTransformPath(t,original)));
        foreach(var sourceNode in sourceNodes)
        {
            string path=AnimationUtility.CalculateTransformPath(sourceNode,original);var node=path.Length==0?actual:actual.Find(path);Require(node,"Native hierarchy path missing: "+path);row.nativeNodesChecked++;
            float position=Vector3.Distance(sourceNode.localPosition,node.localPosition),scale=Vector3.Distance(sourceNode.localScale,node.localScale),rotation=QuaternionDifference(sourceNode.localRotation,node.localRotation);
            row.maximumLocalPositionDifference=Mathf.Max(row.maximumLocalPositionDifference,position);row.maximumLocalScaleDifference=Mathf.Max(row.maximumLocalScaleDifference,scale);row.maximumQuaternionComponentDifference=Mathf.Max(row.maximumQuaternionComponentDifference,rotation);
            Require(position<=1e-6f&&scale<=1e-6f&&rotation<=2e-6f,"Original rest/local TRS changed: "+path);
            var sourceSkin=sourceNode.GetComponent<SkinnedMeshRenderer>();if(sourceSkin)
            {
                var skin=node.GetComponent<SkinnedMeshRenderer>();Require(skin,"Original skinned renderer missing: "+path);Require(SamePersistentMesh(sourceSkin.sharedMesh,skin.sharedMesh),"Native skinned mesh/bind-pose storage replaced: "+path);row.meshReferencesChecked++;
                Require(BonePath(sourceSkin.rootBone,original)==BonePath(skin.rootBone,actual),"Original skin root-bone mapping changed: "+path);
                var before=sourceSkin.bones.Select(b=>BonePath(b,original)).ToArray();var after=skin.bones.Select(b=>BonePath(b,actual)).ToArray();Require(before.SequenceEqual(after),"Skin bone order/mapping changed: "+path);row.skinBindingsChecked+=before.Length;
            }
            var sourceFilter=sourceNode.GetComponent<MeshFilter>();if(sourceFilter&&sourceFilter.sharedMesh){var filter=node.GetComponent<MeshFilter>();Require(filter&&SamePersistentMesh(sourceFilter.sharedMesh,filter.sharedMesh),"Original rigid mesh storage replaced: "+path);row.meshReferencesChecked++;}
        }
        foreach(var node in actual.GetComponentsInChildren<Transform>(true))
        {
            string path=AnimationUtility.CalculateTransformPath(node,actual);if(paths.Contains(path))continue;
            Require(allowedExtraPrefix!=null&&(path==allowedExtraPrefix||path.StartsWith(allowedExtraPrefix+"/",StringComparison.Ordinal)),"Unexpected extra native hierarchy node outside accessory wrapper: "+path);
        }
    }
    static bool HasMeshFrom(GameObject actor,string source){return actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(s=>s.sharedMesh&&AssetDatabase.GetAssetPath(s.sharedMesh)==source)||actor.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh&&AssetDatabase.GetAssetPath(f.sharedMesh)==source);}
    static bool SamePersistentMesh(Mesh a,Mesh b)
    {
        if(!a||!b||a!=b)return false;string ag,bg;long ai,bi;
        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a,out ag,out ai)&&AssetDatabase.TryGetGUIDAndLocalFileIdentifier(b,out bg,out bi)&&ag==bg&&ai==bi;
    }
    static string BonePath(Transform bone,Transform root){return bone?AnimationUtility.CalculateTransformPath(bone,root):"<null>";}
    static float QuaternionDifference(Quaternion a,Quaternion b){float sign=Quaternion.Dot(a,b)<0?-1:1,result=0;for(int i=0;i<4;i++)result=Mathf.Max(result,Mathf.Abs(a[i]-sign*b[i]));return result;}
    static string NativeName(string name){int index=name.LastIndexOf('|');return index<0?name:name.Substring(index+1);}
    static string FindSingle(string name,string extension){var paths=AssetDatabase.FindAssets(name,new[]{Root}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>string.Equals(Path.GetFileName(p),name+extension,StringComparison.OrdinalIgnoreCase)).ToArray();Require(paths.Length==1,"Expected one "+name+extension+", found "+paths.Length);return paths[0];}
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    static string CurveHash(AnimationClip clip)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            foreach(var binding in AnimationUtility.GetCurveBindings(clip).OrderBy(b=>b.path,StringComparer.Ordinal).ThenBy(b=>b.propertyName,StringComparer.Ordinal).ThenBy(b=>b.type.FullName,StringComparer.Ordinal))
            {
                writer.Write(binding.path);writer.Write(binding.propertyName);writer.Write(binding.type.FullName);var curve=AnimationUtility.GetEditorCurve(clip,binding);writer.Write((int)curve.preWrapMode);writer.Write((int)curve.postWrapMode);writer.Write(curve.length);
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
