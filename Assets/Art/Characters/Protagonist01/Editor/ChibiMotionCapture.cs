#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class ChibiMotionSet
{
    [Serializable] public sealed class CaptureInfo
    {
        public string name,framesDirectory,intent,prop;public bool auxiliary,loop;
        public float durationSeconds,captureSeconds;public int frameCount,captureRate,width,height;
        public Vector3 cameraPosition,cameraTarget;public float orthographicSize;public FrameRow[] frames;
    }
    [Serializable] sealed class CaptureBundle
    {public string method,prefab;public bool gameplayConnected,vfxChanged;public CaptureInfo[] motions,transitions,details;}
    public static void CaptureAll()
    {
        var main=Definitions();var all=main.Concat(AuxiliaryDefinitions(main)).ToArray();
        var captures=new List<CaptureInfo>();
        foreach(var m in all)captures.Add(CaptureMotion(m,!main.Contains(m)));
        var transitions=CaptureTransitions(main);
        var details=main.Where(m=>m.prop=="Staff"||m.prop=="Sword").Select(m=>CaptureMotion(m,false,true)).ToArray();
        File.WriteAllText(Report+"/review-report.json",JsonUtility.ToJson(new CaptureBundle{prefab=PrefabPath,
            method="Actual saved/reloaded Generic clips sampled in Unity, CPU-skinned and rendered at30fps in one fixed2.5D side camera. Transitions are offline pose blends, not gameplay logic.",
            gameplayConnected=false,vfxChanged=false,motions=captures.ToArray(),transitions=transitions,details=details},true));
        Debug.Log("CHIBI_V007_CAPTURE_SUCCESS "+captures.Count+" clips + "+transitions.Length+" transitions");
    }
    public static void ReviewTransitionsOnly()
    {
        try
        {
            var bundle=JsonUtility.FromJson<CaptureBundle>(File.ReadAllText(Report+"/review-report.json"));
            bundle.transitions=CaptureTransitions(Definitions());
            File.WriteAllText(Report+"/review-report.json",JsonUtility.ToJson(bundle,true));
            Debug.Log("CHIBI_V007_TRANSITION_REVIEW_SUCCESS");
        }
        catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    public static void ReviewWeaponsOnly()
    {
        try
        {
            ChibiCylinderGripBuilder.Build();Build();
            var bundle=JsonUtility.FromJson<CaptureBundle>(File.ReadAllText(Report+"/review-report.json"));
            var weapons=Definitions().Where(m=>m.prop=="Staff"||m.prop=="Sword").ToArray();
            foreach(var m in weapons)
            {
                int index=Array.FindIndex(bundle.motions,c=>c.name==m.name);
                if(index<0)throw new Exception("Missing existing weapon review "+m.name);
                bundle.motions[index]=CaptureMotion(m,false);
            }
            bundle.details=weapons.Select(m=>CaptureMotion(m,false,true)).ToArray();
            File.WriteAllText(Report+"/review-report.json",JsonUtility.ToJson(bundle,true));
            ExportStaffGripForensics();
            Debug.Log("CHIBI_V007_WEAPON_REVIEW_SUCCESS");
        }
        catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    static CaptureInfo CaptureMotion(Motion m,bool auxiliary,bool gripDetail=false)
    {
        using(var stage=new Stage())
        {
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));SceneManager.MoveGameObjectToScene(actor,stage.scene);
            actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();var bones=actor.GetComponentsInChildren<Transform>().Where(t=>t!=actor.transform&&!t.name.Contains("Socket")).ToArray();
            using(var rig=new Rig(actor,skins,bones))
            {
                rig.MakeNeutral();var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+m.name+".anim");
                if(!clip)throw new Exception("Missing saved clip "+m.name);
                actor.transform.rotation=Quaternion.Euler(0,90,0);SetFixedView(stage);
                using(var prop=new ReviewProp(stage,rig,m.prop))
                {
                    Vector3 releasePosition=Vector3.zero;Quaternion releaseRotation=Quaternion.identity;
                    if(m.releaseTime>=0){clip.SampleAnimation(actor,m.releaseTime);prop.UpdateAttached();releasePosition=prop.Position;releaseRotation=prop.Rotation;}
                    var cpu=skins.Select(s=>new CpuSkin(s)).ToArray();
                    try
                    {
                        int cycles=m.loop?2:1;int count=Mathf.RoundToInt(m.duration*cycles*RenderRate)+(m.loop||auxiliary?0:8);
                        string name=gripDetail?(m.prop=="Staff"?"Staff_Grip_Side_25D":"Sword_Grip_Side_25D"):m.name;
                        string directory=Report+(gripDetail?"/GripDetails/":"/Frames/")+name;Directory.CreateDirectory(directory);var rows=new List<FrameRow>();
                        for(int i=0;i<count;i++)
                        {
                            float t=i/(float)RenderRate,local=m.loop?Mathf.Repeat(t,m.duration):Mathf.Min(t,m.duration);
                            clip.SampleAnimation(actor,local);
                            if(m.releaseTime>=0&&local>=m.releaseTime)prop.Flight(releasePosition,releaseRotation,local-m.releaseTime,actor.transform.forward);
                            else prop.UpdateAttached();
                            if(gripDetail)stage.SetView(prop.Position,new Vector3(1.05f,2.0f,-10),.19f);
                            foreach(var snapshot in cpu)snapshot.Bake();stage.Save(directory+"/"+i.ToString("D4")+".png");
                            var row=rig.Frame(t);prop.Measure(row);rows.Add(row);
                        }
                        clip.SampleAnimation(actor,m.duration);prop.UpdateAttached();foreach(var c in cpu)c.Bake();
                        var result=Info(name,directory,count,stage,rows);result.auxiliary=auxiliary;result.loop=m.loop;result.durationSeconds=m.duration;result.intent=gripDetail?"Actual saved clip, hand and handle close-up at the same2.5D camera angle; camera follows the hilt only.":m.intent;result.prop=m.prop;
                        if(gripDetail)result.cameraTarget=prop.Position;
                        File.WriteAllText(directory+"/capture.json",JsonUtility.ToJson(result,true));return result;
                    }
                    finally{foreach(var snapshot in cpu)snapshot.Dispose();}
                }
            }
        }
    }
    static void SetFixedView(Stage stage)=>stage.SetView(new Vector3(0,.78f,0),new Vector3(1.05f,2.0f,-10),1.05f);
    static CaptureInfo Info(string name,string dir,int count,Stage stage,List<FrameRow> rows)
    {return new CaptureInfo{name=name,framesDirectory=Path.GetFullPath(dir),frameCount=count,captureRate=RenderRate,width=ImageSize,height=ImageSize,captureSeconds=count/(float)RenderRate,
        cameraPosition=stage.camera.transform.position,cameraTarget=new Vector3(0,.78f,0),orthographicSize=stage.camera.orthographicSize,frames=rows.ToArray()};}

    sealed class ReviewProp:IDisposable
    {
        readonly GameObject obj;readonly Transform hand,tip;readonly float scale;readonly Vector3 offset;readonly bool potion;
        public Vector3 Position=>obj?obj.transform.position:Vector3.zero;public Quaternion Rotation=>obj?obj.transform.rotation:Quaternion.identity;
        public ReviewProp(Stage stage,Rig rig,string kind)
        {
            hand=rig.rh;potion=kind=="Potion"||kind=="ThrowItem";
            string file=kind=="Sword"?"Assets/Art/Stage01Kit/Prefabs/Weapons/Sword.prefab":kind=="Staff"?"Assets/Art/Stage01Kit/Prefabs/Weapons/Staff.prefab":potion?"Assets/Art/Stage01Kit/Prefabs/Props/Potion_Health.prefab":"";
            if(file=="")return;scale=potion?.85f:.70f;offset=potion?new Vector3(.040f,.025f,.045f):ChibiCylinderGripBuilder.GripCenterMetres;
            obj=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(file));SceneManager.MoveGameObjectToScene(obj,stage.scene);
            obj.name="Review_"+kind;
            foreach(var b in obj.GetComponentsInChildren<MonoBehaviour>())Object.DestroyImmediate(b);
            foreach(var c in obj.GetComponentsInChildren<Collider>())c.enabled=false;
            foreach(var rb in obj.GetComponentsInChildren<Rigidbody>()){rb.isKinematic=true;rb.useGravity=false;}
            tip=obj.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="EffectTip");
            if(potion){tip=new GameObject("Review_Nozzle").transform;tip.SetParent(obj.transform,false);tip.localPosition=new Vector3(0,.237f,0);}
            UpdateAttached();
        }
        public void UpdateAttached()
        {
            if(!obj)return;obj.transform.SetParent(hand,false);var s=hand.lossyScale;
            obj.transform.localPosition=new Vector3(offset.x/s.x,offset.y/s.y,offset.z/s.z);
            obj.transform.localRotation=potion?Quaternion.Euler(0,0,90):Quaternion.Euler(ChibiCylinderGripBuilder.MountRotationEuler);obj.transform.localScale=new Vector3(scale/s.x,scale/s.y,scale/s.z);
        }
        public void Flight(Vector3 p,Quaternion q,float t,Vector3 direction)
        {if(!obj)return;obj.transform.SetParent(null,true);obj.transform.position=p+direction*(1.8f*t)+Vector3.up*(1.1f*t-3.8f*t*t);obj.transform.rotation=q*Quaternion.AngleAxis(-180*t,Vector3.forward);}
        public void Measure(FrameRow row)
        {
            if(!tip)return;
            row.propHilt=obj.transform.position;row.propAxis=obj.transform.up;row.propNormal=obj.transform.forward;
            if(potion)row.potionNozzle=tip.position;else row.swordTip=tip.position;
        }
        public void Dispose(){if(obj)Object.DestroyImmediate(obj);}
    }

    static Pose ReadPose(GameObject actor,Rig rig,string name,float time)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+name+".anim");
        clip.SampleAnimation(actor,Mathf.Clamp(time,0,clip.length));return new Pose(rig.transforms){gripR=rig.skins.Max(s=>Grip(s,"Grip_R")),gripL=rig.skins.Max(s=>Grip(s,"Grip_L"))};
    }
    static void ApplyPose(Rig rig,Pose pose){pose.Apply(rig.transforms);rig.Grip(pose.gripR,pose.gripL);}
    static Pose Mix(Pose a,Pose b,float u)
    {
        var result=new Pose{p=new Vector3[a.p.Length],q=new Quaternion[a.q.Length],s=new Vector3[a.s.Length],gripR=Mathf.Lerp(a.gripR,b.gripR,u),gripL=Mathf.Lerp(a.gripL,b.gripL,u)};
        for(int i=0;i<a.p.Length;i++){result.p[i]=Vector3.Lerp(a.p[i],b.p[i],u);result.q[i]=Quaternion.Slerp(a.q[i],b.q[i],u);result.s[i]=Vector3.Lerp(a.s[i],b.s[i],u);}return result;
    }
    static CaptureInfo[] CaptureTransitions(Motion[] main)
    {
        CaptureInfo Sequence(string name,string intent,string propKind,float duration,Action<GameObject,Rig,float> evaluate)
        {
            using(var stage=new Stage())
            {
                var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));SceneManager.MoveGameObjectToScene(actor,stage.scene);
                var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();var bones=actor.GetComponentsInChildren<Transform>().Where(t=>t!=actor.transform&&!t.name.Contains("Socket")).ToArray();
                using(var rig=new Rig(actor,skins,bones))
                {
                    rig.MakeNeutral();actor.transform.rotation=Quaternion.Euler(0,90,0);SetFixedView(stage);
                    var cpu=skins.Select(s=>new CpuSkin(s)).ToArray();
                    using(var prop=new ReviewProp(stage,rig,propKind))
                    try
                    {
                        string dir=Report+"/Transitions/"+name;Directory.CreateDirectory(dir);int count=Mathf.RoundToInt(duration*RenderRate);var rows=new List<FrameRow>();
                        for(int i=0;i<count;i++){float t=i/(float)RenderRate;evaluate(actor,rig,t);prop.UpdateAttached();foreach(var c in cpu)c.Bake();stage.Save(dir+"/"+i.ToString("D4")+".png");var row=rig.Frame(t);prop.Measure(row);rows.Add(row);}
                        var info=Info(name,dir,count,stage,rows);info.durationSeconds=count/(float)RenderRate;info.intent=intent;info.prop=propKind;return info;
                    }
                    finally{foreach(var c in cpu)c.Dispose();}
                }
            }
        }
        float D(string name)=>main.First(m=>m.name==name).duration;
        Pose At(GameObject actor,Rig rig,string name,float t,bool loop=false)=>ReadPose(actor,rig,name,loop?Mathf.Repeat(t,D(name)):t);
        void Ground(GameObject actor,Rig r,float t)
        {
            string[] names={"Idle_Fists","Walk_Fists","Run_Fists","Dash_Forward","Run_Fists","Idle_Fists"};
            float[] lengths={.4f,D("Walk_Fists")*2,D("Run_Fists")*2,D("Dash_Forward"),D("Run_Fists"),.4f};
            float start=0;int index=0;for(;index<lengths.Length-1&&t>=start+lengths[index];index++)start+=lengths[index];
            float local=t-start;bool loop=names[index].Contains("Fists");var pose=At(actor,r,names[index],local,loop);
            if(index>0&&local<.10f){string old=names[index-1];var from=At(actor,r,old,lengths[index-1]+local,old.Contains("Fists"));pose=Mix(from,pose,S01(local/.10f));}
            ApplyPose(r,pose);r.CorrectFeet();
        }
        void AirPunch(GameObject actor,Rig r,float t)
        {
            float jump=D("Jump_Full"),dash=D("Dash_Forward");
            if(t<jump)
            {
                var basePose=At(actor,r,"Jump_Full",t);float local=t-.28f;
                if(local>=0&&local<D("Punch_Right_Jab"))
                {
                    var punch=At(actor,r,"Punch_Right_Jab",local);float w=S01(local/.055f)*(1-S01((local-D("Punch_Right_Jab")+.08f)/.08f));
                    for(int i=0;i<r.transforms.Length;i++)
                    {
                        string n=r.transforms[i].name;
                        if(n.StartsWith("Spine")||n.Contains("Shoulder")||n.Contains("Arm")||n.Contains("Hand"))
                            basePose.q[i]=basePose.q[i]*Quaternion.Slerp(Quaternion.identity,Quaternion.Inverse(r.neutral.q[i])*punch.q[i],w);
                    }
                }
                ApplyPose(r,basePose);
            }
            else if(t<jump+dash)
            {float local=t-jump;var p=At(actor,r,"Dash_Forward",local);if(local<.07f)p=Mix(At(actor,r,"Jump_Full",jump),p,S01(local/.07f));ApplyPose(r,p);}
            else {float local=t-jump-dash;var p=At(actor,r,"Idle_Fists",local,true);if(local<.08f)p=Mix(At(actor,r,"Dash_Forward",dash),p,S01(local/.08f));ApplyPose(r,p);}
        }
        return new[]{Sequence("Move_Dash_Recover","Idle→Walk→Run→Dash→Run→Idle. Offline saved-clip blend with100ms boundary windows and actual-skin sole correction during preview.","",.8f+D("Walk_Fists")*2+D("Run_Fists")*3+D("Dash_Forward"),Ground),
            Sequence("Jump_Attack_Dash","Jump base with a saved punch upper-body overlay during flight, landing absorption, then dash and ready. No gameplay implementation.","",D("Jump_Full")+D("Dash_Forward")+.4f,AirPunch)};
    }
}
#endif
