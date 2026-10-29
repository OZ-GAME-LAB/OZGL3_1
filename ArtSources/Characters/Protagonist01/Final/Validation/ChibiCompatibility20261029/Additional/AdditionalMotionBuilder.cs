#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

// Offline-only source. Copy to a disposable Editor import location when running.
// All Animator/Avatar changes below affect disposable instances, never player assets.
public static class AdditionalMotionBuilder
{
    public const string TargetPrefab = "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Prefabs/Protagonist_Chibi_MotionReview.prefab";
    public const string OutputRoot = "Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Clips";
    public const string KayKitSource = "Assets/Art/Monsters/Stage01/SkeletonsPack01/v001/FBX/Skeleton_Mage.fbx";
    public const string LadderSource = "Assets/Anim/Clips/Climbing Ladder.fbx";
    public const string LadderClip = "Assets/Anim/Clips/Climb.anim";
    const string ReportRoot = "ArtSources/Characters/Protagonist01/Final/Validation/ChibiCompatibility20261029/Additional";
    const int Rate = 60;

    [Serializable] public sealed class ClipEvidence
    {
        public string name, path, source, sourceTake, method;
        public float seconds, sourceSeconds, sourceLoopEndpointMaxDegrees, savedLoopEndpointMaxDegrees;
        public float maximumGroundCorrection, finalMeshMinimumY, maximumLocalBoneLengthDeviation;
        public Vector3 ladderInitialPelvisOffset,ladderCyclePelvisDisplacement;
        public float maximumAnatomicalDirectionError;
        public float minimumHeadHandSupportPlaneGapDiagnostic,maximumShoulderClearanceCorrection;
        public int samples, genericBindings;
        public bool loop, targetAvatarValid, humanoidCurvesRemaining;
    }
    [Serializable] public sealed class BuildEvidence
    {
        public string targetPrefab = TargetPrefab;
        public string scope = "Two new Chibi24 Generic clips; no runtime code, controller, model, rig, or VFX asset changes.";
        public ClipEvidence[] clips;
        public string independentVisualReviewReport = "QA/final-visual-review.json";
    }
    [Serializable] public sealed class ClearanceSample {public float time,separation;public bool actualContact;}
    [Serializable] public sealed class ShoulderClearanceSample
    {public float time,separation,maximumCorrection;public Quaternion leftCorrection,rightCorrection;}
    [Serializable] public sealed class ShoulderClearanceEvidence
    {
        public string method;
        public float outwardReach,forwardReach,verticalScale,minimumSeparation,maximumReachFraction,maximumAdjacentCorrectionStepDegrees,maximumWristLocalDeviation;
        public int actualContactFrames;
        public ShoulderClearanceSample[] frames;
    }
    [Serializable] public sealed class BakedClearanceEvidence
    {
        public string clip,method;
        public int sampleRate,samples;
        public float minimumSupportPlaneGapDiagnostic,requiredActualContactCount,worstTime;
        public int actualContactSamples;
        public bool passed;
        public ClearanceSample[] frames;
    }
    [Serializable] public sealed class GeometryEvidence
    {
        public float time;
        public Vector3[] head,leftFist,rightFist,leftTriangles,rightTriangles;
        public string[] boneNames;
        public Vector3[] bonePositions;
        public Quaternion[] boneRotations;
    }
    sealed class Pose
    {
        public Vector3[] p, s;
        public Quaternion[] q;
        public Pose(Transform[] bones)
        {
            p=bones.Select(x=>x.localPosition).ToArray();
            q=bones.Select(x=>x.localRotation).ToArray();
            s=bones.Select(x=>x.localScale).ToArray();
        }
        public Pose Copy()
        {
            return new Pose(new Transform[0]) { p=(Vector3[])p.Clone(),q=(Quaternion[])q.Clone(),s=(Vector3[])s.Clone() };
        }
    }

    public static void BuildAll()
    {
        Directory.CreateDirectory(OutputRoot);
        Directory.CreateDirectory(ReportRoot);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var rows = new List<ClipEvidence>();
        rows.Add(BuildKayKit("Death_A", "Death_Fall", true));
        rows.Add(BuildLadder());
        AssetDatabase.SaveAssets();
        ValidateSavedLadderClearance();
        File.WriteAllText(ReportRoot+"/build-evidence.json",JsonUtility.ToJson(new BuildEvidence { clips=rows.ToArray() },true));
        Debug.Log("CHIBI_ADDITIONAL_MOTIONS_BUILT: "+string.Join(", ",rows.Select(x=>x.name)));
    }

    public static void ValidateSavedLadderClearance()
    {
        GameObject target=null;
        try
        {
            target=Clone(TargetPrefab);DisableAnimators(target);CombatRetargetUtility.RestoreBindPose(target);
            string path=OutputRoot+"/Ladder_Climb_Loop.anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(!clip)throw new InvalidOperationException("Missing saved ladder clip.");
            Vector3 right=CombatRetargetUtility.Find(target,"RightArm").position-CombatRetargetUtility.Find(target,"LeftArm").position;
            var basis=Quaternion.LookRotation(Vector3.Cross(Vector3.ProjectOnPlane(right,Vector3.up).normalized,Vector3.up),Vector3.up);
            var verifier=new HeadHandClearance(target,basis);
            var result=MeasureClipClearance(clip,target,verifier,true);
            File.WriteAllText(ReportRoot+"/ladder-baked-clearance.json",JsonUtility.ToJson(result,true));
            if(!result.passed)throw new InvalidOperationException("Saved ladder actual head/fist contact at "+result.actualContactSamples+" sampled poses.");
            Debug.Log("CHIBI_LADDER_BAKED_CLEARANCE_PASSED: zero actual contacts across "+result.samples+" samples.");
        }
        finally{if(target)Object.DestroyImmediate(target);}
    }
    public static void DiagnoseSavedLadderGeometry()
    {ExportSavedLadderGeometry("LadderNativeGeometry");}
    public static void DiagnoseFinalLadderGeometry()
    {ExportSavedLadderGeometry("LadderFinalGeometry");}
    static void ExportSavedLadderGeometry(string directoryName)
    {
        GameObject target=null;
        try
        {
            target=Clone(TargetPrefab);DisableAnimators(target);CombatRetargetUtility.RestoreBindPose(target);
            var verifier=new HeadHandClearance(target,Quaternion.identity);
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputRoot+"/Ladder_Climb_Loop.anim");
            string output=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"OZGL3_ArtReview","Characters","Protagonist01","Final","Review","Compatibility20261029","Diagnostics",directoryName);
            Directory.CreateDirectory(output);
            int count=Mathf.RoundToInt(clip.length*30)+1;
            for(int i=0;i<count;i++)
            {float time=i*clip.length/(count-1);clip.SampleAnimation(target,time);verifier.WriteGeometry(target,time,Path.Combine(output,i.ToString("D4")+".json"));}
            Debug.Log("CHIBI_LADDER_GEOMETRY_DUMP_COMPLETE");
        }
        finally{if(target)Object.DestroyImmediate(target);}
    }
    static BakedClearanceEvidence MeasureClipClearance(AnimationClip clip,GameObject target,HeadHandClearance verifier,bool saved)
    {
        int count=Mathf.CeilToInt(clip.length*120)+1;var frames=new ClearanceSample[count];
        var result=new BakedClearanceEvidence{clip=OutputRoot+"/"+clip.name+".anim",sampleRate=120,samples=count,minimumSupportPlaneGapDiagnostic=float.PositiveInfinity,requiredActualContactCount=0,
            method=(saved?"Saved":"In-memory candidate")+" Generic clip sampled at least120Hz. Actual deformed head/fist triangle edge crossings in both directions after AABB pruning plus solid-angle containment. Pass requires zero actual contacts. Separation field is ONLY the old conservative support-plane diagnostic; negative values do not imply penetration. No pose/asset edits during this test."};
        for(int i=0;i<count;i++)
        {
            float time=i*clip.length/(count-1);clip.SampleAnimation(target,time);float gap=verifier.Measure();
            bool contact=verifier.HasActualContact();if(contact)result.actualContactSamples++;
            frames[i]=new ClearanceSample{time=time,separation=gap,actualContact=contact};
            if(gap<result.minimumSupportPlaneGapDiagnostic){result.minimumSupportPlaneGapDiagnostic=gap;result.worstTime=time;}
        }
        result.frames=frames;result.passed=result.actualContactSamples==0;
        return result;
    }

    public static ClipEvidence BuildKayKit(string sourceTake, string outputName, bool death)
    {
        GameObject source=null,target=null;
        try
        {
            source=Clone(KayKitSource); target=Clone(TargetPrefab);
            var clips=AssetDatabase.LoadAllAssetsAtPath(KayKitSource).OfType<AnimationClip>()
                .Where(x=>!x.name.StartsWith("__preview__",StringComparison.Ordinal)).ToArray();
            var matches=clips.Where(x=>NativeName(x.name)==sourceTake).ToArray();
            if(matches.Length!=1) throw new InvalidOperationException("Missing/ambiguous KayKit take: "+sourceTake);
            var sourceClip=matches[0];
            DisableAnimators(source); DisableAnimators(target);
            CombatRetargetUtility.RestoreBindPose(target);
            float floor=MeshMinimumY(target);
            var options=new CombatRetargetUtility.Options { preserveTwoHandGrip=false, rootMotionScale=1f, preserveSourceRootTranslation=true };
            using(var transfer=new CombatRetargetUtility.Retargeter(source,target,options))
            {
                var bones=target.GetComponentsInChildren<Transform>(true).Where(x=>x!=target.transform).ToArray();
                float duration=sourceClip.length+(death?.20f:0f);
                int count=Mathf.RoundToInt(duration*Rate)+1;
                var frames=new Pose[count];
                float largestCorrection=0f, endMin=0f;
                for(int i=0;i<count;i++)
                {
                    float time=i*duration/(count-1);
                    transfer.Sample(sourceClip,Mathf.Min(time,sourceClip.length));
                    if(death)
                    {
                        var hips=CombatRetargetUtility.Find(target,"Hips");
                        float currentMin=MeshMinimumY(target);
                        // Keep the deformed mesh above the floor; settle the final held pose
                        // onto the same floor without rewriting the source rotation motion.
                        float settle=Mathf.SmoothStep(0,1,Mathf.InverseLerp(sourceClip.length*.75f,sourceClip.length,time));
                        float correction=Mathf.Lerp(Mathf.Max(0,floor-currentMin),floor-currentMin,settle);
                        hips.position+=Vector3.up*correction;
                        largestCorrection=Mathf.Max(largestCorrection,Mathf.Abs(correction));
                        endMin=MeshMinimumY(target);
                    }
                    frames[i]=new Pose(bones);
                }
                var clip=MakeGeneric(target,bones,frames,duration,outputName,false);
                Save(clip,OutputRoot+"/"+outputName+".anim");
                return new ClipEvidence {name=outputName,path=OutputRoot+"/"+outputName+".anim",source=KayKitSource,
                    sourceTake=sourceClip.name,sourceSeconds=sourceClip.length,seconds=duration,samples=count,loop=false,
                    method=death?"Bind-frame retarget of KayKit Death_A; actual deformed-mesh floor correction, non-looping final0.20s hold.":"Generic bind-frame retarget retaining source timing.",
                    maximumGroundCorrection=largestCorrection,finalMeshMinimumY=endMin,genericBindings=AnimationUtility.GetCurveBindings(clip).Length,
                    humanoidCurvesRemaining=AnimationUtility.GetCurveBindings(clip).Any(b=>b.type==typeof(Animator))};
            }
        }
        finally { if(source)Object.DestroyImmediate(source); if(target)Object.DestroyImmediate(target); }
    }

    public static ClipEvidence BuildLadder()
    {
        GameObject source=null,target=null;
        AnimationClip samplingClip=null;
        PlayableGraph graph=default(PlayableGraph);
        try
        {
            source=Clone(LadderSource); target=Clone(TargetPrefab);
            var input=AssetDatabase.LoadAssetAtPath<AnimationClip>(LadderClip);
            if(!input || !input.humanMotion) throw new InvalidOperationException("Expected existing Mixamo Humanoid Climb.anim.");
            // The asset loops at exactly its duration. Sample a temporary non-looping
            // clone so the final stored source pose is measured without wrapping to zero.
            samplingClip=Object.Instantiate(input);
            samplingClip.name=input.name+"_OfflineUnwrapped";
            var samplingSettings=AnimationUtility.GetAnimationClipSettings(samplingClip);
            samplingSettings.loopTime=false; samplingSettings.loopBlend=false;
            AnimationUtility.SetAnimationClipSettings(samplingClip,samplingSettings);
            samplingClip.wrapMode=WrapMode.ClampForever;
            var sourceAnimator=source.GetComponentInChildren<Animator>(true);
            if(!sourceAnimator || !sourceAnimator.avatar || !sourceAnimator.avatar.isValid || !sourceAnimator.avatar.isHuman)
                throw new InvalidOperationException("Existing Climbing Ladder FBX requires a valid Humanoid avatar.");
            DisableAnimators(target);
            CombatRetargetUtility.RestoreBindPose(target);
            sourceAnimator.runtimeAnimatorController=null; sourceAnimator.applyRootMotion=false;
            sourceAnimator.cullingMode=AnimatorCullingMode.AlwaysAnimate; sourceAnimator.enabled=true;
            sourceAnimator.Rebind();
            graph=PlayableGraph.Create("Offline_Mixamo_Climb_To_Chibi24");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable=AnimationClipPlayable.Create(graph,samplingClip);
            playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
            // Evaluate the native motion on its original Avatar only. Chibi's bind
            // axes/proportions are not suitable for an auto-built HumanAvatar.
            var output=AnimationPlayableOutput.Create(graph,"NativeMixamoSource",sourceAnimator);
            output.SetSourcePlayable(playable); graph.Play();
            Action<float> evaluateSource=time=>
            {
                source.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                playable.SetTime(time); graph.Evaluate(0);
            };
            var sourceHips=CombatRetargetUtility.Find(source,"mixamorig:Hips");
            evaluateSource(0);Vector3 firstHips=sourceHips.position;
            var transfer=new NativeLadderTransfer(source,target);
            evaluateSource(input.length);Vector3 lastHips=sourceHips.position;
            Vector3 cycleDisplacement=lastHips-firstHips;
            var bones=target.GetComponentsInChildren<Transform>(true).Where(x=>x!=target.transform).ToArray();
            Vector3[] bindPositions=bones.Select(b=>b.localPosition).ToArray();
            float sourceStep=1f/Mathf.Max(1,input.frameRate);
            float duration=input.length+sourceStep;
            int count=Mathf.RoundToInt(duration*Rate)+1;
            var frames=new Pose[count];
            float maximumLocalBoneLengthDeviation=0f,maximumAnatomicalDirectionError=0f;
            float minimumHeadHandSupportPlaneGapDiagnostic=float.PositiveInfinity,maximumShoulderClearanceCorrection=0;
            for(int i=0;i<count;i++)
            {
                float time=i*duration/(count-1), sourceTime=Mathf.Min(time,input.length);
                evaluateSource(sourceTime);
                transfer.Apply(sourceHips.position-firstHips-cycleDisplacement*(sourceTime/input.length));
                maximumAnatomicalDirectionError=Mathf.Max(maximumAnatomicalDirectionError,transfer.MaximumDirectionError);
                frames[i]=new Pose(bones);
                for(int b=0;b<bones.Length;b++)
                    if(bones[b].name!="Hips") maximumLocalBoneLengthDeviation=Mathf.Max(maximumLocalBoneLengthDeviation,Mathf.Abs(bones[b].localPosition.magnitude-bindPositions[b].magnitude));
            }
            var clearanceEvidence=transfer.AdjustSequence(bones,frames,duration);
            minimumHeadHandSupportPlaneGapDiagnostic=clearanceEvidence.minimumSeparation;
            maximumShoulderClearanceCorrection=clearanceEvidence.frames.Max(f=>f.maximumCorrection);
            int endpointIndex=Mathf.Min(count-1,Mathf.RoundToInt(input.length/duration*(count-1)));
            float sourceSeam=0f;
            for(int b=0;b<bones.Length;b++) sourceSeam=Mathf.Max(sourceSeam,Quaternion.Angle(frames[0].q[b],frames[endpointIndex].q[b]));
            // The original FBX ends one30Hz sample before the next cycle. Restore
            // that closing interval, rather than forcing a snap at the seam.
            for(int i=0;i<count;i++)
            {
                float time=i*duration/(count-1);
                if(time<=input.length) continue;
                float u=Mathf.Clamp01((time-input.length)/sourceStep);
                for(int b=0;b<bones.Length;b++)
                {
                    frames[i].p[b]=Vector3.Lerp(frames[endpointIndex].p[b],frames[0].p[b],u);
                    frames[i].q[b]=Quaternion.Slerp(frames[endpointIndex].q[b],frames[0].q[b],u);
                    frames[i].s[b]=Vector3.Lerp(frames[endpointIndex].s[b],frames[0].s[b],u);
                }
            }
            frames[count-1]=frames[0].Copy();
            File.WriteAllText(ReportRoot+"/ladder-clearance-frames.json",JsonUtility.ToJson(clearanceEvidence,true));
            if(sourceSeam>45f) throw new InvalidOperationException("Climb source seam too large for a one-frame closing interval: "+sourceSeam+" degrees. Inspect source before saving.");
            var clip=MakeGeneric(target,bones,frames,duration,"Ladder_Climb_Loop",true);
            var candidateEvidence=transfer.ValidateBaked(clip,target);
            File.WriteAllText(ReportRoot+"/ladder-candidate-clearance.json",JsonUtility.ToJson(candidateEvidence,true));
            if(!candidateEvidence.passed)
            {
                string diagnostic=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"OZGL3_ArtReview","Characters","Protagonist01","Final","Review","Compatibility20261029","Diagnostics");
                Directory.CreateDirectory(diagnostic);
                File.WriteAllText(Path.Combine(diagnostic,"Ladder_Climb_Loop_Rejected.asset.json"),EditorJsonUtility.ToJson(clip,true));
                throw new InvalidOperationException("Ladder candidate has actual head/fist contacts at "+candidateEvidence.actualContactSamples+" baked poses. All49 build samples and120Hz candidate samples recorded; final asset not replaced.");
            }
            Save(clip,OutputRoot+"/Ladder_Climb_Loop.anim");
            return new ClipEvidence {name=clip.name,path=OutputRoot+"/Ladder_Climb_Loop.anim",source=LadderSource,sourceTake=LadderClip,
                method="Existing Mixamo Humanoid clip cloned without looping and evaluated on its original Avatar. Native torso frames and limb directions transferred onto Chibi bind pose; native axial twist retained and local lengths fixed. Target has no Humanoid Avatar. Source pelvis balance scaled/in-place. Constant chest-relative rung targets adapt arm reach with two-bone IK, preserving native elbow bend planes, fixed lengths and wrist LOCAL pose; maximumAnatomicalDirectionError is measured BEFORE this proportional arm adaptation. Generic endpoint closes over30Hz interval. Saved clip clearance is separately tested at120Hz. No source/importer changes.",
                seconds=duration,sourceSeconds=input.length,samples=count,loop=true,targetAvatarValid=false,
                ladderInitialPelvisOffset=firstHips,ladderCyclePelvisDisplacement=cycleDisplacement,maximumAnatomicalDirectionError=maximumAnatomicalDirectionError,
                minimumHeadHandSupportPlaneGapDiagnostic=minimumHeadHandSupportPlaneGapDiagnostic,maximumShoulderClearanceCorrection=maximumShoulderClearanceCorrection,
                sourceLoopEndpointMaxDegrees=sourceSeam,savedLoopEndpointMaxDegrees=0f,maximumLocalBoneLengthDeviation=maximumLocalBoneLengthDeviation,
                genericBindings=AnimationUtility.GetCurveBindings(clip).Length,humanoidCurvesRemaining=AnimationUtility.GetCurveBindings(clip).Any(b=>b.type==typeof(Animator))};
        }
        finally
        {
            if(graph.IsValid())graph.Destroy();
            if(samplingClip)Object.DestroyImmediate(samplingClip);
            if(source)Object.DestroyImmediate(source); if(target)Object.DestroyImmediate(target);
        }
    }

    // Direction-based conversion avoids target Humanoid joint-limit and T-pose
    // calibration assumptions. Source joints have already been evaluated natively.
    sealed class NativeLadderTransfer
    {
        readonly Dictionary<string,Transform> s,t;
        readonly Dictionary<string,Vector3> bp;
        readonly Dictionary<string,Quaternion> bq;
        readonly Transform[] nodes;
        readonly Pose bind;
        readonly Quaternion alignment;
        readonly Vector3 bindRight;
        readonly float scale;
        readonly Dictionary<string,Quaternion> firstSourceQ;
        Dictionary<string,Quaternion> firstTargetQ;
        readonly HeadHandClearance clearance;
        public float MaximumDirectionError {get;private set;}
        public BakedClearanceEvidence ValidateBaked(AnimationClip clip,GameObject target){return MeasureClipClearance(clip,target,clearance,false);}
        public NativeLadderTransfer(GameObject source,GameObject target)
        {
            s=source.GetComponentsInChildren<Transform>(true).Where(x=>x.name.StartsWith("mixamorig:",StringComparison.Ordinal))
                .ToDictionary(x=>x.name.Substring("mixamorig:".Length));
            t=target.GetComponentsInChildren<Transform>(true).ToDictionary(x=>x.name);
            nodes=target.GetComponentsInChildren<Transform>(true);bind=new Pose(nodes);
            bp=t.ToDictionary(p=>p.Key,p=>p.Value.position);bq=t.ToDictionary(p=>p.Key,p=>p.Value.rotation);
            bindRight=bp["RightArm"]-bp["LeftArm"];
            alignment=BodyFrame(bindRight,Vector3.up)*Quaternion.Inverse(BodyFrame(S("RightArm")-S("LeftArm"),Vector3.up));
            scale=.5f*(Chain(bp["LeftUpLeg"],bp["LeftLeg"],bp["LeftFoot"])/Chain(S("LeftUpLeg"),S("LeftLeg"),S("LeftFoot"))
                +Chain(bp["RightUpLeg"],bp["RightLeg"],bp["RightFoot"])/Chain(S("RightUpLeg"),S("RightLeg"),S("RightFoot")));
            firstSourceQ=s.ToDictionary(p=>p.Key,p=>p.Value.rotation);
            foreach(var skin in target.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach(string name in new[]{"Grip_R","Grip_L"}){int id=skin.sharedMesh.GetBlendShapeIndex(name);if(id>=0)skin.SetBlendShapeWeight(id,100);}
            clearance=new HeadHandClearance(target,BodyFrame(bindRight,Vector3.up));
            // Calibrate source longitudinal axes onto the target with minimal
            // swings at a real source pose. Subsequent source global rotation
            // deltas preserve axial twist without guessing a straight-bind hinge.
            Apply(Vector3.zero);
            firstTargetQ=t.ToDictionary(p=>p.Key,p=>p.Value.rotation);
        }
        Vector3 S(string name){return s[name].position;}
        static float Chain(Vector3 a,Vector3 b,Vector3 c){return Vector3.Distance(a,b)+Vector3.Distance(b,c);}
        static Quaternion BodyFrame(Vector3 right,Vector3 up)
        {
            up.Normalize();right=Vector3.ProjectOnPlane(right,up).normalized;
            if(up.sqrMagnitude<.5f||right.sqrMagnitude<.5f)throw new InvalidOperationException("Degenerate anatomical body frame.");
            return Quaternion.LookRotation(Vector3.Cross(right,up).normalized,up);
        }
        void Body(string targetBone,string targetEnd,string sourceBone,string sourceEnd,Vector3 sourceRight)
        {
            var initial=BodyFrame(bindRight,bp[targetEnd]-bp[targetBone]);
            var current=BodyFrame(sourceRight,S(sourceEnd)-S(sourceBone));
            t[targetBone].rotation=alignment*current*Quaternion.Inverse(initial)*bq[targetBone];
        }
        Quaternion NativeDelta(string sourceBone)
        {
            return alignment*(s[sourceBone].rotation*Quaternion.Inverse(firstSourceQ[sourceBone]))*Quaternion.Inverse(alignment);
        }
        void Segment(string targetBone,string targetEnd,string sourceBone,string sourceEnd)
        {
            var bone=t[targetBone];
            if(firstTargetQ!=null)bone.rotation=NativeDelta(sourceBone)*firstTargetQ[targetBone];
            Vector3 current=t[targetEnd].position-bone.position,desired=alignment*(S(sourceEnd)-S(sourceBone));
            if(current.sqrMagnitude<1e-10f||desired.sqrMagnitude<1e-10f)throw new InvalidOperationException("Degenerate ladder segment "+targetBone);
            bone.rotation=Quaternion.FromToRotation(current,desired)*bone.rotation;
        }
        void Check(string a,string b,string sa,string sb)
        {
            MaximumDirectionError=Mathf.Max(MaximumDirectionError,Vector3.Angle(t[b].position-t[a].position,alignment*(S(sb)-S(sa))));
        }
        public void Apply(Vector3 sourcePelvisBalance)
        {
            for(int i=0;i<nodes.Length;i++){nodes[i].localPosition=bind.p[i];nodes[i].localRotation=bind.q[i];nodes[i].localScale=bind.s[i];}
            MaximumDirectionError=0;
            Vector3 sourceRight=S("RightArm")-S("LeftArm");
            var initialPelvis=BodyFrame(bp["RightUpLeg"]-bp["LeftUpLeg"],bp["Spine02"]-bp["Hips"]);
            var currentPelvis=BodyFrame(S("RightUpLeg")-S("LeftUpLeg"),S("Spine")-S("Hips"));
            t["Hips"].rotation=alignment*currentPelvis*Quaternion.Inverse(initialPelvis)*bq["Hips"];
            t["Hips"].position=bp["Hips"]+alignment*sourcePelvisBalance*scale;
            Body("Spine02","Spine01","Spine","Spine1",sourceRight);
            Body("Spine01","Spine","Spine1","Spine2",sourceRight);
            Body("Spine","neck","Spine2","Neck",sourceRight);
            Body("neck","Head","Neck","Head",sourceRight);
            // The Chibi has no head-tip bone. Its bind local +Y defines that axis.
            var initialHead=BodyFrame(bindRight,bq["Head"]*Vector3.up);
            var sourceHead=BodyFrame(sourceRight,S("HeadTop_End")-S("Head"));
            t["Head"].rotation=alignment*sourceHead*Quaternion.Inverse(initialHead)*bq["Head"];
            foreach(string side in new[]{"Left","Right"})
            {
                Segment(side+"Shoulder",side+"Arm",side+"Shoulder",side+"Arm");
                Segment(side+"Arm",side+"ForeArm",side+"Arm",side+"ForeArm");
                Segment(side+"ForeArm",side+"Hand",side+"ForeArm",side+"Hand");
                // Preserve native hand-to-forearm longitudinal angle. Closed fists
                // do not need Mixamo's detailed individual finger curl/roll channels.
                Quaternion forearmDelta=t[side+"ForeArm"].rotation*Quaternion.Inverse(bq[side+"ForeArm"]);
                var hand=t[side+"Hand"];hand.rotation=firstTargetQ==null?forearmDelta*bq[side+"Hand"]:NativeDelta(side+"Hand")*firstTargetQ[side+"Hand"];
                Vector3 fingerAxis=alignment*(S(side+"HandMiddle1")-S(side+"Hand"));
                hand.rotation=Quaternion.FromToRotation(hand.rotation*Vector3.up,fingerAxis)*hand.rotation;
                Segment(side+"UpLeg",side+"Leg",side+"UpLeg",side+"Leg");
                Segment(side+"Leg",side+"Foot",side+"Leg",side+"Foot");
                Segment(side+"Foot",side+"ToeBase",side+"Foot",side+"ToeBase");
                var toe=t[side+"ToeBase"];
                toe.rotation=firstTargetQ==null?t[side+"Foot"].rotation*Quaternion.Inverse(bq[side+"Foot"])*bq[side+"ToeBase"]:NativeDelta(side+"ToeBase")*firstTargetQ[side+"ToeBase"];
                Check(side+"Arm",side+"ForeArm",side+"Arm",side+"ForeArm");
                Check(side+"ForeArm",side+"Hand",side+"ForeArm",side+"Hand");
                Check(side+"UpLeg",side+"Leg",side+"UpLeg",side+"Leg");
                Check(side+"Leg",side+"Foot",side+"Leg",side+"Foot");
            }
            Check("Spine02","Spine01","Spine","Spine1");Check("Spine01","Spine","Spine1","Spine2");
            Check("Spine","neck","Spine2","Neck");Check("neck","Head","Neck","Head");
        }
        sealed class Offset
        {
            public float outward,forward,heightScale;
        }
        void ApplyPose(Transform[] bones,Pose pose)
        {for(int i=0;i<bones.Length;i++){bones[i].localPosition=pose.p[i];bones[i].localRotation=pose.q[i];bones[i].localScale=pose.s[i];}}
        bool ApplyOffset(Offset offset,out Quaternion left,out Quaternion right,out float reach,out float wristError)
        {
            var chestFrame=BodyFrame(t["RightArm"].position-t["LeftArm"].position,t["neck"].position-t["Spine02"].position);
            Vector3 up=chestFrame*Vector3.up,rightAxis=chestFrame*Vector3.right,forward=chestFrame*Vector3.forward;
            left=right=Quaternion.identity;reach=0;wristError=0;
            var goals=new Dictionary<string,Vector3>();
            foreach(string side in new[]{"Left","Right"})
            {
                var upper=t[side+"Arm"];var lower=t[side+"ForeArm"];var hand=t[side+"Hand"];
                float height=Vector3.Dot(hand.position-upper.position,up)*offset.heightScale;
                var goal=upper.position+rightAxis*(side=="Right"?offset.outward:-offset.outward)+forward*offset.forward+up*height;
                float ratio=Vector3.Distance(upper.position,goal)/(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position));
                reach=Mathf.Max(reach,ratio);if(ratio>.985f)return false;goals[side]=goal;
            }
            foreach(string side in new[]{"Left","Right"})
            {
                var upper=t[side+"Arm"];var lower=t[side+"ForeArm"];var hand=t[side+"Hand"];
                var initial=upper.rotation;var wristLocal=hand.localRotation;Vector3 goal=goals[side];
                Vector3 oldAxis=hand.position-upper.position,newAxis=goal-upper.position;
                Vector3 oldBend=Vector3.ProjectOnPlane(lower.position-upper.position,oldAxis);
                if(oldBend.sqrMagnitude<1e-10f)throw new InvalidOperationException("Degenerate native ladder elbow bend plane.");
                Vector3 bend=(Quaternion.FromToRotation(oldAxis,newAxis)*oldBend.normalized).normalized;
                float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position),d=newAxis.magnitude;
                Vector3 axis=newAxis/d;float along=(a*a-b*b+d*d)/(2*d),perpendicular=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
                Vector3 elbow=upper.position+axis*along+bend*perpendicular;
                upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
                lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,goal-lower.position)*lower.rotation;
                // The hand is carried by its forearm. Never write its local rotation.
                wristError=Mathf.Max(wristError,Quaternion.Angle(wristLocal,hand.localRotation));
                if(side=="Left")left=upper.rotation*Quaternion.Inverse(initial);else right=upper.rotation*Quaternion.Inverse(initial);
            }
            return true;
        }
        public ShoulderClearanceEvidence AdjustSequence(Transform[] bones,Pose[] frames,float duration)
        {
            var native=frames.Select(p=>p.Copy()).ToArray();var candidates=new List<Offset>();
            foreach(float outward in new[]{.10f,.12f,.14f,.16f,.18f})foreach(float forward in new[]{.12f,.14f,.16f,.18f,.20f})foreach(float heightScale in new[]{1f,.85f,.70f})
                candidates.Add(new Offset{outward=outward,forward=forward,heightScale=heightScale});
            candidates=candidates.OrderBy(c=>c.outward+c.forward*.4f+(1-c.heightScale)*.15f).ToList();
            var probe=Enumerable.Range(0,native.Length).Where(i=>i%4==0||i==native.Length-1).ToArray();
            Offset selected=null;
            foreach(var candidate in candidates)
            {
                bool good=true;
                foreach(int index in probe)
                {
                    ApplyPose(bones,native[index]);
                    if(!ApplyOffset(candidate,out _,out _,out _,out _)||clearance.HasActualContact()){good=false;break;}
                }
                if(!good)continue;
                for(int index=0;index<native.Length;index++)
                {
                    ApplyPose(bones,native[index]);
                    if(!ApplyOffset(candidate,out _,out _,out _,out _)||clearance.HasActualContact()){good=false;break;}
                }
                if(good){selected=candidate;break;}
            }
            if(selected==null)throw new InvalidOperationException("No feasible rung IK parameter set without actual head/fist contact. Final ladder asset retained.");
            var result=new ShoulderClearanceEvidence{method="Constant chest-relative rung targets: outside shoulder width plus forward reach; native vertical alternation retained/scaled. Two-bone IK transports the native elbow bend plane and preserves hand local rotation. Every target must remain within98.5percent actual chain reach; no phase-dependent clamps. Actual triangle contacts, not conservative support planes, select the candidate.",
                outwardReach=selected.outward,forwardReach=selected.forward,verticalScale=selected.heightScale,minimumSeparation=float.PositiveInfinity,frames=new ShoulderClearanceSample[native.Length]};
            for(int i=0;i<native.Length;i++)
            {
                ApplyPose(bones,native[i]);ApplyOffset(selected,out var left,out var right,out var reach,out var wristError);float gap=clearance.Measure();
                frames[i]=new Pose(bones);result.minimumSeparation=Mathf.Min(result.minimumSeparation,gap);
                result.maximumReachFraction=Mathf.Max(result.maximumReachFraction,reach);result.maximumWristLocalDeviation=Mathf.Max(result.maximumWristLocalDeviation,wristError);
                if(clearance.HasActualContact())result.actualContactFrames++;
                result.frames[i]=new ShoulderClearanceSample{time=i*duration/(native.Length-1),separation=gap,maximumCorrection=Mathf.Max(Quaternion.Angle(Quaternion.identity,left),Quaternion.Angle(Quaternion.identity,right)),leftCorrection=left,rightCorrection=right};
                if(i>0)result.maximumAdjacentCorrectionStepDegrees=Mathf.Max(result.maximumAdjacentCorrectionStepDegrees,
                    Quaternion.Angle(result.frames[i-1].leftCorrection,left),Quaternion.Angle(result.frames[i-1].rightCorrection,right));
            }
            return result;
        }
    }

    sealed class HeadHandClearance
    {
        const float DesiredGap=.010f;
        sealed class SkinInfo {public SkinnedMeshRenderer skin;public int[] groups,triangles;}
        sealed class Geometry {public readonly List<Vector3[]> head=new List<Vector3[]>(),leftTriangles=new List<Vector3[]>(),rightTriangles=new List<Vector3[]>();public readonly List<Vector3> left=new List<Vector3>(),right=new List<Vector3>();}
        readonly SkinInfo[] skins;
        readonly Vector3[] axes;
        public float MinimumSeparation {get;private set;}
        public float MaximumCorrection {get;private set;}
        public Quaternion LeftCorrection {get;private set;}
        public Quaternion RightCorrection {get;private set;}
        public HeadHandClearance(GameObject target,Quaternion basis)
        {
            skins=target.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(skin=>
            {
                var weights=skin.sharedMesh.boneWeights;var groups=new int[weights.Length];
                for(int i=0;i<weights.Length;i++)
                {
                    var w=weights[i];int index=w.boneIndex0;float best=w.weight0;
                    if(w.weight1>best){best=w.weight1;index=w.boneIndex1;}if(w.weight2>best){best=w.weight2;index=w.boneIndex2;}if(w.weight3>best)index=w.boneIndex3;
                    string name=skin.bones[index].name;groups[i]=name=="Head"?1:name=="LeftHand"?2:name=="RightHand"?3:0;
                }
                return new SkinInfo{skin=skin,groups=groups,triangles=skin.sharedMesh.triangles};
            }).ToArray();
            var directions=new List<Vector3>();
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++)
                if(x!=0||y!=0||z!=0)directions.Add(basis*new Vector3(x,y,z).normalized);
            axes=directions.ToArray();
        }
        Geometry ReadGeometry()
        {
            var g=new Geometry();
            foreach(var info in skins)
            {
                var mesh=new Mesh();
                try
                {
                    info.skin.BakeMesh(mesh);var vertices=mesh.vertices.Select(info.skin.transform.TransformPoint).ToArray();
                    for(int i=0;i<vertices.Length;i++){if(info.groups[i]==2)g.left.Add(vertices[i]);else if(info.groups[i]==3)g.right.Add(vertices[i]);}
                    for(int i=0;i<info.triangles.Length;i+=3)
                    {
                        int a=info.triangles[i],b=info.triangles[i+1],c=info.triangles[i+2];
                        if(info.groups[a]==1&&info.groups[b]==1&&info.groups[c]==1)g.head.Add(new[]{vertices[a],vertices[b],vertices[c]});
                        if(info.groups[a]==2&&info.groups[b]==2&&info.groups[c]==2)g.leftTriangles.Add(new[]{vertices[a],vertices[b],vertices[c]});
                        if(info.groups[a]==3&&info.groups[b]==3&&info.groups[c]==3)g.rightTriangles.Add(new[]{vertices[a],vertices[b],vertices[c]});
                    }
                }
                finally{Object.DestroyImmediate(mesh);}
            }
            if(g.head.Count<4||g.left.Count<4||g.right.Count<4)throw new InvalidOperationException("Cannot measure actual Chibi head/fist geometry from skin weights.");
            return g;
        }
        static List<Vector3> ClipY(List<Vector3> input,float height,bool above)
        {
            var output=new List<Vector3>();if(input.Count==0)return output;
            Vector3 a=input[input.Count-1];bool ain=above?a.y>=height:a.y<=height;
            foreach(var b in input)
            {
                bool bin=above?b.y>=height:b.y<=height;
                if(ain!=bin)output.Add(Vector3.LerpUnclamped(a,b,(height-a.y)/(b.y-a.y)));
                if(bin)output.Add(b);a=b;ain=bin;
            }
            return output;
        }
        float Separation(List<Vector3[]> triangles,Vector3[] hand,out Vector3 normal,out Vector3 innerPoint)
        {
            float low=hand.Min(p=>p.y)-DesiredGap,high=hand.Max(p=>p.y)+DesiredGap;
            var band=new List<Vector3>();
            foreach(var triangle in triangles)
            {
                if(triangle.All(p=>p.y<low)||triangle.All(p=>p.y>high))continue;
                band.AddRange(ClipY(ClipY(new List<Vector3>(triangle),low,true),high,false));
            }
            normal=Vector3.forward;innerPoint=hand[0];if(band.Count==0)return 1f;
            float best=float.NegativeInfinity;
            foreach(var axis in axes)
            {
                float headMaximum=float.NegativeInfinity;foreach(var point in band)headMaximum=Mathf.Max(headMaximum,Vector3.Dot(point,axis));
                float handMinimum=float.PositiveInfinity;Vector3 nearest=hand[0];
                foreach(var point in hand){float dot=Vector3.Dot(point,axis);if(dot<handMinimum){handMinimum=dot;nearest=point;}}
                float gap=handMinimum-headMaximum;
                if(gap>best){best=gap;normal=axis;innerPoint=nearest;}
            }
            return best;
        }
        public float Measure()
        {
            var actual=ReadGeometry();
            return Mathf.Min(Separation(actual.head,actual.left.ToArray(),out _,out _),Separation(actual.head,actual.right.ToArray(),out _,out _));
        }
        static bool SegmentHits(Vector3 origin,Vector3 end,Vector3[] triangle)
        {
            var direction=end-origin;var e1=triangle[1]-triangle[0];var e2=triangle[2]-triangle[0];
            var h=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<1e-12f)return false;
            float inv=1/det;var s=origin-triangle[0];float u=Vector3.Dot(s,h)*inv;if(u< -1e-7f||u>1.0000001f)return false;
            var q=Vector3.Cross(s,e1);float v=Vector3.Dot(direction,q)*inv;if(v< -1e-7f||u+v>1.0000001f)return false;
            float time=Vector3.Dot(e2,q)*inv;return time>1e-7f&&time<.9999999f;
        }
        static Bounds TriangleBounds(Vector3[] triangle)
        {var b=new Bounds(triangle[0],Vector3.zero);b.Encapsulate(triangle[1]);b.Encapsulate(triangle[2]);return b;}
        static bool InsideHead(List<Vector3[]> head,Vector3 point)
        {
            double sum=0;
            foreach(var tri in head)
            {
                var a=tri[0]-point;var b=tri[1]-point;var c=tri[2]-point;
                double la=a.magnitude,lb=b.magnitude,lc=c.magnitude;
                double denominator=la*lb*lc+Vector3.Dot(a,b)*lc+Vector3.Dot(b,c)*la+Vector3.Dot(c,a)*lb;
                sum+=2*Math.Atan2(Vector3.Dot(a,Vector3.Cross(b,c)),denominator);
            }
            return Math.Abs(sum/(4*Math.PI))>.55;
        }
        static bool Contacts(List<Vector3[]> head,Bounds[] headBounds,List<Vector3[]> hand,List<Vector3> points)
        {
            var fistBounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)fistBounds.Encapsulate(point);
            var nearby=new List<int>();for(int i=0;i<headBounds.Length;i++)if(fistBounds.Intersects(headBounds[i]))nearby.Add(i);
            foreach(var handTriangle in hand)
            {
                var hb=TriangleBounds(handTriangle);
                foreach(int h in nearby)
                {
                    if(!hb.Intersects(headBounds[h]))continue;var ht=head[h];
                    for(int e=0;e<3;e++)
                        if(SegmentHits(handTriangle[e],handTriangle[(e+1)%3],ht)||SegmentHits(ht[e],ht[(e+1)%3],handTriangle))return true;
                }
            }
            // Surface crossings detect partial entry; whole containment needs an
            // additional volume test. Head groups have open seams, so use a strong
            // winding threshold on several representative fist points.
            var center=Vector3.zero;foreach(var point in points)center+=point;center/=points.Count;
            if(InsideHead(head,center))return true;
            foreach(int index in new[]{0,points.Count/3,points.Count*2/3,points.Count-1})if(InsideHead(head,points[index]))return true;
            return false;
        }
        public bool HasActualContact()
        {
            var actual=ReadGeometry();var bounds=actual.head.Select(TriangleBounds).ToArray();
            return Contacts(actual.head,bounds,actual.leftTriangles,actual.left)||Contacts(actual.head,bounds,actual.rightTriangles,actual.right);
        }
        public void WriteGeometry(GameObject actor,float time,string path)
        {
            var actual=ReadGeometry();var names=new[]{"Hips","Spine02","Spine","Head","LeftArm","LeftForeArm","LeftHand","RightArm","RightForeArm","RightHand"};
            File.WriteAllText(path,JsonUtility.ToJson(new GeometryEvidence{time=time,head=actual.head.SelectMany(t=>t).ToArray(),leftFist=actual.left.ToArray(),rightFist=actual.right.ToArray(),
                leftTriangles=actual.leftTriangles.SelectMany(t=>t).ToArray(),rightTriangles=actual.rightTriangles.SelectMany(t=>t).ToArray(),
                boneNames=names,bonePositions=names.Select(n=>CombatRetargetUtility.Find(actor,n).position).ToArray(),boneRotations=names.Select(n=>CombatRetargetUtility.Find(actor,n).rotation).ToArray()},true));
        }
    }

    static GameObject Clone(string assetPath)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if(!prefab)throw new InvalidOperationException("Missing source asset "+assetPath);
        var result=Object.Instantiate(prefab); result.name=prefab.name;
        result.transform.position=Vector3.zero; result.transform.rotation=Quaternion.identity;
        return result;
    }
    static void DisableAnimators(GameObject actor) { foreach(var a in actor.GetComponentsInChildren<Animator>(true)){a.runtimeAnimatorController=null;a.enabled=false;} }
    static string NativeName(string value) { int bar=value.LastIndexOf('|'); return bar<0?value:value.Substring(bar+1); }
    static float MeshMinimumY(GameObject actor)
    {
        float min=float.PositiveInfinity;
        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh=new Mesh();
            try { skin.BakeMesh(mesh); foreach(var v in mesh.vertices)min=Mathf.Min(min,skin.transform.TransformPoint(v).y); }
            finally { Object.DestroyImmediate(mesh); }
        }
        if(float.IsInfinity(min)||float.IsNaN(min))throw new InvalidOperationException("No finite deformed mesh vertices.");
        return min;
    }
    static AnimationClip MakeGeneric(GameObject actor,Transform[] bones,Pose[] poses,float duration,string name,bool loop)
    {
        int count=poses.Length;
        foreach(var pose in poses)for(int b=0;b<bones.Length;b++)
        {
            for(int a=0;a<3;a++)
                if(float.IsNaN(pose.p[b][a])||float.IsInfinity(pose.p[b][a])||float.IsNaN(pose.s[b][a])||float.IsInfinity(pose.s[b][a]))
                    throw new InvalidOperationException("Non-finite target transform: "+bones[b].name);
            for(int a=0;a<4;a++)
                if(float.IsNaN(pose.q[b][a])||float.IsInfinity(pose.q[b][a]))
                    throw new InvalidOperationException("Non-finite target rotation: "+bones[b].name);
        }
        for(int b=0;b<bones.Length;b++)for(int i=1;i<count;i++)
            if(Quaternion.Dot(poses[i-1].q[b],poses[i].q[b])<0){var q=poses[i].q[b];poses[i].q[b]=new Quaternion(-q.x,-q.y,-q.z,-q.w);}
        var clip=new AnimationClip {name=name,frameRate=Rate,legacy=false};
        for(int b=0;b<bones.Length;b++)
        {
            string path=AnimationUtility.CalculateTransformPath(bones[b],actor.transform);
            for(int a=0;a<3;a++)
            {
                int bone=b,axis=a;
                Put(clip,path,"m_LocalPosition."+"xyz"[a],poses.Select(p=>p.p[bone][axis]).ToArray(),duration,loop);
                Put(clip,path,"m_LocalScale."+"xyz"[a],poses.Select(p=>p.s[bone][axis]).ToArray(),duration,loop);
            }
            for(int a=0;a<4;a++){int bone=b,axis=a;Put(clip,path,"m_LocalRotation."+"xyzw"[a],poses.Select(p=>p.q[bone][axis]).ToArray(),duration,loop);}
        }
        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            foreach(var namePart in new[]{"Grip_R","Grip_L"})
                if(skin.sharedMesh.GetBlendShapeIndex(namePart)>=0)
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(skin.transform,actor.transform),typeof(SkinnedMeshRenderer),"blendShape."+namePart),AnimationCurve.Constant(0,duration,100));
        clip.EnsureQuaternionContinuity();
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.loopBlend=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
        return clip;
    }
    static void Put(AnimationClip clip,string path,string property,float[] values,float duration,bool loop)
    {
        float dt=duration/(values.Length-1);var keys=new Keyframe[values.Length];
        for(int i=0;i<keys.Length;i++)
        {
            float slope=i==0?(values[1]-values[0])/dt:i==keys.Length-1?(values[i]-values[i-1])/dt:(values[i+1]-values[i-1])/(2*dt);
            if(loop&&(i==0||i==keys.Length-1))slope=(values[1]-values[values.Length-2])/(2*dt);
            keys[i]=new Keyframe(i*dt,values[i],slope,slope);
        }
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),new AnimationCurve(keys));
    }
    static void Save(AnimationClip clip,string path)
    {
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing){EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);}
        else AssetDatabase.CreateAsset(clip,path);
    }
}
#endif
