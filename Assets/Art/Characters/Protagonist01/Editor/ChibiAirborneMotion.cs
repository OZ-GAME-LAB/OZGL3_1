#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Offline authoring and review only. Does not connect an Animator or game script.
public static partial class ChibiMotionSet
{
    public const string AirReview = "ArtSources/Characters/Protagonist01/Final/Validation/Airborne20261027";
    const string AirFrames = "Logs/Airborne20261027/Frames";
    static Motion AirMotion(bool rising)
    {
        float duration = rising ? .8f : 1f;
        return new Motion {
            name = rising ? "Jump_Rise_Loop" : "Jump_Fall_Loop", loop = true,
            duration = duration, sourceDuration = duration,
            intent = rising ? "Compact ascending hold with a leading knee and trailing foot; continuous quiet balance, no vertical travel."
                : "Relaxed falling hold, feet underneath the pelvis and elbows slightly open for balance; no vertical travel.",
            phases = new[] { 0f, duration * .25f, duration * .5f, duration * .75f, duration },
            evaluate = (r,t) => EvaluateAirborne(r,t,duration,rising)
        };
    }

    static void EvaluateAirborne(Rig r, float t, float duration, bool rising)
    {
        // Keep the pelvis fixed. The controller owns the jump trajectory.
        // A low amplitude periodic balance cycle must never look like running in air.
        float phase = 2 * Mathf.PI * t / duration;
        float wave = Mathf.Sin(phase), follow = Mathf.Sin(phase - .6f);
        r.Stance(0, rising ? 2.0f + .45f * wave : 1.1f + .55f * wave, 0, 0);
        Vector3 left, right;
        Quaternion lq, rq;
        if (rising)
        {
            left = r.leftFootTarget + new Vector3(0, .078f + .0035f * wave, .065f + .003f * follow);
            right = r.rightFootTarget + new Vector3(0, .033f + .0025f * follow, -.035f);
            lq = Quaternion.AngleAxis(4 + 1.4f * wave, Vector3.right) * r.lfRotation;
            rq = Quaternion.AngleAxis(12 + 1.5f * follow, Vector3.right) * r.rfRotation;
        }
        else
        {
            left = r.leftFootTarget + new Vector3(0, .026f + .003f * follow, .026f);
            right = r.rightFootTarget + new Vector3(0, .043f + .003f * wave, -.018f);
            lq = Quaternion.AngleAxis(7 + 1.2f * follow, Vector3.right) * r.lfRotation;
            rq = Quaternion.AngleAxis(10 + 1.4f * wave, Vector3.right) * r.rfRotation;
        }
        r.Plant(left,right,lq,rq);
        // Move the whole arm through a stable elbow plane. Wrists follow forearms.
        float lift = rising ? .052f : .070f;
        float side = rising ? .018f : .038f;
        r.Hand(true, r.rHandNeutral + new Vector3(r.rightSign * side, lift + .004f * follow, rising ? .040f : .025f));
        r.Hand(false, r.lHandNeutral + new Vector3(r.leftSign * side, lift - .003f * wave, rising ? .018f : .040f));
        r.Fist(true); r.Fist(false); r.Gaze(rising ? -.4f : 1.4f); r.Grip();
    }

    [Serializable] class AirMetric {
        public string name,variant; public float duration,maxVertexStep,loopSeam,rootDrift,maxBoneStep;
        public float minElbowAngle=180,maxElbowAngle,minWristAlignment=1,maxBoundaryTangentDifference;
        public int samples,nonFinite,collapsedTriangles,missingBindings;
        public FrameRow[] markers;
    }
    [Serializable] class AirReviewReport {
        public string method; public bool gameplayChanged=false,modelChanged=false;
        public AirMetric[] clips; public string[] frames;
    }
    static bool AirFinite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);

    [MenuItem("Project M/Chibi Compact v007/Build Airborne Loops Only")]
    public static void BuildAirborneOnly()
    {
        Directory.CreateDirectory(AirReview);
        using(var stage=new Stage()) {
            var actor=CreateActor(stage);
            var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bones=actor.GetComponentsInChildren<Transform>().Where(t=>t!=actor.transform).ToArray();
            using(var rig=new Rig(actor,skins,bones)) {
                rig.MakeNeutral();
                foreach(bool rising in new[]{true,false}) BakeMotion(actor,rig,AirMotion(rising),true);
            }
        }
        AssetDatabase.SaveAssets();
        ReviewAirborneOnly();
    }

    static void CloseAirLoopTangents(AnimationClip clip)
    {
        if(clip.name!="Jump_Rise_Loop"&&clip.name!="Jump_Fall_Loop")return;
        // Equal endpoint derivatives for both the focused and full authoring paths.
        foreach(var binding in AnimationUtility.GetCurveBindings(clip)) {
            var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;
            if(keys.Length<2)continue;
            float tangent=(keys[0].outTangent+keys[keys.Length-1].inTangent)*.5f;
            var first=keys[0];var last=keys[keys.Length-1];
            first.inTangent=first.outTangent=last.inTangent=last.outTangent=tangent;
            keys[0]=first;keys[keys.Length-1]=last;curve.keys=keys;
            AnimationUtility.SetEditorCurve(clip,binding,curve);
        }
    }

    public static void ReviewAirborneOnly()
    {
        Directory.CreateDirectory(AirReview);
        var rows=new List<AirMetric>();var captures=new List<string>();
        foreach(string variant in new[]{"MotionReview","SwordGrip","StaffGrip"})
        using(var stage=new Stage()) {
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Protagonist_Chibi_"+variant+".prefab"));
            SceneManager.MoveGameObjectToScene(actor,stage.scene);
            var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            var bones=actor.GetComponentsInChildren<Transform>().Where(t=>t!=actor.transform&&!t.name.Contains("Socket")).ToArray();
            using(var rig=new Rig(actor,skins,bones)) {
                rig.MakeNeutral();
                foreach(bool rising in new[]{true,false}) {
                    var motion=AirMotion(rising);
                    var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+motion.name+".anim");
                    actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                    rows.Add(AuditAir(actor,rig,clip,variant));
                    var cpu=skins.Select(s=>new CpuSkin(s)).ToArray();
                    try {
                        // Preview offset is applied to the disposable parent only.
                        actor.transform.SetPositionAndRotation(new Vector3(0,.28f,0),Quaternion.Euler(0,90,0));
                        foreach(string view in variant=="MotionReview" ? new[]{"Side","Front","Opposite","ThreeQuarter"}:new[]{"Front"}) {
                            Vector3 offset=view=="Side"?new Vector3(1.05f,2,-10):view=="Front"?new Vector3(10,1.6f,0):view=="Opposite"?new Vector3(-1.05f,2,10):new Vector3(7,3,-7);
                            stage.SetView(new Vector3(0,.88f,0),offset,.86f);
                            int count=view=="Side"?Mathf.RoundToInt(clip.length*2*RenderRate):9;
                            string folder=AirFrames+"/"+variant+"/"+clip.name+"/"+view;
                            Directory.CreateDirectory(folder);
                            for(int i=0;i<count;i++) {
                                float time=view=="Side"?Mathf.Repeat(i/(float)RenderRate,clip.length):clip.length*i/(count-1);
                                clip.SampleAnimation(actor,time);foreach(var skin in cpu)skin.Bake();
                                stage.Save(folder+"/"+i.ToString("D4")+".png");
                            }
                            captures.Add(folder);
                        }
                    } finally {foreach(var skin in cpu)skin.Dispose();}
                }
                if(variant=="MotionReview") CaptureAirSequence(stage,actor,rig,captures);
            }
            Object.DestroyImmediate(actor);
        }
        File.WriteAllText(AirReview+"/unity-review.json",JsonUtility.ToJson(new AirReviewReport {
            method="Saved native24-bone Generic clips reloaded and sampled at120Hz; actual skin vertices, local rotation steps, loop endpoints, binding paths, elbows and wrist-follow alignment. Full side loops at30fps and9phases in3otherviews; 2additional hand-mesh variants checked. Sequence uses offline pose blends and an explicitly preview-only parent trajectory.",
            clips=rows.ToArray(),frames=captures.ToArray()},true));
        if(rows.Any(r=>r.nonFinite>0||r.collapsedTriangles>0||r.missingBindings>0||r.loopSeam>.0001f))throw new Exception("Airborne validation failed");
        Debug.Log("AIRBORNE_REVIEW_READY");
    }

    static AirMetric AuditAir(GameObject actor,Rig rig,AnimationClip clip,string variant)
    {
        var result=new AirMetric{name=clip.name,variant=variant,duration=clip.length};
        result.missingBindings=AnimationUtility.GetCurveBindings(clip).Count(b=>!actor.transform.Find(b.path));
        foreach(var binding in AnimationUtility.GetCurveBindings(clip)) {
            var keys=AnimationUtility.GetEditorCurve(clip,binding).keys;
            if(keys.Length>1)result.maxBoundaryTangentDifference=Mathf.Max(result.maxBoundaryTangentDifference,Mathf.Abs(keys[0].outTangent-keys[keys.Length-1].inTangent));
        }
        var meshes=rig.skins.Select(s=>new Mesh()).ToArray();
        Vector3[][] first=null,previous=null;Quaternion[] previousQ=null;
        Vector3 initialHip=Vector3.zero;var markers=new List<FrameRow>();
        int count=Mathf.RoundToInt(clip.length*120)+1;
        try {
            for(int i=0;i<count;i++) {
                float time=clip.length*i/(count-1);clip.SampleAnimation(actor,time);
                var vertices=new Vector3[meshes.Length][];
                for(int m=0;m<meshes.Length;m++) {
                    rig.skins[m].BakeMesh(meshes[m]);vertices[m]=meshes[m].vertices.Select(v=>rig.skins[m].transform.TransformPoint(v)).ToArray();
                    var tri=meshes[m].triangles;
                    foreach(var v in vertices[m])if(!AirFinite(v.x)||!AirFinite(v.y)||!AirFinite(v.z))result.nonFinite++;
                    for(int j=0;j<tri.Length;j+=3)if(Vector3.Cross(vertices[m][tri[j+1]]-vertices[m][tri[j]],vertices[m][tri[j+2]]-vertices[m][tri[j]]).sqrMagnitude<4e-20f)result.collapsedTriangles++;
                    if(previous!=null)for(int j=0;j<vertices[m].Length;j++)result.maxVertexStep=Mathf.Max(result.maxVertexStep,Vector3.Distance(previous[m][j],vertices[m][j]));
                    if(i==count-1)for(int j=0;j<vertices[m].Length;j++)result.loopSeam=Mathf.Max(result.loopSeam,Vector3.Distance(first[m][j],vertices[m][j]));
                }
                if(i==0){first=vertices;initialHip=rig.hips.position;}
                previous=vertices;
                result.rootDrift=Mathf.Max(result.rootDrift,Vector3.Distance(initialHip,rig.hips.position));
                var q=rig.transforms.Select(t=>t.localRotation).ToArray();
                if(previousQ!=null)for(int j=0;j<q.Length;j++) {
                    var delta=Quaternion.Inverse(previousQ[j])*q[j];
                    float angle=2*Mathf.Atan2(new Vector3(delta.x,delta.y,delta.z).magnitude,Mathf.Abs(delta.w))*Mathf.Rad2Deg;
                    result.maxBoneStep=Mathf.Max(result.maxBoneStep,angle);
                }
                previousQ=q;
                foreach(bool right in new[]{true,false}) {
                    var shoulder=right?rig.rua:rig.lua;var elbow=right?rig.rla:rig.lla;var hand=right?rig.rh:rig.lh;
                    float angle=Vector3.Angle(shoulder.position-elbow.position,hand.position-elbow.position);
                    result.minElbowAngle=Mathf.Min(result.minElbowAngle,angle);result.maxElbowAngle=Mathf.Max(result.maxElbowAngle,angle);
                    var expected=Quaternion.FromToRotation(right?rig.rForearmNeutral:rig.lForearmNeutral,(hand.position-elbow.position).normalized)*(right?rig.rhRotation:rig.lhRotation);
                    result.minWristAlignment=Mathf.Min(result.minWristAlignment,Mathf.Abs(Quaternion.Dot(expected,hand.rotation)));
                }
                markers.Add(rig.Frame(time));
            }
        } finally {foreach(var m in meshes)Object.DestroyImmediate(m);}
        result.samples=count;result.markers=markers.ToArray();return result;
    }

    static void CaptureAirSequence(Stage stage,GameObject actor,Rig rig,List<string> captures)
    {
        var cpu=rig.skins.Select(s=>new CpuSkin(s)).ToArray();
        Pose Read(string name,float t)=>ReadPose(actor,rig,name,t);
        Pose Blend(Pose a,Pose b,float u)=>Mix(a,b,S01(u));
        const float takeoff=5f/30f, landAt=1.02f;
        try {
            foreach(string view in new[]{"Side","Front"}) {
                stage.SetView(new Vector3(0,.83f,0),view=="Side"?new Vector3(1.05f,2,-10):new Vector3(10,1.6f,0),1f);
                var folder=AirFrames+"/Sequence/"+view;Directory.CreateDirectory(folder);
                for(int frame=0;frame<54;frame++) {
                    float t=frame/(float)RenderRate;actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,90,0));Pose pose;
                    if(t<=takeoff)pose=Read("Jump_Takeoff",t);
                    else if(t<.29f)pose=Blend(Read("Jump_Takeoff",takeoff),Read("Jump_Rise_Loop",t-takeoff),(t-takeoff)/(.29f-takeoff));
                    else if(t<.50f)pose=Read("Jump_Rise_Loop",t-takeoff);
                    else if(t<.68f)pose=Blend(Read("Jump_Rise_Loop",t-takeoff),Read("Jump_Fall_Loop",t-.50f),(t-.50f)/.18f);
                    else if(t<.91f)pose=Read("Jump_Fall_Loop",t-.50f);
                    else if(t<landAt)pose=Blend(Read("Jump_Fall_Loop",t-.50f),Read("Jump_Land",0),(t-.91f)/(landAt-.91f));
                    else pose=Read("Jump_Land",t-landAt);
                    ApplyPose(rig,pose);
                    if(t>takeoff&&t<landAt){float u=(t-takeoff)/(landAt-takeoff);actor.transform.position=Vector3.up*(.42f*4*u*(1-u));}
                    foreach(var skin in cpu)skin.Bake();stage.Save(folder+"/"+frame.ToString("D4")+".png");
                }
                captures.Add(folder);
            }
        } finally {foreach(var skin in cpu)skin.Dispose();}
    }
}
#endif
