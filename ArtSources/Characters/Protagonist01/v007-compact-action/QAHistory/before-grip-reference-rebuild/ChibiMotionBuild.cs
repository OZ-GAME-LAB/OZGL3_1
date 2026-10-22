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
    [Serializable] public sealed class MotionInfo
    {
        public string name,clip,prop,intent; public float duration,authoredDuration,releaseTime;public bool loop,auxiliary;
        public float[] phases;public int keySamples;public FrameRow start,end;
    }
    [Serializable] sealed class BuildInfo
    {public string prefab,method;public int mainMotions,auxiliaryClips,bakeRate;public bool gameplayChanged,vfxChanged;public MotionInfo[] motions;}

    [MenuItem("Project M/Chibi Compact v007/Build and Review All Motions")]
    public static void BuildAndReview()
    {
        try{Build();CaptureAll();}catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    public static void BuildRequestedTweaks()
    {
        try{ChibiStaffOrbBuilder.Build();Build();CaptureAll();}
        catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
    }
    public static void Build()
    {
        Directory.CreateDirectory(Root+"/Clips");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Report);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string removed in new[]{"Sword_Aerial_Plunge","Sword_Plunge_Fall_Loop","Sword_Plunge_Land"})
            if(AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/Clips/"+removed+".anim"))AssetDatabase.DeleteAsset(Root+"/Clips/"+removed+".anim");
        using(var stage=new Stage())
        {
            GameObject actor=CreateActor(stage);var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            var transforms=actor.GetComponentsInChildren<Transform>().Where(t=>t!=actor.transform).ToArray();
            using(var rig=new Rig(actor,skins,transforms))
            {
                rig.MakeNeutral();
                foreach(string side in new[]{"RightHand","LeftHand"})
                {var h=transforms.First(t=>t.name==side);var socket=new GameObject(side=="RightHand"?"WeaponSocket":"WeaponSocket_Left").transform;socket.SetParent(h,false);}
                bool saved;PrefabUtility.SaveAsPrefabAsset(actor,PrefabPath,out saved);if(!saved)throw new Exception("Cannot save v007 prefab");
                var all=new List<MotionInfo>();var main=Definitions();
                foreach(var m in main)all.Add(BakeMotion(actor,rig,m,false));
                foreach(var m in AuxiliaryDefinitions(main))all.Add(BakeMotion(actor,rig,m,true));
                AssetDatabase.SaveAssets();
                File.WriteAllText(Report+"/build-report.json",JsonUtility.ToJson(new BuildInfo{prefab=PrefabPath,
                    method="New chibi neutral; compact per-action pose editing; preserved AI gait structure adapted to new limb lengths; 60Hz Generic curves. Durations align to the 30fps review grid with a uniform author-time map.",
                    mainMotions=main.Length,auxiliaryClips=all.Count-main.Length,bakeRate=BakeRate,gameplayChanged=false,vfxChanged=false,motions=all.ToArray()},true));
            }
        }
        Debug.Log("CHIBI_V007_MOTIONS_BUILT");
    }
    static GameObject CreateActor(Stage stage)
    {
        var actor=stage.New("Protagonist_Chibi_MotionReview");
        var visual=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RigPath));
        SceneManager.MoveGameObjectToScene(visual,stage.scene);visual.name="Visual";visual.transform.SetParent(actor.transform,false);
        foreach(var a in visual.GetComponentsInChildren<Animator>())a.enabled=false;
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);var fists=AssetDatabase.LoadAssetAtPath<Mesh>(FistsPath);
        if(!fists||!material)throw new Exception("Missing validated v006 model/hand assets");
        var skins=visual.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach(var skin in skins)
        {
            if(skin.sharedMesh.vertexCount!=fists.vertexCount)throw new Exception("Unexpected v006 skin vertex count");
            skin.sharedMesh=fists;skin.sharedMaterials=Enumerable.Repeat(material,skin.sharedMaterials.Length).ToArray();
            skin.updateWhenOffscreen=true;skin.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;skin.receiveShadows=false;
            SetGrip(skin,"Grip_R",100);SetGrip(skin,"Grip_L",100);
        }
        Bounds b=SkinBounds(skins);visual.transform.localScale*=1.2f/b.size.y;b=SkinBounds(skins);
        visual.transform.localPosition+=new Vector3(-b.center.x,-b.min.y,-b.center.z);
        var animator=actor.AddComponent<Animator>();animator.avatar=null;animator.runtimeAnimatorController=null;animator.enabled=false;
        return actor;
    }
    static MotionInfo BakeMotion(GameObject actor,Rig rig,Motion m,bool auxiliary)
    {
        int count=Mathf.RoundToInt(m.duration*BakeRate)+1;var poses=new Pose[count];FrameRow start=null,end=null;
        for(int i=0;i<count;i++)
        {
            float t=i*m.duration/(count-1);m.evaluate(rig,t);
            if(!auxiliary)rig.CorrectFeet();
            poses[i]=new Pose(rig.transforms){gripR=rig.skins.Max(s=>Grip(s,"Grip_R")),gripL=rig.skins.Max(s=>Grip(s,"Grip_L"))};
            if(i==0)start=rig.Frame(t);if(i==count-1)end=rig.Frame(t);
        }
        for(int j=0;j<rig.transforms.Length;j++)for(int i=1;i<count;i++)
            if(Quaternion.Dot(poses[i-1].q[j],poses[i].q[j])<0){var q=poses[i].q[j];poses[i].q[j]=new Quaternion(-q.x,-q.y,-q.z,-q.w);}
        var clip=new AnimationClip{name=m.name,frameRate=BakeRate,legacy=false};
        for(int j=0;j<rig.transforms.Length;j++)
        {
            int b=j;string path=AnimationUtility.CalculateTransformPath(rig.transforms[j],actor.transform);
            for(int a=0;a<3;a++)
            {int c=a;string axis="."+"xyz"[a];AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalPosition"+axis),Keys(poses.Select(p=>p.p[b][c]).ToArray(),m.duration));
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalScale"+axis),Keys(poses.Select(p=>p.s[b][c]).ToArray(),m.duration));}
            for(int a=0;a<4;a++){int c=a;AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+"xyzw"[a]),Keys(poses.Select(p=>p.q[b][c]).ToArray(),m.duration));}
        }
        foreach(var skin in rig.skins)foreach(bool right in new[]{true,false})
        {
            string name=right?"Grip_R":"Grip_L",path=AnimationUtility.CalculateTransformPath(skin.transform,actor.transform);
            AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(SkinnedMeshRenderer),"blendShape."+name),Keys(poses.Select(p=>right?p.gripR:p.gripL).ToArray(),m.duration));
        }
        clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=m.loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
        string output=Root+"/Clips/"+m.name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(output);
        if(!existing)AssetDatabase.CreateAsset(clip,output);else{EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(clip);}
        return new MotionInfo{name=m.name,clip=output,prop=m.prop,intent=m.intent,duration=m.duration,authoredDuration=m.sourceDuration,releaseTime=m.releaseTime,loop=m.loop,phases=m.phases,auxiliary=auxiliary,keySamples=count,start=start,end=end};
    }
    static AnimationCurve Keys(float[] values,float duration)
    {
        if(values.All(v=>Mathf.Abs(v-values[0])<1e-7f))return AnimationCurve.Constant(0,duration,values[0]);
        float hz=(values.Length-1)/duration;var keys=new Keyframe[values.Length];
        for(int i=0;i<values.Length;i++)
        {float tangent=i==0?(values[1]-values[0])*hz:i==values.Length-1?(values[i]-values[i-1])*hz:(values[i+1]-values[i-1])*hz*.5f;keys[i]=new Keyframe(i/hz,values[i],tangent,tangent);}
        return new AnimationCurve(keys);
    }
    static float AirHeight(Motion m,float t)
    {
        float a=t*m.sourceDuration/m.duration;
        if(m.name=="Jump_Full")return ChibiJumpHeight(a);
        return 0;
    }
    static Motion[] AuxiliaryDefinitions(Motion[] main)
    {
        var jump=main.First(m=>m.name=="Jump_Full");
        Motion Slice(string name,Motion source,float a,float b,float duration,bool loop)
        {
            return new Motion{name=name,prop=source.prop,duration=duration,sourceDuration=duration,loop=loop,
                intent="Physics-ready pose phase derived from "+source.name+". Baked aerial height removed; runtime movement remains external.",
                evaluate=(r,t)=>{float authored=Mathf.Lerp(a,b,Mathf.Clamp01(t/duration));float time=authored*source.duration/source.sourceDuration;
                    source.evaluate(r,time);r.hips.position-=Vector3.up*AirHeight(source,time);}};
        }
        return new[]{Slice("Jump_Takeoff",jump,0,.16f,5/30f,false),Slice("Jump_Rise_Loop",jump,.34f,.34f,.3f,true),
            Slice("Jump_Apex_Loop",jump,.48f,.48f,.2f,true),Slice("Jump_Fall_Loop",jump,.66f,.66f,.3f,true),
            Slice("Jump_Land",jump,.78f,1.2f,13/30f,false)};
    }
}
#endif
