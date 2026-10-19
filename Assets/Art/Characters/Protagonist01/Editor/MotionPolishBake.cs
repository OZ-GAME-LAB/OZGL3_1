#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEditor;
using Object = UnityEngine.Object;

// Offline pose editing and curve baking. This class is editor-only; it has no game update path.
public static class MotionPolishBake
{
    const string Root = MotionPolishBuilder.Root;
    const string Report = MotionPolishBuilder.Report;
    const int FPS = 60;
    sealed class Pose { public Vector3[] p; public Quaternion[] q; public Vector3 hip, lf, rf; public float ly,ry,yaw; }
    static GameObject actor;
    static Animator animator;
    static Transform[] bones;
    static Transform hip, spine, chest, head, lu, ll, lf, ru, rl, rf, la, le, lh, ra, re, rh;
    static SkinnedMeshRenderer skin;
    static Mesh baked;
    static int[] soleL,soleR;
    static Pose neutral;
    static Quaternion leftFootRest,rightFootRest;
    static Vector3 leftFootPosition,rightFootPosition;
    static float soleOffsetL,soleOffsetR;
    static readonly List<string> notes = new List<string>();
    static bool skipHandBuild;
    public static void BuildAndInspect() { Build(); MotionPolishReviewCapture.CaptureDiagnosticFrames(); MotionPolishReviewCapture.CaptureHands(); }
    public static void BuildMotionsAndInspect() { BuildMotionsOnly(); MotionPolishReviewCapture.ConfigureFinalReview(); MotionPolishReviewCapture.CaptureDiagnosticFrames(); }
    public static void BuildFinalReview() { Build(); CombatVfxBuilder.Build(); MotionPolishReviewCapture.CaptureFinalReview(); }
    public static void BuildMotionsOnly() { skipHandBuild=true; try { Build(); } finally { skipHandBuild=false; } }
    public static void RefreshHandsAndVfx()
    {
        MotionPolishHandBuilder.Build();
        var p=PrefabUtility.LoadPrefabContents(Root+"/Prefabs/Protagonist01_MotionReview.prefab");
        try
        {
            var s=p.GetComponentInChildren<SkinnedMeshRenderer>();s.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/Protagonist01_PolishedHands.asset");
            for(int i=0;i<s.sharedMesh.blendShapeCount;i++)s.SetBlendShapeWeight(i,100);
            p.GetComponent<Animator>().avatar=null;
            PrefabUtility.SaveAsPrefabAsset(p,Root+"/Prefabs/Protagonist01_MotionReview.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(p);}
        CombatVfxBuilder.Build();AssetDatabase.SaveAssets();
    }

    public static void Build()
    {
        notes.Clear();
        if(!skipHandBuild) MotionPolishHandBuilder.Build();
        MotionPolishBuilder.BuildReviewPrefab();
        actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/Protagonist01_MotionReview.prefab"));
        animator = actor.GetComponent<Animator>();
        animator.avatar = AssetDatabase.LoadAllAssetsAtPath(MotionPolishBuilder.Original+"/Protagonist01_Rig.fbx").OfType<Avatar>().First();
        animator.runtimeAnimatorController=null; animator.applyRootMotion=false; animator.Rebind();
        bones=actor.GetComponentsInChildren<Transform>().Where(t=>t!=actor.transform && !t.name.Contains("Socket") && t.GetComponent<SkinnedMeshRenderer>()==null).ToArray();
        hip=B("Hips"); spine=B("Spine02"); chest=B("Spine"); head=B("Head");
        lu=B("LeftUpLeg");ll=B("LeftLeg");lf=B("LeftFoot");ru=B("RightUpLeg");rl=B("RightLeg");rf=B("RightFoot");
        la=B("LeftArm");le=B("LeftForeArm");lh=B("LeftHand");ra=B("RightArm");re=B("RightForeArm");rh=B("RightHand");
        skin=actor.GetComponentInChildren<SkinnedMeshRenderer>(); baked=new Mesh();
        soleL=SoleIndices("LeftFoot","LeftToeBase");soleR=SoleIndices("RightFoot","RightToeBase");
        try
        {
            var idle=Read("Idle_Fists");neutral=idle[Mathf.Min(60,idle.Length-1)];Apply(neutral);
            float floor=Mathf.Min(neutral.ly,neutral.ry);hip.position+=Vector3.up*(-floor+.004f);
            neutral=Get();leftFootRest=lf.rotation;rightFootRest=rf.rotation;
            leftFootPosition=lf.position+Vector3.up*(.006f-neutral.ly);rightFootPosition=rf.position+Vector3.up*(.006f-neutral.ry);
            soleOffsetL=lf.position.y-neutral.ly;soleOffsetR=rf.position.y-neutral.ry;
            BakeNatural("Idle_Fists",idle,true);
            BakeNatural("Walk_Fists",Read("Walk_Fists"),true);
            BakeNatural("Run_Fists",Read("Run_Fists"),true);
            BakeJump(Read("Jump_Full"));
            BakeNatural("Hit_Reaction",Read("Hit_Reaction"),false);
            BakeNatural("Interact_Reach",Read("Interact_Reach"),false);
            BakeNatural("Item_Throw_Overhand",Read("Item_Throw_Overhand"),false);
            var pickup=Read("Pickup_Low");var pick=new List<Pose>();
            for(int i=0;i<=156;i++)
            {
                float t=i/60f, u=t<1.25f?Mathf.Lerp(2.85f,1.15f,Smooth(t/1.25f)):Mathf.Lerp(1.15f,2.85f,Smooth((t-1.35f)/1.25f));
                Apply(At(pickup,u)); pick.Add(Get());
            }
            BakeNatural("Pickup_Low",pick.ToArray(),false);
            Author("Sword_Horizontal_Slash",1.35f,Slash);
            Author("Sword_Aerial_Plunge",1.55f,Plunge);
            Author("Dash_Forward",.85f,Dash);
            Author("Staff_Cast_Forward",1.50f,Staff);
            Author("Punch_Right_Jab",1.05f,Punch);
            Author("Potion_Drink",2.8f,Drink);
            var prefab=PrefabUtility.LoadPrefabContents(Root+"/Prefabs/Protagonist01_MotionReview.prefab");
            try { prefab.GetComponent<Animator>().avatar=null; PrefabUtility.SaveAsPrefabAsset(prefab,Root+"/Prefabs/Protagonist01_MotionReview.prefab"); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            AssetDatabase.SaveAssets();File.WriteAllLines(Report+"/polish-bake-report.txt",notes);
            Debug.Log("V004_POLISHED_CLIPS_BAKED "+notes.Count);
        }
        finally {Object.DestroyImmediate(baked);Object.DestroyImmediate(actor);}
    }
    static Transform B(string name)=>actor.GetComponentsInChildren<Transform>().First(t=>t.name==name);
    static float Smooth(float x)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(x));
    static float Pulse(float t,float a,float b,float c)=>Smooth((t-a)/(b-a))*(1-Smooth((t-b)/(c-b)));
    static Pose Get()
    {
        skin.BakeMesh(baked);var v=baked.vertices;
        float Min(int[] ids)=>ids.Min(i=>skin.transform.TransformPoint(v[i]).y);
        return new Pose {p=bones.Select(b=>b.localPosition).ToArray(),q=bones.Select(b=>b.localRotation).ToArray(),hip=hip.position,lf=lf.position,rf=rf.position,
            ly=Min(soleL),ry=Min(soleR),yaw=Yaw()};
    }
    static float Yaw()
    {
        var axis=ru.position-lu.position;axis.y=0;
        return Mathf.Atan2(-axis.z,axis.x)*Mathf.Rad2Deg;
    }
    static void Apply(Pose p)
    {for(int j=0;j<bones.Length;j++){bones[j].localPosition=p.p[j];bones[j].localRotation=p.q[j];}}
    static Pose At(Pose[] p,float t)
    {
        float f=Mathf.Clamp(t*FPS,0,p.Length-1);int a=Mathf.FloorToInt(f),b=Mathf.Min(p.Length-1,a+1);float u=f-a;
        var r=new Pose{p=new Vector3[bones.Length],q=new Quaternion[bones.Length]};
        for(int j=0;j<bones.Length;j++){r.p[j]=Vector3.Lerp(p[a].p[j],p[b].p[j],u);r.q[j]=Quaternion.Slerp(p[a].q[j],p[b].q[j],u);}return r;
    }
    static Pose[] Read(string name)
    {
        var clip=AssetDatabase.LoadAllAssetsAtPath(Root+"/Sources/"+name+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
        var g=PlayableGraph.Create();g.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var p=AnimationClipPlayable.Create(g,clip);p.SetApplyFootIK(false);p.SetApplyPlayableIK(false);
        AnimationPlayableOutput.Create(g,"Source",animator).SetSourcePlayable(p);g.Play();
        var frames=new Pose[Mathf.RoundToInt(clip.length*FPS)+1];
        for(int i=0;i<frames.Length;i++){p.SetTime(Mathf.Min(i/(float)FPS,clip.length-.00001f));g.Evaluate(0);frames[i]=Get();}
        g.Destroy();return frames;
    }
    static int[] SoleIndices(params string[] names)
    {
        var mesh=skin.sharedMesh;var ids=new HashSet<int>(names.Select(n=>Array.FindIndex(skin.bones,b=>b.name==n)));
        var output=new List<int>();using(var counts=mesh.GetBonesPerVertex())using(var weights=mesh.GetAllBoneWeights())
        {int off=0;for(int i=0;i<counts.Length;i++){float w=0;for(int k=0;k<counts[i];k++){var a=weights[off++];if(ids.Contains(a.boneIndex))w+=a.weight;}if(w>.75f)output.Add(i);}}
        return output.ToArray();
    }
    static void BakeNatural(string name,Pose[] source,bool loop)
    {
        source=Filter(source);
        int start=0,end=source.Length-1;
        if(name.StartsWith("Walk")||name.StartsWith("Run"))
        {
            float desired=name.StartsWith("Walk")?1.1f:.75f,best=float.MaxValue;
            for(int a=15;a<source.Length-45;a+=2)
                for(int gap=Mathf.RoundToInt((desired-.25f)*FPS);gap<=Mathf.RoundToInt((desired+.25f)*FPS);gap++)
                {
                    int b=a+gap;if(b>=source.Length-3)continue;float score=0;
                    for(int j=0;j<bones.Length;j++)score+=Mathf.Pow(Quaternion.Angle(source[a].q[j],source[b].q[j]),2)*(bones[j].name.Contains("Leg")?3:1);
                    score+=Mathf.Pow(gap/FPS-desired,2)*20;
                    if(score<best){best=score;start=a;end=b;}
                }
            notes.Add(name+" cycle selected source "+(start/60f).ToString("F3")+".."+(end/60f).ToString("F3"));
        }
        var output=new List<Pose>();
        Vector3 begin=source[start].hip,finish=source[end].hip;float yaw0=source[start].yaw;
        bool travel=name.StartsWith("Walk")||name.StartsWith("Run");
        for(int i=start;i<=end;i++)
        {
            Apply(source[i]);float u=(i-start)/(float)(end-start);
            Vector3 remove=travel?Vector3.Lerp(begin,finish,u):begin;
            hip.position-=new Vector3(remove.x-neutral.hip.x,0,remove.z-neutral.hip.z);
            if(!travel && name!="Jump_Full")
            {
                float correction=-Mathf.DeltaAngle(0,source[i].yaw);
                hip.rotation=Quaternion.AngleAxis(correction,Vector3.up)*hip.rotation;
            }
            if(name=="Pickup_Low")
            {
                float reach=Pulse(u,0,.5f,1);hip.position+=Vector3.down*(.13f*reach);
                Vector3 handGoal=Vector3.Lerp(rh.position,V(.17f,.17f,.26f),reach);
                Hand(true,handGoal);
            }
            if(name=="Idle_Fists"||name=="Interact_Reach"||name=="Pickup_Low")
            {
                PlantFeet(leftFootPosition,rightFootPosition);
            }
            // Solve only the legs at penetrated support contacts; torso/root is never floor-snapped.
            CorrectFeet();
            output.Add(Get());
        }
        if(loop)
        {
            var a=output[0];var b=output[output.Count-1];
            for(int i=0;i<output.Count;i++)
            {
                float blend=Smooth(i/(float)(output.Count-1));
                for(int j=0;j<bones.Length;j++)
                {
                    output[i].p[j]+= (a.p[j]-b.p[j])*blend;
                    output[i].q[j]=Quaternion.Slerp(Quaternion.identity,a.q[j]*Quaternion.Inverse(b.q[j]),blend)*output[i].q[j];
                }
            }
        }
        if(loop) output=PeriodicFilter(output);
        for(int i=0;i<output.Count;i++){Apply(output[i]);CorrectFeet();output[i]=Get();}
        Save(name,output.ToArray(),loop,name.Contains("Throw")?75:100);
    }
    static List<Pose> PeriodicFilter(List<Pose> input)
    {
        int count=input.Count-1;var result=new List<Pose>();
        for(int i=0;i<=count;i++)
        {
            var p=new Pose{p=new Vector3[bones.Length],q=new Quaternion[bones.Length]};
            for(int j=0;j<bones.Length;j++)
            {
                float sum=0;Quaternion rot=Quaternion.identity;Vector3 pos=Vector3.zero;
                for(int k=-6;k<=6;k++)
                {
                    int n=((i+k)%count+count)%count;float w=Mathf.Exp(-k*k/11.52f);
                    pos+=input[n].p[j]*w;rot=sum==0?input[n].q[j]:Quaternion.Slerp(rot,input[n].q[j],w/(sum+w));sum+=w;
                }
                p.p[j]=pos/sum;p.q[j]=rot;
            }
            result.Add(p);
        }
        return result;
    }
    static void BakeJump(Pose[] source)
    {
        source=Filter(source);var frames=new List<Pose>();
        Pose Grounded(float sourceTime)
        {
            Apply(At(source,sourceTime));var p=hip.position;p.x=neutral.hip.x;p.z=neutral.hip.z;hip.position=p;
            PlantFeet(leftFootPosition,rightFootPosition);CorrectFeet();return Get();
        }
        // The flight curve starts from the actual planted takeoff pose. Using the raw
        // source pelvis here would instantly undo its anatomical reach correction.
        var takeoff=Grounded(1.1f);var landing=Grounded(1.6f);
        for(int i=0;i<=163;i++)
        {
            float t=i/60f;
            float sourceTime=t<.6f?Mathf.Lerp(.3f,1.1f,t/.6f):t<1.35f?Mathf.Lerp(1.1f,1.6f,(t-.6f)/.75f):1.6f+t-1.35f;
            Apply(At(source,sourceTime));var h=hip.position;h.x=neutral.hip.x;h.z=neutral.hip.z;hip.position=h;
            float air=Mathf.Clamp01((t-.6f)/.75f);bool airborne=t>.6f&&t<1.35f;
            if(airborne)
            {
                float blendOut=1-Smooth(air/.18f),blendIn=Smooth((air-.82f)/.18f);
                for(int j=0;j<bones.Length;j++)
                {
                    bones[j].localPosition=Vector3.Lerp(Vector3.Lerp(bones[j].localPosition,takeoff.p[j],blendOut),landing.p[j],blendIn);
                    bones[j].localRotation=Quaternion.Slerp(Quaternion.Slerp(bones[j].localRotation,takeoff.q[j],blendOut),landing.q[j],blendIn);
                }
                hip.position=Vector3.Lerp(takeoff.hip,landing.hip,air)+Vector3.up*(.80f*4*air*(1-air));
            }
            else
            {
                PlantFeet(leftFootPosition,rightFootPosition);
            }
            CorrectFeet();frames.Add(Get());
        }
        Save("Jump_Full",frames.ToArray(),false,100);
    }
    static Pose[] Filter(Pose[] input)
    {
        var output=new Pose[input.Length];
        for(int i=0;i<input.Length;i++)
        {
            Apply(input[i]);var pose=Get();
            for(int j=0;j<bones.Length;j++)
            {
                var before=input[Mathf.Max(0,i-2)];var after=input[Mathf.Min(input.Length-1,i+2)];
                pose.p[j]=input[i].p[j]*.6f+(before.p[j]+after.p[j])*.2f;
                pose.q[j]=Quaternion.Slerp(input[i].q[j],Quaternion.Slerp(before.q[j],after.q[j],.5f),.4f);
            }
            Apply(pose);output[i]=Get();
        }
        return output;
    }
    static void CorrectFeet()
    {
        var p=Get();
        if(p.ly<.006f){var pos=lf.position;pos.y+=.006f-p.ly;Solve(lu,ll,lf,pos,lu.position+Vector3.forward);}
        if(p.ry<.006f){var pos=rf.position;pos.y+=.006f-p.ry;Solve(ru,rl,rf,pos,ru.position+Vector3.forward);}
    }
    static void Solve(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 hint,float weight=1)
    {
        var p0=upper.position;var p1=lower.position;var p2=end.position;var endRotation=end.rotation;
        target=Vector3.Lerp(p2,target,weight);
        float a=Vector3.Distance(p0,p1),b=Vector3.Distance(p1,p2),d=Mathf.Clamp(Vector3.Distance(p0,target),Mathf.Abs(a-b)+.0001f,a+b-.0001f);
        var dir=(target-p0).normalized;var bend=hint-p0; bend-=dir*Vector3.Dot(bend,dir);
        if(bend.sqrMagnitude<.000001f)bend=Vector3.Cross(dir,Vector3.right);bend.Normalize();
        float along=(a*a+d*d-b*b)/(2*d);float height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));var knee=p0+dir*along+bend*height;
        upper.rotation=Quaternion.FromToRotation(p1-p0,knee-p0)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(end.position-lower.position,target-lower.position)*lower.rotation;
        end.rotation=endRotation;
    }
    static void Stance(float drop=0,float yaw=0,float lean=0)
    {
        Apply(neutral);hip.position+=new Vector3(0,-drop,0);
        hip.rotation=Quaternion.AngleAxis(yaw,Vector3.up)*hip.rotation;
        spine.rotation=Quaternion.AngleAxis(lean,Vector3.right)*spine.rotation;
        head.rotation=Quaternion.AngleAxis(-yaw*.35f,Vector3.up)*head.rotation;
        var l=leftFootPosition+new Vector3(-.015f,0,.08f);var r=rightFootPosition+new Vector3(.015f,0,-.08f);
        PlantFeet(l,r);
    }
    static void PlantFeet(Vector3 left,Vector3 right)
    {
        float ReachDrop(Transform upper,Transform lower,Transform end,Vector3 target)
        {
            float reach=(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,end.position))*.995f;
            var flat=upper.position-target;flat.y=0;
            float vertical=Mathf.Sqrt(Mathf.Max(.0001f,reach*reach-flat.sqrMagnitude));
            return Mathf.Max(0,upper.position.y-target.y-vertical);
        }
        // Offline pelvis fitting obeys limb reach before solving planted feet. It is not a
        // per-frame skin-to-floor snap and does not alter airborne poses or the game root.
        float drop=Mathf.Max(ReachDrop(lu,ll,lf,left),ReachDrop(ru,rl,rf,right));hip.position-=Vector3.up*drop;
        Solve(lu,ll,lf,left,lu.position+Vector3.forward);Solve(ru,rl,rf,right,ru.position+Vector3.forward);
        lf.rotation=leftFootRest;rf.rotation=rightFootRest;
    }
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static void Hand(bool right,Vector3 position,Vector3? blade=null)
    {
        Transform upper=right?ra:la,elbow=right?re:le,hand=right?rh:lh;
        var hint=V(right?.7f:-.7f,.8f,-.15f);
        Solve(upper,elbow,hand,position,hint);
        if(blade.HasValue)
        {
            Vector3 axis=right?Vector3.left:Vector3.right;
            hand.rotation=Quaternion.FromToRotation(hand.TransformDirection(axis),blade.Value.normalized)*hand.rotation;
        }
    }
    static void Author(string name,float length,Action<float> action)
    {
        var frames=new List<Pose>();
        for(int i=0;i<=Mathf.RoundToInt(length*FPS);i++){action(i/(float)FPS);CorrectFeet();frames.Add(Get());}
        Save(name,frames.ToArray(),false,name.StartsWith("Potion")?75:100);
    }
    static void Slash(float t)
    {
        float wind=Smooth(t/.32f),cut=Smooth((t-.32f)/.20f),recover=Smooth((t-.70f)/.65f);
        float yaw=(-22*wind+45*cut)*(1-recover),drop=.035f*Pulse(t,0,.40f,1.15f);
        Stance(drop,yaw,3*Pulse(t,.25f,.5f,.9f));
        var ready=V(.23f,.91f,.18f);var back=V(.38f,.96f,-.14f);var across=V(-.21f,.94f,.38f);
        Vector3 pos=Vector3.Lerp(Vector3.Lerp(ready,back,wind),across,cut);pos=Vector3.Lerp(pos,ready,recover);
        Vector3 dir=Vector3.Slerp(Vector3.Slerp(V(.4f,.1f,.9f),V(.95f,0,-.3f),wind),V(-.9f,0,.4f),cut);dir=Vector3.Slerp(dir,V(.4f,.1f,.9f),recover);
        Hand(true,pos,dir);Hand(false,V(-.29f,.84f,.07f));
    }
    static void Plunge(float t)
    {
        float fall=Mathf.Clamp01((t-.22f)/.36f),recover=Smooth((t-.80f)/.75f);float height=1.55f*(1-fall*fall);
        if(t<.22f)height+=.08f*Mathf.Sin(Mathf.PI*t/.22f);
        float compress=.19f*Pulse(t,.53f,.67f,1.05f);Stance(compress,0,7*(1-recover));
        hip.position+=Vector3.up*height;
        if(height>.015f)
        {float tuck=.22f*(1-Smooth(fall));Solve(lu,ll,lf,V(-.13f,height+.17f+tuck,.10f),V(-.1f,height+.6f,.5f));Solve(ru,rl,rf,V(.13f,height+.17f+tuck,-.04f),V(.1f,height+.6f,.5f));}
        var strike=V(.04f,height+.82f-compress+.13f*(1-Smooth(fall)),.36f);var ready=V(.23f,.91f,.18f);
        Hand(true,Vector3.Lerp(strike,ready,recover),Vector3.Slerp(Vector3.down,V(.4f,.1f,.9f),recover));
        Hand(false,Vector3.Lerp(strike+V(-.09f,.04f,-.02f),V(-.29f,.84f,.07f),recover),Vector3.down);
    }
    static void Dash(float t)
    {
        float compress=Pulse(t,0,.13f,.29f),drive=Pulse(t,.10f,.29f,.71f),recover=Smooth((t-.56f)/.29f);
        Stance(.07f*compress+.09f*drive,0,18*drive);
        hip.position+=V(0,0,.06f*drive);
        Solve(lu,ll,lf,leftFootPosition+V(0,0,.22f*drive),V(-.1f,.5f,.5f));
        Solve(ru,rl,rf,rightFootPosition+V(0,.04f*drive,-.20f*drive),V(.1f,.5f,.4f));
        Hand(true,Vector3.Lerp(V(.27f,.85f,.16f),V(.30f,.94f,-.24f),drive));
        Hand(false,Vector3.Lerp(V(-.25f,.85f,.12f),V(-.24f,.99f,.29f),drive));
    }
    static void Staff(float t)
    {
        float coil=Smooth(t/.4f),cast=Smooth((t-.4f)/.27f),back=Smooth((t-.85f)/.65f);
        Stance(.025f*Pulse(t,0,.4f,1.3f),Mathf.Lerp(-9,10,cast)*(1-back),3*Pulse(t,.35f,.65f,1.1f));
        var ready=V(.26f,.83f,.16f);var charge=V(.29f,.96f,-.03f);var release=V(.19f,1.04f,.48f);
        Hand(true,Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(ready,charge,coil),release,cast),ready,back),Vector3.Slerp(Vector3.up,V(0,.22f,1),cast*(1-back)));
        Hand(false,Vector3.Lerp(V(-.23f,.84f,.1f),V(-.26f,1.02f,.22f),Pulse(t,.1f,.55f,1.35f)));
    }
    static void Punch(float t)
    {
        float wind=Smooth(t/.20f),strike=Smooth((t-.20f)/.13f),back=Smooth((t-.40f)/.40f),settle=Smooth((t-.80f)/.25f);
        Stance(.025f*Pulse(t,0,.22f,.7f),(-9*wind+21*strike)*(1-back),5*Pulse(t,.16f,.33f,.7f));
        var ready=V(.22f,1.05f,.19f);var drawn=V(.27f,1.02f,.02f);var hit=V(.09f,1.08f,.54f);
        Hand(true,Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(ready,drawn,wind),hit,strike),ready,back));
        Hand(false,V(-.18f,1.08f,.19f));
        // The source idle wrist points down. A jab needs the knuckles on the same
        // axis as the forearm, including the return to guard.
        AlignFist(rh,re,Vector3.right);AlignFist(lh,le,Vector3.left);
    }
    static void AlignFist(Transform hand,Transform elbow,Vector3 thumbReference)
    {
        var along=(hand.position-elbow.position).normalized;
        var thumb=Vector3.ProjectOnPlane(thumbReference,along).normalized;
        // A fixed anatomical frame avoids the arbitrary roll axis of FromToRotation
        // when the idle wrist and the advancing forearm become antiparallel.
        hand.rotation=Quaternion.LookRotation(thumb,along);
    }
    static void Drink(float t)
    {
        float up=Smooth((t-.20f)/.7f),down=Smooth((t-1.72f)/.80f);float sip=up*(1-down);
        Stance(0,0,0);head.rotation=Quaternion.AngleAxis(-6*sip,Vector3.right)*head.rotation;
        var mouth=head.position+V(0,.14f,.15f);var ready=V(.24f,.88f,.18f);
        Vector3 direction=Vector3.Slerp(Vector3.up,V(0,.5f,-.86f),sip);
        Hand(true,ready,direction);
        Vector3 bottleGrip=mouth-rh.rotation*(Vector3.up*.045f+Vector3.left*.154f);
        Hand(true,Vector3.Lerp(ready,bottleGrip,sip),direction);
        Hand(false,V(-.25f,.78f,.06f));
    }
    static void Save(string name,Pose[] frames,bool loop,float grip)
    {
        var clip=new AnimationClip{name=name,frameRate=FPS,legacy=false};float duration=(frames.Length-1)/(float)FPS;
        string[] axes={"x","y","z","w"};
        for(int j=0;j<bones.Length;j++)
        {
            string path=AnimationUtility.CalculateTransformPath(bones[j],actor.transform);
            for(int axis=0;axis<3;axis++)
            {
                var keys=new Keyframe[frames.Length];for(int i=0;i<frames.Length;i++)keys[i]=new Keyframe(i/(float)FPS,frames[i].p[j][axis]);
                var curve=new AnimationCurve(keys);SmoothCurve(curve,loop);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalPosition."+axes[axis]),curve);
            }
            for(int axis=0;axis<4;axis++)
            {
                var keys=new Keyframe[frames.Length];Quaternion prev=frames[0].q[j];
                for(int i=0;i<frames.Length;i++){Quaternion q=frames[i].q[j];if(Quaternion.Dot(prev,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);keys[i]=new Keyframe(i/(float)FPS,q[axis]);prev=q;}
                var curve=new AnimationCurve(keys);SmoothCurve(curve,loop);AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+axes[axis]),curve);
            }
        }
        string skinPath=AnimationUtility.CalculateTransformPath(skin.transform,actor.transform);
        var gripNotes=new List<string>();
        foreach(var shape in new[]{"Grip_R","Grip_L"})
        {
            bool right=shape=="Grip_R";float amount=right?grip:100;
            if(name.StartsWith("Sword")||name.StartsWith("Staff")) amount=right||name=="Sword_Aerial_Plunge"?75:100;
            var curve=AnimationCurve.Constant(0,duration,amount);
            if(right && (name=="Item_Throw_Overhand"||name=="Interact_Reach"||name=="Pickup_Low"))
            {
                var keys=new Keyframe[frames.Length];
                for(int i=0;i<keys.Length;i++)
                {float t=i/(float)FPS,u=t/duration;float open=name=="Item_Throw_Overhand"?Pulse(u,.37f,.5f,.72f):name=="Interact_Reach"?Pulse(u,.1f,.5f,.9f):Pulse(u,.25f,.48f,.63f);
                    float closed=name=="Item_Throw_Overhand"?Mathf.Lerp(75,100,Smooth((u-.50f)/.22f)):100;
                    keys[i]=new Keyframe(t,Mathf.Lerp(closed,15,open));}
                curve=new AnimationCurve(keys);SmoothCurve(curve);
            }
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(skinPath,typeof(SkinnedMeshRenderer),"blendShape."+shape),curve);
            gripNotes.Add(shape+"="+curve.keys.Min(k=>k.value).ToString("F1")+".."+curve.keys.Max(k=>k.value).ToString("F1"));
        }
        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.loopBlend=false;settings.startTime=0;settings.stopTime=duration;AnimationUtility.SetAnimationClipSettings(clip,settings);
        MotionPolishBuilder.SaveAsset(clip,Root+"/Clips/"+name+".anim");
        notes.Add(name+" | "+duration.ToString("F3")+"s | "+frames.Length+" keys/bone | original hierarchy transform clip | loop="+loop+" | "+string.Join(", ",gripNotes));
    }
    static void SmoothCurve(AnimationCurve c,bool loop=false)
    {
        for(int i=0;i<c.length;i++)c.SmoothTangents(i,0);
        if(loop&&c.length>3)
        {
            var keys=c.keys;float tangent=(keys[1].value-keys[keys.Length-2].value)*FPS*.5f;
            keys[0].inTangent=keys[0].outTangent=tangent;keys[keys.Length-1].inTangent=keys[keys.Length-1].outTangent=tangent;c.keys=keys;
        }
    }
}
#endif
