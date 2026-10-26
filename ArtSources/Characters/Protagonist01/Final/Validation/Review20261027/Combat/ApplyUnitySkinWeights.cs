#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class ApplyUnitySkinWeights {
 const string Input="Logs/AssetAudit20261027/SkinSolver/unity-weight-patch.json";
 const string MeshPath="Assets/Art/Characters/Protagonist01/Combat/Meshes/Protagonist_Combat_ElbowWeights.asset";
 const string ReportPath="ArtSources/Characters/Protagonist01/Final/Validation/Review20261027/Combat/unity-weight-application.json";
 [Serializable] public class Weight {public int boneIndex;public float weight;}
 [Serializable] public class Patch {public int vertex,nativeVertex;public Weight[] weights;}
 [Serializable] public class InputData {public string prefab,expectedPrefabSHA256,meshName,sourceCandidateSHA256,sourceOriginalSHA256;public int vertexCount;public string[] bones;public Patch[] patches;}
 [Serializable] public class Report {public string prefab,prefabBeforeSHA256,prefabAfterSHA256,meshAsset,meshAssetSHA256,originalMeshAsset,sourceCandidateSHA256,sourceOriginalSHA256;public int vertexCount,patchedVertices,unchangedVertices,actualChangedVertices,maximumInfluences;public bool geometryBindposesUVNormalsExactlyPreserved,allWeightsFiniteNonNegative;public float maximumUnpatchedWeightDifference,maximumNormalizationError;public string[] boneOrder;}
 static string SHA(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
 static List<BoneWeight1>[] Weights(Mesh m){var counts=m.GetBonesPerVertex();var data=m.GetAllBoneWeights();var rows=new List<BoneWeight1>[m.vertexCount];int offset=0;for(int i=0;i<rows.Length;i++){rows[i]=new List<BoneWeight1>();for(int j=0;j<counts[i];j++)rows[i].Add(data[offset++]);}counts.Dispose();data.Dispose();return rows;}
 static float Diff(List<BoneWeight1> a,List<BoneWeight1> b){var ids=a.Select(x=>x.boneIndex).Concat(b.Select(x=>x.boneIndex)).Distinct();float max=0;foreach(int id in ids)max=Mathf.Max(max,Mathf.Abs(a.Where(x=>x.boneIndex==id).Sum(x=>x.weight)-b.Where(x=>x.boneIndex==id).Sum(x=>x.weight)));return max;}
 static bool EqualGeometry(Mesh a,Mesh b)=>a.vertices.SequenceEqual(b.vertices)&&a.triangles.SequenceEqual(b.triangles)&&a.normals.SequenceEqual(b.normals)&&a.tangents.SequenceEqual(b.tangents)&&a.uv.SequenceEqual(b.uv)&&a.uv2.SequenceEqual(b.uv2)&&a.colors.SequenceEqual(b.colors)&&a.bindposes.SequenceEqual(b.bindposes)&&a.subMeshCount==b.subMeshCount&&a.blendShapeCount==b.blendShapeCount;
 public static void ApplyAndVerify(){
  var input=JsonUtility.FromJson<InputData>(File.ReadAllText(Input));if(SHA(input.prefab)!=input.expectedPrefabSHA256)throw new Exception("Prefab changed after patch preflight");if(File.Exists(MeshPath))throw new Exception("Refuse replacing an existing corrected mesh asset");var actor=PrefabUtility.LoadPrefabContents(input.prefab);Mesh corrected=null;
  try {var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name==input.meshName);var original=skin.sharedMesh;if(original.vertexCount!=input.vertexCount||!skin.bones.Select(b=>b.name).SequenceEqual(input.bones))throw new Exception("Mesh/bone order mismatch");var before=Weights(original);var rows=before.Select(r=>r.ToList()).ToArray();var patchset=new HashSet<int>();foreach(var p in input.patches){if(!patchset.Add(p.vertex)||p.vertex<0||p.vertex>=rows.Length)throw new Exception("Invalid/duplicate patch vertex");rows[p.vertex]=p.weights.Select(w=>new BoneWeight1{boneIndex=w.boneIndex,weight=w.weight}).ToList();if(Mathf.Abs(rows[p.vertex].Sum(w=>w.weight)-1)>1e-6f)throw new Exception("Patch not normalized");}
   corrected=Object.Instantiate(original);corrected.name="Protagonist_Combat_ElbowWeights";var counts=new NativeArray<byte>(rows.Select(r=>(byte)r.Count).ToArray(),Allocator.Temp);var weights=new NativeArray<BoneWeight1>(rows.SelectMany(r=>r).ToArray(),Allocator.Temp);corrected.SetBoneWeights(counts,weights);counts.Dispose();weights.Dispose();if(!EqualGeometry(original,corrected))throw new Exception("Non-weight mesh attribute changed");var after=Weights(corrected);float unpatchedMax=0,norm=0;int changed=0;bool finite=true;for(int i=0;i<after.Length;i++){float delta=Diff(before[i],after[i]);if(delta>1e-6f)changed++;if(!patchset.Contains(i))unpatchedMax=Mathf.Max(unpatchedMax,delta);norm=Mathf.Max(norm,Mathf.Abs(after[i].Sum(w=>w.weight)-1));finite&=after[i].All(w=>!float.IsNaN(w.weight)&&!float.IsInfinity(w.weight)&&w.weight>=0&&w.boneIndex>=0&&w.boneIndex<input.bones.Length);}/* SetBoneWeights normalizes unchanged float32 rows too: allow one ULP, report its measured maximum explicitly. */if(unpatchedMax>2e-7f||norm>1e-6f||!finite)throw new Exception("Unexpected unpatched/normalization change: "+unpatchedMax+" / "+norm);
   Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));AssetDatabase.CreateAsset(corrected,MeshPath);AssetDatabase.SaveAssets();skin.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);PrefabUtility.SaveAsPrefabAsset(actor,input.prefab);AssetDatabase.SaveAssets();var report=new Report{prefab=input.prefab,prefabBeforeSHA256=input.expectedPrefabSHA256,prefabAfterSHA256=SHA(input.prefab),meshAsset=MeshPath,meshAssetSHA256=SHA(MeshPath),originalMeshAsset=AssetDatabase.GetAssetPath(original),sourceCandidateSHA256=input.sourceCandidateSHA256,sourceOriginalSHA256=input.sourceOriginalSHA256,vertexCount=original.vertexCount,patchedVertices=patchset.Count,unchangedVertices=rows.Length-patchset.Count,actualChangedVertices=changed,maximumInfluences=after.Max(r=>r.Count),geometryBindposesUVNormalsExactlyPreserved=true,allWeightsFiniteNonNegative=finite,maximumUnpatchedWeightDifference=unpatchedMax,maximumNormalizationError=norm,boneOrder=input.bones};File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));Debug.Log("COMBAT_WEIGHT_PATCH_PERSISTED "+patchset.Count+" vertices");
  }finally{PrefabUtility.UnloadPrefabContents(actor);if(corrected&&!AssetDatabase.Contains(corrected))Object.DestroyImmediate(corrected);}
  HeroAssetAudit.RunCombatRetouched();
 }
}
#endif
