#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Object = UnityEngine.Object;

// Offline-only evaluation on disposable instances of the real gameplay prefab.
public static partial class ChibiMotionSet
{
    const string CompatibilityRoot = "ArtSources/Characters/Protagonist01/Final/Validation/ChibiCompatibility20261029";
    const string GameplayChibi = "Assets/2.Prefab/Player/Player_Chibi.prefab";
    static string CompatibilityMedia => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"OZGL3_ArtReview/Characters/Protagonist01/Final/Review/Compatibility20261029");
    [Serializable] sealed class CompatibilityFrame { public int frame; public float time,height; public string phase; public Vector3 hips,leftHand,rightHand,leftFoot,rightFoot; }
    [Serializable] sealed class CompatibilitySequence { public string name,view,folder; public int fps; public bool gameplaySimulation,clipsModified; public string method; public CompatibilityFrame[] frames; }
    [Serializable] sealed class CompatibilitySequences { public CompatibilitySequence[] sequences; }
    [Serializable] sealed class CompatibleClipAudit { public string name,path,maxBoneName; public float duration,minVertexY,maxVertexStep,maxBoneStep,maxBoneTime,loopSeam; public int samples,missingBindings,nonFiniteVertices; public Vector3 firstBounds,lastBounds; public string[] frameDirectories; }
    [Serializable] sealed class CompatibleAudit { public string prefab,method; public CompatibleClipAudit[] clips; }
    [Serializable] sealed class RetargetFrames { public string clip; public CombatRetargetUtility.FrameMetrics[] frames; }
    static void DisableGameplayForPreview(GameObject actor)
    {
        foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
        while(actor.GetComponentsInChildren<MonoBehaviour>(true).Length>0)
        {
            var remaining=actor.GetComponentsInChildren<MonoBehaviour>(true);
            var removable=remaining.FirstOrDefault(candidate=>!remaining.Any(other=>other!=candidate && other.gameObject==candidate.gameObject && other.GetType().GetCustomAttributes(typeof(RequireComponent),true).Cast<RequireComponent>().Any(require=>new[]{require.m_Type0,require.m_Type1,require.m_Type2}.Any(type=>type!=null&&type.IsAssignableFrom(candidate.GetType())))));
            if(!removable)throw new Exception("Cannot detach preview runtime component dependency cycle");
            Object.DestroyImmediate(removable);
        }
        foreach(var body in actor.GetComponentsInChildren<Rigidbody>(true)){body.isKinematic=true;body.useGravity=false;}
        foreach(var collider in actor.GetComponentsInChildren<Collider>(true))collider.enabled=false;
        foreach(var animator in actor.GetComponentsInChildren<Animator>(true))animator.enabled=false;
        actor.SetActive(true);
    }
    [MenuItem("Project M/Chibi Compatibility/Review Current Jump Sequence")]
    public static void ReviewJumpCompatibility()
    {
        try
        {
            Directory.CreateDirectory(CompatibilityRoot+"/Jump");
            var records=new List<CompatibilitySequence>();
            using(var stage=new Stage())
            {
                var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(GameplayChibi));
                SceneManager.MoveGameObjectToScene(actor,stage.scene);DisableGameplayForPreview(actor);
                actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,90,0));
                var transforms=actor.GetComponentsInChildren<Transform>(true).Where(t=>t!=actor.transform).ToArray();
                var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var neutral=new Pose(transforms);
                var bones=transforms.GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
                var cpu=skins.Select(s=>new CpuSkin(s)).ToArray();
                var clips=new[]{"Idle_Fists","Jump_Takeoff","Jump_Rise_Loop","Jump_Apex_Loop","Jump_Fall_Loop","Jump_Land"}.ToDictionary(n=>n,n=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+n+".anim"));
                foreach(var c in clips.Values)if(!c)throw new Exception("Missing jump clip");
                Pose Read(string name,float time){neutral.Apply(transforms);var c=clips[name];c.SampleAnimation(actor,c.isLooping?Mathf.Repeat(time,c.length):Mathf.Clamp(time,0,c.length));return new Pose(transforms);}
                try
                {
                    const float idleEnd=.65f,airStart=.8166667f,landAt=2.0166667f,landEnd=2.45f,end=3.05f;
                    foreach(bool intended in new[]{true,false}) foreach(string view in new[]{"Side","Front"})
                    {
                        stage.SetView(new Vector3(0,1.03f,0),view=="Side"?new Vector3(1.05f,1.35f,-10):new Vector3(10,1.25f,0),1.23f);
                        var name=intended?"Intended_Idle_Jump_Land":"Saved_ApexOnly_Idle_Jump_Land";
                        var folder=Path.Combine(CompatibilityMedia,"Frames",name,view);Directory.CreateDirectory(folder);
                        var frames=new List<CompatibilityFrame>();
                        for(int i=0;i<=Mathf.RoundToInt(end*RenderRate);i++)
                        {
                            float t=i/(float)RenderRate,height=0;string phase;Pose p;
                            actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,90,0));
                            if(t<idleEnd){phase="IDLE";p=Read("Idle_Fists",t);}
                            else if(t<airStart){phase="TAKEOFF";p=Read("Jump_Takeoff",t-idleEnd);if(t<idleEnd+.06f)p=Mix(Read("Idle_Fists",t),p,S01((t-idleEnd)/.06f));}
                            else if(t<landAt)
                            {
                                float elapsed=t-airStart,u=elapsed/(landAt-airStart),v=1-2*u;
                                height=.66f*4*u*(1-u);
                                phase=v>.20f?"RISE":v<-.20f?"FALL":"APEX";
                                if(!intended)p=Read("Jump_Apex_Loop",elapsed);
                                else if(v>=.20f)p=Read("Jump_Rise_Loop",elapsed);
                                else if(v>=0)p=Mix(Read("Jump_Apex_Loop",elapsed),Read("Jump_Rise_Loop",elapsed),v/.20f);
                                else if(v>-.20f)p=Mix(Read("Jump_Apex_Loop",elapsed),Read("Jump_Fall_Loop",elapsed),-v/.20f);
                                else p=Read("Jump_Fall_Loop",elapsed);
                                if(elapsed<.08f)p=Mix(Read("Jump_Takeoff",clips["Jump_Takeoff"].length),p,S01(elapsed/.08f));
                            }
                            else if(t<landEnd){phase="LAND";p=Read("Jump_Land",t-landAt);if(t<landAt+.06f)p=Mix(Read(intended?"Jump_Fall_Loop":"Jump_Apex_Loop",landAt-airStart),p,S01((t-landAt)/.06f));}
                            else {phase="IDLE";p=Read("Idle_Fists",t-landEnd);if(t<landEnd+.10f)p=Mix(Read("Jump_Land",clips["Jump_Land"].length),p,S01((t-landEnd)/.10f));}
                            p.Apply(transforms);actor.transform.position=Vector3.up*height;
                            foreach(var skin in cpu)skin.Bake();stage.Save(Path.Combine(folder,i.ToString("D4")+".png"));
                            frames.Add(new CompatibilityFrame{frame=i,time=t,height=height,phase=phase,hips=bones["Hips"].position,leftHand=bones["LeftHand"].position,rightHand=bones["RightHand"].position,leftFoot=bones["LeftFoot"].position,rightFoot=bones["RightFoot"].position});
                        }
                        records.Add(new CompatibilitySequence{name=name,view=view,folder=folder,fps=RenderRate,gameplaySimulation=false,clipsModified=false,method="Actual Player_Chibi prefab and unchanged saved clips. Intended preview mixes Rise/Apex/Fall; comparison uses only the currently assigned Air clip (Apex). Offline transitions at authored speed; compressed .66m trajectory for pose inspection, NOT gameplay physics or full controller simulation.",frames=frames.ToArray()});
                    }
                }
                finally{foreach(var c in cpu)c.Dispose();Object.DestroyImmediate(actor);}
            }
            File.WriteAllText(CompatibilityRoot+"/Jump/sequence-render.json",JsonUtility.ToJson(new CompatibilitySequences{sequences=records.ToArray()},true));
            Debug.Log("CHIBI_CURRENT_JUMP_REVIEW_COMPLETE");
        }
        catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }

    static GameObject CompatibilityActor(Stage stage,string path)
    {
        var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        SceneManager.MoveGameObjectToScene(actor,stage.scene);DisableGameplayForPreview(actor);
        actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        return actor;
    }
    static void SaveCompatibleClip(AnimationClip clip)
    {
        string path=Root+"/Clips/"+clip.name+".anim";
        var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(old){EditorUtility.CopySerialized(clip,old);EditorUtility.SetDirty(old);Object.DestroyImmediate(clip);}
        else AssetDatabase.CreateAsset(clip,path);
    }
    public static void BuildCompatibleAttacks()
    {
        try
        {
            Directory.CreateDirectory(CompatibilityRoot+"/Combat");
            using(var stage=new Stage())
            {
                var source=CompatibilityActor(stage,"Assets/Art/Characters/Protagonist01/Combat/Prefabs/Protagonist_Combat.prefab");
                var target=CompatibilityActor(stage,GameplayChibi);
                AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/Idle_Fists.anim").SampleAnimation(target,0);
                using(var converter=new CombatRetargetUtility.Retargeter(source,target,new CombatRetargetUtility.Options{preserveTwoHandGrip=true}))
                {
                    foreach(string name in new[]{"Longsword_Attack_Chop","Longsword_Attack_Slice","Longsword_Attack_Stab"})
                    {
                        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Characters/Protagonist01/Combat/Clips/"+name+".anim");
                        SaveCompatibleClip(converter.Bake(clip,name));
                        var frames=new List<CombatRetargetUtility.FrameMetrics>();
                        int count=Mathf.RoundToInt(clip.length*120)+1;
                        for(int i=0;i<count;i++){converter.Sample(clip,clip.length*i/(count-1));frames.Add(converter.LastMetrics);}
                        File.WriteAllText(CompatibilityRoot+"/Combat/"+name+"-retarget.json",JsonUtility.ToJson(new RetargetFrames{clip=name,frames=frames.ToArray()},true));
                    }
                    var prop=Object.Instantiate(CombatRetargetUtility.Find(source,"WeaponRoot").gameObject);
                    prop.name="Longsword_Chibi_SocketProp";prop.SetActive(true);prop.transform.SetParent(null);prop.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);prop.transform.localScale=Vector3.one;
                    SceneManager.MoveGameObjectToScene(prop,stage.scene);
                    PrefabUtility.SaveAsPrefabAsset(prop,Root+"/Prefabs/Longsword_Chibi_SocketProp.prefab");Object.DestroyImmediate(prop);
                }
                Object.DestroyImmediate(source);Object.DestroyImmediate(target);
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ReviewCompatibleActions();
            Debug.Log("CHIBI_ATTACK_RETARGET_COMPLETE");
        }
        catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    public static void ReviewCompatibleActions()
    {
        var rows=new List<CompatibleClipAudit>();Directory.CreateDirectory(CompatibilityRoot+"/QA");
        foreach(string name in new[]{"Longsword_Attack_Chop","Longsword_Attack_Slice","Longsword_Attack_Stab","Death_Fall","Ladder_Climb_Loop"})
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+name+".anim");if(!clip)continue;
            using(var stage=new Stage())
            {
                var actor=CompatibilityActor(stage,GameplayChibi);
                var transforms=actor.GetComponentsInChildren<Transform>(true).Where(t=>t!=actor.transform).ToArray();var neutral=new Pose(transforms);
                if(name.StartsWith("Longsword"))
                {
                    var prop=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Longsword_Chibi_SocketProp.prefab"));
                    prop.transform.SetParent(CombatRetargetUtility.Find(actor,"WeaponSocket"),false);prop.transform.localPosition=Vector3.zero;prop.transform.localRotation=Quaternion.identity;prop.transform.localScale=Vector3.one;
                    prop.SetActive(true);
                }
                var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);var meshes=skins.Select(s=>new Mesh()).ToArray();
                var row=new CompatibleClipAudit{name=name,path=AssetDatabase.GetAssetPath(clip),duration=clip.length,minVertexY=float.PositiveInfinity};
                row.missingBindings=AnimationUtility.GetCurveBindings(clip).Count(b=>!actor.transform.Find(b.path));
                Vector3[][] first=null,last=null;Quaternion[] previousQ=null;Bounds total=new Bounds();bool have=false;
                try
                {
                    row.samples=Mathf.RoundToInt(clip.length*120)+1;
                    for(int i=0;i<row.samples;i++)
                    {
                        neutral.Apply(transforms);clip.SampleAnimation(actor,clip.length*i/(row.samples-1));
                        var frame=new Vector3[skins.Length][];
                        for(int s=0;s<skins.Length;s++)
                        {
                            skins[s].BakeMesh(meshes[s]);frame[s]=meshes[s].vertices.Select(v=>skins[s].transform.TransformPoint(v)).ToArray();
                            foreach(var v in frame[s]){if(float.IsNaN(v.x)||float.IsInfinity(v.x)||float.IsNaN(v.y)||float.IsInfinity(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.z))row.nonFiniteVertices++;row.minVertexY=Mathf.Min(row.minVertexY,v.y);if(!have){total=new Bounds(v,Vector3.zero);have=true;}else total.Encapsulate(v);}
                            if(last!=null)for(int v=0;v<frame[s].Length;v++)row.maxVertexStep=Mathf.Max(row.maxVertexStep,Vector3.Distance(frame[s][v],last[s][v]));
                            if(i==row.samples-1)for(int v=0;v<frame[s].Length;v++)row.loopSeam=Mathf.Max(row.loopSeam,Vector3.Distance(frame[s][v],first[s][v]));
                        }
                        var q=transforms.Select(t=>t.localRotation).ToArray();
                        if(previousQ!=null)for(int j=0;j<q.Length;j++){float step=Quaternion.Angle(previousQ[j],q[j]);if(step>row.maxBoneStep){row.maxBoneStep=step;row.maxBoneName=transforms[j].name;row.maxBoneTime=clip.length*i/(row.samples-1);}}
                        previousQ=q;if(first==null)first=frame;last=frame;
                    }
                    row.firstBounds=total.center;row.lastBounds=total.size;
                    var cpu=skins.Select(s=>new CpuSkin(s)).ToArray();
                    try
                    {
                        var folders=new List<string>();
                        foreach(string view in new[]{"Side","Front","Opposite","ThreeQuarter"})
                        {
                            actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,90,0));
                            stage.SetView(name=="Death_Fall"?new Vector3(-.36f,.70f,0):new Vector3(.05f,.70f,0),view=="Side"?new Vector3(1.05f,1.2f,-10):view=="Front"?new Vector3(10,1.4f,0):view=="Opposite"?new Vector3(-1.05f,1.2f,10):new Vector3(7,2.5f,-7),name=="Death_Fall"?1.3f:1.15f);
                            string folder=Path.Combine(CompatibilityMedia,"Frames",name,view);Directory.CreateDirectory(folder);folders.Add(folder);
                            int count=view=="Side"?Mathf.RoundToInt(clip.length*30)+1:13;
                            for(int i=0;i<count;i++){neutral.Apply(transforms);clip.SampleAnimation(actor,clip.length*i/(count-1));foreach(var c in cpu)c.Bake();stage.Save(Path.Combine(folder,i.ToString("D4")+".png"));}
                        }
                        row.frameDirectories=folders.ToArray();
                    }
                    finally{foreach(var c in cpu)c.Dispose();}
                    rows.Add(row);
                }
                finally{foreach(var mesh in meshes)Object.DestroyImmediate(mesh);Object.DestroyImmediate(actor);}
            }
        }
        File.WriteAllText(CompatibilityRoot+"/QA/saved-clip-audit.json",JsonUtility.ToJson(new CompatibleAudit{prefab=GameplayChibi,method="Saved native24 clips reloaded on actual gameplay prefab. Full mesh evaluation120Hz; every side frame30Hz and13 phases in3 other views. Runtime scripts removed only on disposable preview clones.",clips=rows.ToArray()},true));
        if(rows.Any(r=>r.missingBindings>0||r.nonFiniteVertices>0))throw new Exception("Binding/finite-geometry validation failed");
        Debug.Log("CHIBI_ACTION_REVIEW_COMPLETE "+rows.Count);
    }
    public static void BuildAndReviewAdditional()
    {
        try{AdditionalMotionBuilder.BuildAll();ReviewCompatibleActions();AnimatorCompatibilityValidator.Run();}
        catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    public static void BuildAllCompatibleMotions()
    {
        try{AdditionalMotionBuilder.BuildAll();BuildCompatibleAttacks();AnimatorCompatibilityValidator.Run();}
        catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    public static void ReviewLadderSource()
    {
        using(var stage=new Stage())
        {
            var actor=CompatibilityActor(stage,AdditionalMotionBuilder.LadderSource);
            var animator=actor.GetComponentInChildren<Animator>(true);animator.enabled=true;animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AdditionalMotionBuilder.LadderClip);
            var graph=PlayableGraph.Create("Ladder_Source_Inspection");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);
            var output=AnimationPlayableOutput.Create(graph,"Source",animator);output.SetSourcePlayable(playable);graph.Play();
            var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);var cpu=skins.Select(s=>new CpuSkin(s)).ToArray();
            var bones=actor.GetComponentsInChildren<Transform>(true).Where(t=>t.parent&&t.parent!=actor.transform).ToArray();
            var sticks=bones.Select(t=>{var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);SceneManager.MoveGameObjectToScene(g,stage.scene);Object.DestroyImmediate(g.GetComponent<Collider>());return g;}).ToArray();
            var records=new List<DiagnosticPose>();
            try
            {
                stage.SetView(new Vector3(0,1,0),new Vector3(1.05f,1.2f,-10),1.25f);
                string folder=Path.Combine(CompatibilityMedia,"Frames","Ladder_Source","Side");Directory.CreateDirectory(folder);
                for(int i=0;i<13;i++)
                {
                    actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,90,0));playable.SetTime(clip.length*i/12f);graph.Evaluate(0);
                    var positions=bones.Select(b=>b.position).ToArray();var bounds=new Bounds(positions[0],Vector3.zero);foreach(var p in positions)bounds.Encapsulate(p);
                    stage.SetView(bounds.center,new Vector3(1.05f,1.2f,-10),Mathf.Max(.7f,bounds.size.y*.65f));
                    for(int j=0;j<bones.Length;j++){Vector3 d=bones[j].position-bones[j].parent.position;sticks[j].transform.SetPositionAndRotation((bones[j].position+bones[j].parent.position)/2,Quaternion.FromToRotation(Vector3.up,d));sticks[j].transform.localScale=new Vector3(.025f,d.magnitude*.5f,.025f);}
                    foreach(var c in cpu)c.Bake();stage.Save(Path.Combine(folder,i.ToString("D4")+".png"));
                    records.Add(new DiagnosticPose{time=clip.length*i/12f,names=bones.Select(b=>b.name).ToArray(),positions=positions,rotations=bones.Select(b=>b.rotation).ToArray()});
                }
                File.WriteAllText(CompatibilityRoot+"/Additional/ladder-source-poses.json",JsonUtility.ToJson(new DiagnosticPoses{frames=records.ToArray()},true));
                Debug.Log("LADDER_SOURCE_INSPECTED skins="+skins.Length);
            }
            finally{foreach(var c in cpu)c.Dispose();foreach(var s in sticks)Object.DestroyImmediate(s);graph.Destroy();Object.DestroyImmediate(actor);}
        }
    }
    [Serializable] sealed class DiagnosticPose {public float time;public string[] names;public Vector3[] positions;public Quaternion[] rotations;}
    [Serializable] sealed class DiagnosticPoses {public DiagnosticPose[] frames;}
    public static void ReviewAdditionalSources()
    {
        ReviewLadderSource();
        AnimatorCompatibilityValidator.Run();
    }
}
#endif
