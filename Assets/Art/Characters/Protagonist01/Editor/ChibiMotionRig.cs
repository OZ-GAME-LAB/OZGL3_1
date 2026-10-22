#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class ChibiMotionSet
{
    public sealed partial class Rig
    {
        SourceBank bank;
        public Vector3 SwordTipLocal => ChibiCylinderGripBuilder.GripCenterMetres+Quaternion.Euler(ChibiCylinderGripBuilder.MountRotationEuler)*Vector3.up*(.817f*ChibiCylinderGripBuilder.PropScale);
        public Vector3 PotionNozzleLocal => new Vector3(.040f-.237f*.85f,.025f,.045f);
        public Vector3 Mouth => head.position+head.rotation*new Vector3(0,.060f,.175f);
        public Vector3 HipNeutral {get;private set;}
        public void Reset(){neutral.Apply(transforms);Grip();}
        public void Stance(float drop=0,float lean=0,float yaw=0,float forward=0)
        {Body(0,_=>drop,_=>lean,_=>yaw,_=>forward);}
        public void Body(float t,Func<float,float> drop,Func<float,float> lean,Func<float,float> yaw,Func<float,float> forward)
        {
            Reset(); hips.position+=new Vector3(0,-drop(t),forward(t));
            hips.rotation=Quaternion.AngleAxis(yaw(t)*.45f,Vector3.up)*Quaternion.AngleAxis(lean(t)*.20f,Vector3.right)*hips.rotation;
            for(int i=0;i<3;i++)
            {
                float lag=.012f*(i+1),x=Mathf.Max(0,t-lag);
                float weight=new[]{.28f,.28f,.24f}[i];
                spine[i].rotation=Quaternion.AngleAxis(yaw(x)*new[]{.20f,.20f,.15f}[i],Vector3.up)*Quaternion.AngleAxis(lean(x)*weight,Vector3.right)*spine[i].rotation;
            }
            Gaze();Plant(leftFootTarget,rightFootTarget);
        }
        public void Plant(Vector3 left,Vector3 right,Quaternion? lr=null,Quaternion? rr=null)
        {
            float Fit(Transform a,Transform b,Transform c,Vector3 goal)
            {
                float reach=(Vector3.Distance(a.position,b.position)+Vector3.Distance(b.position,c.position))*.995f;
                Vector3 flat=a.position-goal;flat.y=0;
                float vertical=Mathf.Sqrt(Mathf.Max(.000001f,reach*reach-flat.sqrMagnitude));
                return Mathf.Max(0,a.position.y-goal.y-vertical);
            }
            hips.position-=Vector3.up*Mathf.Max(Fit(lul,lll,lf,left),Fit(rul,rll,rf,right));
            Legs(left,right,lr??lfRotation,rr??rfRotation);
        }
        public void Hand(bool right,Vector3 target)
        {
            var a=right?rua:lua;var b=right?rla:lla;var c=right?rh:lh;
            float length=right?rightArmLength:leftArmLength,sign=right?rightSign:leftSign;
            IK(a,b,c,target,a.position+Vector3.down*length+Vector3.right*sign*.12f-Vector3.forward*.07f);
            Fist(right);
        }
        public void Fist(bool right)
        {
            var hand=right?rh:lh;var elbow=right?rla:lla;
            var sourceDirection=right?rForearmNeutral:lForearmNeutral;
            hand.rotation=Quaternion.FromToRotation(sourceDirection,(hand.position-elbow.position).normalized)*(right?rhRotation:lhRotation);
        }
        // Run-only correction. Preserve the source leg/body timing and forward arm
        // swing, but keep the short chibi arms on their own side of the coat.
        // The resulting saved clip is checked with the separate full-mesh probe.
        public void ClearRunArms()
        {
            var right=rh.position;var left=lh.position;
            float SoftFloor(float value,float floor)
                =>.5f*(value+floor+Mathf.Sqrt((value-floor)*(value-floor)+.000025f));
            right.x=hips.position.x+rightSign*.260f;left.x=hips.position.x+leftSign*.268f;
            right.y=SoftFloor(right.y,rua.position.y-.145f);
            left.y=SoftFloor(left.y,lua.position.y-.145f);
            void Solve(bool isRight,Vector3 target)
            {
                var a=isRight?rua:lua;var b=isRight?rla:lla;var c=isRight?rh:lh;
                float sign=isRight?rightSign:leftSign;
                IK(a,b,c,target,a.position+new Vector3(sign*.18f,-.075f,-.035f));Fist(isRight);
            }
            Solve(true,right);Solve(false,left);
        }

        public void HandFrame(bool right,Vector3 bladeDirection,Vector3 planeNormal)
        {
            var x=(right?-bladeDirection:bladeDirection).normalized;
            var z=Vector3.ProjectOnPlane(planeNormal,x).normalized;
            if(z.sqrMagnitude<.1f)z=Vector3.ProjectOnPlane(Vector3.forward,x).normalized;
            if(z.sqrMagnitude<.1f)z=Vector3.ProjectOnPlane(Vector3.up,x).normalized;
            (right?rh:lh).rotation=Quaternion.LookRotation(z,Vector3.Cross(z,x).normalized);
        }
        public Vector3 WeaponReadyHilt(bool right)
        {return new Vector3((right?rightSign:leftSign)*.14f,HipNeutral.y+.085f,HipNeutral.z+.18f);}
        public void WeaponHandFrame(bool right,Vector3 modelAxisY,Vector3 modelNormalZ)
        {
            if(!right)throw new InvalidOperationException("Only the right cylindrical grip has been calibrated.");
            var axis=modelAxisY.normalized;var normal=Vector3.ProjectOnPlane(modelNormalZ,axis).normalized;
            if(normal.sqrMagnitude<.1f)throw new InvalidOperationException("Weapon axis and edge plane must be independent.");
            // Use the calibrated diagonal grasp: handle +Y traverses the palm,
            // while the hand's longitudinal axis can follow the forearm.
            rh.rotation=Quaternion.LookRotation(normal,axis)*Quaternion.Inverse(Quaternion.Euler(ChibiCylinderGripBuilder.MountRotationEuler));
        }
        public void WeaponHand(bool right,Vector3 hilt,Vector3 modelAxisY,Vector3 modelNormalZ)
        {
            WeaponHandFrame(right,modelAxisY,modelNormalZ);var q=rh.rotation;
            var wrist=hilt-q*ChibiCylinderGripBuilder.GripCenterMetres;
            Hand(right,wrist);rh.rotation=q;
        }
        public void Gaze(float pitch=0,float yaw=0)
        {head.rotation=Quaternion.AngleAxis(yaw,Vector3.up)*Quaternion.AngleAxis(pitch,Vector3.right)*headRotation;}
        public void Grip(float right=100,float left=100)
        {foreach(var skin in skins){SetGrip(skin,"Grip_R",right);SetGrip(skin,"Grip_L",left);}}
        public void CorrectFeet()
        {
            for(int i=0;i<3;i++)
            {
                soles.Bake();float l=soles.Minimum(true),r=soles.Minimum(false);
                if(l>=.0025f&&r>=.0025f)break;
                var left=lf.position+Vector3.up*Mathf.Max(0,.003f-l);var right=rf.position+Vector3.up*Mathf.Max(0,.003f-r);
                var lq=lf.rotation;var rq=rf.rotation;Legs(left,right,lq,rq);
            }
        }
        public void KeepSwordTipAboveGround(float height=.006f)
        {
            for(int i=0;i<3;i++)
            {
                var tip=rh.position+rh.rotation*SwordTipLocal;
                if(tip.y>=height)break;
                var q=rh.rotation;Hand(true,rh.position+Vector3.up*(height-tip.y));rh.rotation=q;
            }
        }
        public void SourceLocomotion(string name,float phase,float amplitude=.75f)
        {
            if(bank==null)bank=new SourceBank(skins[0].gameObject.scene);
            Reset();bank.Sample(name,phase);
            for(int i=0;i<transforms.Length;i++)
            {
                if(!bank.bones.TryGetValue(transforms[i].name,out var old)||!bank.neutralQ.TryGetValue(transforms[i].name,out var bq))continue;
                var delta=Quaternion.Inverse(bq)*old.localRotation;
                transforms[i].localRotation=neutral.q[i]*Quaternion.Slerp(Quaternion.identity,delta,amplitude);
            }
            float ratio=(Vector3.Distance(lul.position,lll.position)+Vector3.Distance(lll.position,lf.position))/bank.legLength;
            var p=hips.position;p.y=HipNeutral.y+(bank.hip.position.y-bank.neutralHip.y)*ratio*.75f;p.x=HipNeutral.x;p.z=HipNeutral.z;hips.position=p;
            Vector3 Goal(Vector3 source,Vector3 sourceNeutral,Vector3 target)
            {var delta=source-sourceNeutral;return target+new Vector3(delta.x*ratio*.35f,delta.y*ratio,delta.z*ratio*amplitude);}
            var lg=Goal(bank.lf.position,bank.neutralLF,leftFootTarget);var rg=Goal(bank.rf.position,bank.neutralRF,rightFootTarget);
            Quaternion lq=lfRotation*Quaternion.Inverse(bank.neutralLQ)*bank.lf.rotation,rq=rfRotation*Quaternion.Inverse(bank.neutralRQ)*bank.rf.rotation;
            Plant(lg,rg,lq,rq);Gaze();Fist(true);Fist(false);Grip();CorrectFeet();
        }
        public void InitializeExtras(){HipNeutral=hips.position;}
        public void DisposeExtras(){bank?.Dispose();bank=null;}
    }
    sealed class SourceBank:IDisposable
    {
        readonly GameObject actor;readonly Dictionary<string,AnimationClip> clips=new Dictionary<string,AnimationClip>();
        public readonly Dictionary<string,Transform> bones;public readonly Dictionary<string,Quaternion> neutralQ;
        public readonly Transform hip,lf,rf;public readonly Vector3 neutralHip,neutralLF,neutralRF;public readonly Quaternion neutralLQ,neutralRQ;
        public readonly float legLength;
        public SourceBank(Scene scene)
        {
            actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Protagonist01/ChibiMotion_v007/SourceGait/Skeleton.prefab"));
            SceneManager.MoveGameObjectToScene(actor,scene);actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            foreach(var a in actor.GetComponentsInChildren<Animator>())a.enabled=false;
            foreach(var r in actor.GetComponentsInChildren<Renderer>())r.enabled=false;
            bones=actor.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            Load("Idle_Fists").SampleAnimation(actor,0);
            neutralQ=bones.ToDictionary(k=>k.Key,v=>v.Value.localRotation);
            hip=bones["Hips"];lf=bones["LeftFoot"];rf=bones["RightFoot"];
            neutralHip=hip.position;neutralLF=lf.position;neutralRF=rf.position;neutralLQ=lf.rotation;neutralRQ=rf.rotation;
            legLength=Vector3.Distance(bones["LeftUpLeg"].position,bones["LeftLeg"].position)+Vector3.Distance(bones["LeftLeg"].position,lf.position);
        }
        AnimationClip Load(string name)
        {
            if(!clips.TryGetValue(name,out var clip)){clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Characters/Protagonist01/ChibiMotion_v007/SourceGait/Clips/"+name+".anim");clips[name]=clip;}
            if(!clip)throw new Exception("Missing preserved source clip "+name);return clip;
        }
        public void Sample(string name,float phase)
        {var c=Load(name);c.SampleAnimation(actor,Mathf.Repeat(phase,1)*c.length);}
        public void Dispose(){Object.DestroyImmediate(actor);}
    }
}
#endif
