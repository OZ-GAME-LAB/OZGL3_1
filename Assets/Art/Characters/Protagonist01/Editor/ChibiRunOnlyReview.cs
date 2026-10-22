#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class ChibiMotionSet
{
    static readonly string[] RetiredAttackNames = { "Punch_Right_Jab", "Sword_Horizontal_Slash", "Staff_Cast_Forward" };
    const string RunBeforePath = Report + "/run-revision-before-sha.json";
    const string RunReadmeRecordPath = Report + "/research/run-revision-readme-change.json";
    const string RunReadmePath = Root + "/README_ko.txt";
    static bool usedRunReadmeException;
    [Serializable] sealed class RunBeforeFile { public string path,sha256; public bool allowChange,retire; }
    [Serializable] sealed class RunBeforeInventory { public RunBeforeFile[] files; }
    [Serializable] sealed class RunReadmeRecord { public string path,beforeSHA256,afterSHA256,reason; }
    [Serializable] sealed class RunWeight { public int[] bones; public float[] weights; }
    [Serializable] sealed class RunMeshSchema
    { public string mesh,sha256; public int vertexCount; public int[] triangles; public string[] bones; public RunWeight[] weights; }
    [Serializable] sealed class RunSchema { public string method; public RunMeshSchema[] skins; }
    [Serializable] sealed class RunBakedSkin { public int schemaIndex; public Vector3[] worldVertices; }
    [Serializable] sealed class RunBakedFrame
    { public int frame; public float time,clipTime; public FrameRow markers; public RunBakedSkin[] skins; }
    [Serializable] sealed class RunOnlyInfo
    {
        public string method,beforeReportSHA256,afterReportSHA256,geometryDirectory;
        public bool otherSavedAssetsByteIdentical,allNonRunAndNonReadmeSavedAssetsByteIdentical,recordedReadmeExceptionUsed;
        public CaptureInfo side,front; public int preservedFiles;
    }

    // Deliberately does not invoke Build, any mesh/prefab builder or any other clip bake.
    [MenuItem("Project M/Chibi Compact v007/Review Run Only")]
    public static void ReviewRunOnly()
    {
        try
        {
            var main=Definitions(); var run=main.Single(m=>m.name=="Run_Fists");
            if(main.Any(m=>RetiredAttackNames.Contains(m.name)))throw new InvalidOperationException("Attack retirement is incomplete.");
            VerifyRunAssetPreservation();
            string reportPath=Report+"/review-report.json",beforeSHA=CaptureFileSHA256(reportPath);
            var bundle=JsonUtility.FromJson<CaptureBundle>(File.ReadAllText(reportPath));
            MotionInfo baked;
            using(var stage=new Stage())
            {
                var actor=CreateActor(stage);var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                var bones=actor.GetComponentsInChildren<Transform>().Where(t=>t!=actor.transform).ToArray();
                using(var rig=new Rig(actor,skins,bones)){rig.MakeNeutral();baked=BakeMotion(actor,rig,run,false);}
                AssetDatabase.SaveAssets();
            }
            var side=CaptureMotion(run,false);
            bundle.motions=bundle.motions.Where(m=>!RetiredAttackNames.Contains(m.name)).Select(m=>m.name==run.name?side:m).ToArray();
            if(bundle.motions.Count(m=>m.name==run.name)!=1)throw new InvalidOperationException("Run report row is not unique.");
            bundle.transitions=CaptureTransitions(main); // Only Move_Dash_Recover remains.
            bundle.details=new CaptureInfo[0];
            var front=CaptureRunFrontSurface(run);
            int verified=VerifyRunAssetPreservation();
            WriteReviewText(reportPath,JsonUtility.ToJson(bundle,true));
            var build=JsonUtility.FromJson<BuildInfo>(File.ReadAllText(Report+"/build-report.json"));
            build.motions=build.motions.Where(m=>!RetiredAttackNames.Contains(m.name)).Select(m=>m.name==run.name?baked:m).ToArray();
            build.mainMotions=main.Length;build.auxiliaryClips=build.motions.Count(m=>m.auxiliary);
            WriteReviewText(Report+"/build-report.json",JsonUtility.ToJson(build,true));
            WriteReviewText(Report+"/run-only-review.json",JsonUtility.ToJson(new RunOnlyInfo{
                method="Only Run_Fists was baked. Fourteen other clips and existing models/prefabs/materials are SHA-verified unchanged. A README inventory change is allowed only by its exact before/after SHA record and is counted separately. Actual saved-clip side/front PNG and per-frame full BakeMesh world geometry are exported; no gameplay/VFX integration.",
                beforeReportSHA256=beforeSHA,afterReportSHA256=CaptureFileSHA256(reportPath),
                geometryDirectory=Path.GetFullPath(CaptureSessionRoot+"/RunArmProbe"),otherSavedAssetsByteIdentical=!usedRunReadmeException,
                allNonRunAndNonReadmeSavedAssetsByteIdentical=true,recordedReadmeExceptionUsed=usedRunReadmeException,preservedFiles=verified,side=side,front=front},true));
            Debug.Log("CHIBI_RUN_ONLY_REVIEW_SUCCESS "+CaptureSessionRoot+" preserved="+verified);
        }
        catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }

    static int VerifyRunAssetPreservation()
    {
        var before=JsonUtility.FromJson<RunBeforeInventory>(File.ReadAllText(RunBeforePath));int count=0;usedRunReadmeException=false;
        foreach(var file in before.files)
        {
            if(file.retire)
            {if(File.Exists(file.path))throw new InvalidOperationException("Retired attack file still exists: "+file.path);continue;}
            if(file.allowChange)continue;
            if(!File.Exists(file.path))throw new InvalidOperationException("Unrelated saved asset missing: "+file.path);
            string actual=CaptureFileSHA256(file.path);
            if(actual!=file.sha256)
            {
                if(file.path==RunReadmePath&&File.Exists(RunReadmeRecordPath))
                {
                    var record=JsonUtility.FromJson<RunReadmeRecord>(File.ReadAllText(RunReadmeRecordPath));
                    if(record!=null&&record.path==RunReadmePath&&record.beforeSHA256==file.sha256&&record.afterSHA256==actual)
                    {usedRunReadmeException=true;continue;}
                }
                throw new InvalidOperationException("Unrelated saved asset changed: "+file.path);
            }
            count++;
        }
        return count;
    }

    static CaptureInfo CaptureRunFrontSurface(Motion run)
    {
        using(var stage=new Stage())
        {
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));SceneManager.MoveGameObjectToScene(actor,stage.scene);
            var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bones=actor.GetComponentsInChildren<Transform>().Where(t=>t!=actor.transform&&!t.name.Contains("Socket")).ToArray();
            using(var rig=new Rig(actor,skins,bones))
            {
                rig.MakeNeutral();actor.transform.rotation=Quaternion.Euler(0,90,0);
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/Run_Fists.anim");
                SampleSavedClip(actor,clip,0);
                stage.SetView(new Vector3(0,.78f,0),new Vector3(10,1.4f,0),1.05f);
                string dir=CaptureSessionRoot+"/RunFrontQA",geometry=CaptureSessionRoot+"/RunArmProbe";
                Directory.CreateDirectory(dir);Directory.CreateDirectory(geometry);
                var schemas=skins.Select(s=>new RunMeshSchema{
                    mesh=AssetDatabase.GetAssetPath(s.sharedMesh),sha256=CaptureFileSHA256(AssetDatabase.GetAssetPath(s.sharedMesh)),
                    vertexCount=s.sharedMesh.vertexCount,triangles=s.sharedMesh.triangles,bones=s.bones.Select(b=>b.name).ToArray(),
                    weights=s.sharedMesh.boneWeights.Select(w=>new RunWeight{bones=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3},weights=new[]{w.weight0,w.weight1,w.weight2,w.weight3}}).ToArray()}).ToArray();
                WriteReviewText(geometry+"/schema.json",JsonUtility.ToJson(new RunSchema{
                    method="Actual complete saved base mesh triangles and bone weights. Per-frame vertices are Unity BakeMesh transformed into actor-world metres, with actor yaw90 matching the side-view review. No marker-only collision claim.",skins=schemas}));
                var cpu=skins.Select(s=>new CpuSkin(s)).ToArray();var baked=skins.Select(s=>new Mesh()).ToArray();var rows=new List<FrameRow>();
                try
                {
                    int count=Mathf.RoundToInt(run.duration*2*RenderRate);
                    for(int frame=0;frame<count;frame++)
                    {
                        float time=frame/(float)RenderRate,local=Mathf.Repeat(time,run.duration);SampleSavedClip(actor,clip,local);
                        foreach(var skin in cpu)skin.Bake();stage.Save(dir+"/"+frame.ToString("D4")+".png");
                        var live=new RunBakedSkin[skins.Length];
                        for(int i=0;i<skins.Length;i++)
                        {skins[i].BakeMesh(baked[i]);live[i]=new RunBakedSkin{schemaIndex=i,worldVertices=baked[i].vertices.Select(v=>skins[i].transform.TransformPoint(v)).ToArray()};}
                        var row=rig.Frame(time);rows.Add(row);
                        WriteReviewText(geometry+"/"+frame.ToString("D4")+".json",JsonUtility.ToJson(new RunBakedFrame{frame=frame,time=time,clipTime=local,markers=row,skins=live}));
                    }
                    var info=Info("Run_Fists_Front_QA",dir,count,stage,rows);info.loop=true;info.durationSeconds=run.duration;
                    info.intent="Supplementary frontal QA of the saved Run_Fists clip. Actual full surface export is in RunArmProbe; main review remains2.5D side view.";
                    info.sourcePrefab=PrefabPath;info.sourceMeshSHA256=CaptureFileSHA256(FistsPath);info.sourceClipSHA256=CaptureFileSHA256(Root+"/Clips/Run_Fists.anim");
                    WriteReviewText(dir+"/capture.json",JsonUtility.ToJson(info,true));return info;
                }
                finally{foreach(var skin in cpu)skin.Dispose();foreach(var mesh in baked)Object.DestroyImmediate(mesh);}
            }
        }
    }
}
#endif
