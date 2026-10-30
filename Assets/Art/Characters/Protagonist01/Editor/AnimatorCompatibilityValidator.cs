#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

// Offline validation against the real player prefab. No saved controller,
// prefab, runtime script, avatar, mesh or clip is changed by this validator.
public static class AnimatorCompatibilityValidator
{
    const string Player="Assets/2.Prefab/Player/Player_Chibi.prefab";
    const string Controller="Assets/Anim/Player_Chibi.controller";
    const string Clips="Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Clips/";
    const string Report="ArtSources/Characters/Protagonist01/Final/Validation/ChibiCompatibility20261029/Additional/animator-compatibility.json";
    static readonly string[] Names={"Longsword_Attack_Chop","Longsword_Attack_Slice","Longsword_Attack_Stab","Death_Fall","Ladder_Climb_Loop"};
    [Serializable] public sealed class FrameEvidence
    {
        public float normalizedTime,time,maxWorldVertexDifference,maxBonePositionDifference,maxBoneAngleDifference;
        public float liveMeshDisplacementFromIdle,liveMeshDisplacementFromFirstSample;
        public int comparedVertices;
        public bool finite,passed;
    }
    [Serializable] public sealed class ClipEvidence
    {
        public string name,path;
        public string[] missingBindings,removedRuntimeComponents;
        public FrameEvidence[] frames;
        public int curveBindings;
        public float maximumMovementFromIdle,maximumTemporalMovement;
        public bool generic,noAvatar,noController,movesLiveAnimator,passed;
    }
    [Serializable] public sealed class Evidence
    {
        public string unity,player=Player,controller=Controller;
        public string method="Compare AnimationClip.SampleAnimation against actual manual AnimationClipPlayable evaluation on separate sanitized clones of Player_Chibi. Both use normalized root and Idle_Fists0. Live Animator has avatar=null and runtimeAnimatorController=null. Five phases per clip; all deformed mesh vertices compared.";
        public string playerSHA256Before,playerSHA256After,controllerSHA256Before,controllerSHA256After;
        public ClipEvidence[] clips;
        public bool savedControllerUnchanged,savedPlayerUnchanged,passed;
        public string limitation="Numerical compatibility validation is not a substitute for full-motion visual inspection or testing gameplay state timing. No saved AnimatorController is modified or evaluated.";
    }
    sealed class MeshFrame
    {
        public readonly Dictionary<string,Vector3[]> vertices=new Dictionary<string,Vector3[]>();
        public MeshFrame(GameObject actor)
        {
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=new Mesh();
                try
                {
                    skin.BakeMesh(mesh);
                    vertices.Add(AnimationUtility.CalculateTransformPath(skin.transform,actor.transform),mesh.vertices.Select(skin.transform.TransformPoint).ToArray());
                }
                finally{Object.DestroyImmediate(mesh);}
            }
            if(vertices.Count==0)throw new InvalidOperationException("Player clone has no skinned mesh.");
        }
    }
    sealed class RestPose
    {
        readonly Transform[] nodes; readonly Vector3[] p,s; readonly Quaternion[] q;
        public RestPose(GameObject actor)
        {
            nodes=actor.GetComponentsInChildren<Transform>(true);p=nodes.Select(x=>x.localPosition).ToArray();
            q=nodes.Select(x=>x.localRotation).ToArray();s=nodes.Select(x=>x.localScale).ToArray();
        }
        public void Restore(){for(int i=0;i<nodes.Length;i++){nodes[i].localPosition=p[i];nodes[i].localRotation=q[i];nodes[i].localScale=s[i];}}
    }
    sealed class Actor : IDisposable
    {
        public readonly GameObject holder,model;
        public readonly Animator animator;
        public readonly string[] removed;
        public readonly RestPose neutral;
        public Actor(AnimationClip idle)
        {
            holder=new GameObject("Offline_PlayerCompatibility_Holder");holder.hideFlags=HideFlags.HideAndDontSave;holder.SetActive(false);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Player);
            if(!prefab)throw new InvalidOperationException("Missing actual Player_Chibi prefab.");
            // Parenting under an inactive holder prevents Awake/OnEnable even if
            // a future prefab revision is saved active.
            model=Object.Instantiate(prefab,holder.transform,false);model.name=prefab.name;model.SetActive(false);
            removed=RemoveRuntimeBehaviours(model);
            foreach(var c in model.GetComponentsInChildren<Collider>(true))c.enabled=false;
            foreach(var r in model.GetComponentsInChildren<Rigidbody>(true)){r.isKinematic=true;r.useGravity=false;r.detectCollisions=false;}
            foreach(var b in model.GetComponentsInChildren<Behaviour>(true))b.enabled=false;
            foreach(var particles in model.GetComponentsInChildren<ParticleSystem>(true))particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            model.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);model.transform.localScale=Vector3.one;
            animator=model.GetComponent<Animator>()??model.AddComponent<Animator>();
            animator.runtimeAnimatorController=null;animator.avatar=null;animator.applyRootMotion=false;
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.fireEvents=false;
            model.SetActive(true);holder.SetActive(true);
            animator.enabled=false;idle.SampleAnimation(model,0);
            neutral=new RestPose(model);
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
        }
        public void Dispose(){if(holder)Object.DestroyImmediate(holder);}
    }
    static string[] RemoveRuntimeBehaviours(GameObject root)
    {
        var pending=root.GetComponentsInChildren<MonoBehaviour>(true).Where(x=>x).ToList();
        foreach(var item in pending)item.enabled=false;
        var removed=new List<string>();
        while(pending.Count>0)
        {
            var removable=pending.FirstOrDefault(candidate=>!pending.Any(other=>other!=candidate&&other.gameObject==candidate.gameObject&&RequiredTypes(other.GetType()).Any(type=>type.IsAssignableFrom(candidate.GetType()))));
            if(!removable)throw new InvalidOperationException("Cannot safely strip cyclic RequireComponent MonoBehaviour dependencies: "+string.Join(", ",pending.Select(x=>x.GetType().FullName)));
            removed.Add(removable.GetType().FullName);pending.Remove(removable);Object.DestroyImmediate(removable);
        }
        return removed.ToArray();
    }
    static IEnumerable<Type> RequiredTypes(Type owner)
    {
        foreach(RequireComponent requirement in owner.GetCustomAttributes(typeof(RequireComponent),true))
            foreach(var field in typeof(RequireComponent).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
                if(field.FieldType==typeof(Type)) {var required=field.GetValue(requirement) as Type;if(required!=null)yield return required;}
    }
    public static void Run()
    {
        var result=new Evidence{unity=Application.unityVersion,playerSHA256Before=Hash(Player),controllerSHA256Before=Hash(Controller)};
        var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+"Idle_Fists.anim");
        if(!idle)throw new InvalidOperationException("Missing Chibi idle normalization pose.");
        var rows=new List<ClipEvidence>();
        foreach(var name in Names)rows.Add(Validate(name,idle));
        result.clips=rows.ToArray();result.playerSHA256After=Hash(Player);result.controllerSHA256After=Hash(Controller);
        result.savedPlayerUnchanged=result.playerSHA256Before==result.playerSHA256After;
        result.savedControllerUnchanged=result.controllerSHA256Before==result.controllerSHA256After;
        result.passed=result.savedControllerUnchanged&&result.savedPlayerUnchanged&&rows.All(x=>x.passed);
        Directory.CreateDirectory(Path.GetDirectoryName(Report));File.WriteAllText(Report,JsonUtility.ToJson(result,true));
        if(!result.passed)throw new InvalidOperationException("Live Generic Animator validation failed; see "+Report);
        Debug.Log("CHIBI_LIVE_GENERIC_ANIMATOR_VALIDATION_PASSED: five clips, twenty-five phases, no Avatar.");
    }
    static ClipEvidence Validate(string name,AnimationClip idle)
    {
        string path=Clips+name+".anim";
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(!clip)throw new InvalidOperationException("Missing new clip "+path);
        using(var sampled=new Actor(idle))using(var live=new Actor(idle))
        {
            var bindings=AnimationUtility.GetCurveBindings(clip);
            var missing=bindings.Where(binding=>
            {
                var node=string.IsNullOrEmpty(binding.path)?live.model.transform:live.model.transform.Find(binding.path);
                return !node||!node.GetComponent(binding.type);
            }).Select(b=>b.path+" :: "+b.type.Name+" :: "+b.propertyName).ToArray();
            var row=new ClipEvidence{name=name,path=path,curveBindings=bindings.Length,missingBindings=missing,
                removedRuntimeComponents=live.removed,generic=!clip.humanMotion&&!bindings.Any(b=>b.type==typeof(Animator)),
                noAvatar=!live.animator.avatar,noController=!live.animator.runtimeAnimatorController};
            var baseline=new MeshFrame(live.model); MeshFrame first=null;
            var samples=new List<FrameEvidence>();
            var liveTransforms=live.model.GetComponentsInChildren<Transform>(true).ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,live.model.transform));
            var sampleTransforms=sampled.model.GetComponentsInChildren<Transform>(true).ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,sampled.model.transform));
            PlayableGraph graph=default(PlayableGraph);
            try
            {
                live.animator.enabled=true;live.animator.Rebind();live.neutral.Restore();
                graph=PlayableGraph.Create("Validate_Generic_"+name);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);
                var output=AnimationPlayableOutput.Create(graph,"ActualPlayerAnimator",live.animator);output.SetSourcePlayable(playable);graph.Play();
                foreach(float phase in new[]{0f,.25f,.5f,.75f,1f})
                {
                    float time=clip.length*phase;
                    sampled.neutral.Restore();clip.SampleAnimation(sampled.model,time);
                    playable.SetTime(time);graph.Evaluate(0);
                    var actualMesh=new MeshFrame(live.model);var expectedMesh=new MeshFrame(sampled.model);
                    var f=new FrameEvidence{normalizedTime=phase,time=time,finite=true};
                    f.maxWorldVertexDifference=MeshDifference(actualMesh,expectedMesh,out f.comparedVertices);
                    f.liveMeshDisplacementFromIdle=MeshDifference(actualMesh,baseline,out _);
                    if(first==null)first=actualMesh;
                    f.liveMeshDisplacementFromFirstSample=MeshDifference(actualMesh,first,out _);
                    foreach(var pair in liveTransforms)
                    {
                        var expected=sampleTransforms[pair.Key];
                        f.maxBonePositionDifference=Mathf.Max(f.maxBonePositionDifference,Vector3.Distance(pair.Value.position,expected.position));
                        f.maxBoneAngleDifference=Mathf.Max(f.maxBoneAngleDifference,Quaternion.Angle(pair.Value.rotation,expected.rotation));
                    }
                    f.finite=!float.IsNaN(f.maxWorldVertexDifference)&&!float.IsInfinity(f.maxWorldVertexDifference);
                    f.passed=f.finite&&f.comparedVertices>0&&f.maxWorldVertexDifference<=.002f&&f.maxBonePositionDifference<=.001f&&f.maxBoneAngleDifference<=.15f;
                    samples.Add(f);
                }
            }
            finally{if(graph.IsValid())graph.Destroy();}
            row.frames=samples.ToArray();row.maximumMovementFromIdle=samples.Max(x=>x.liveMeshDisplacementFromIdle);
            row.maximumTemporalMovement=samples.Max(x=>x.liveMeshDisplacementFromFirstSample);
            row.movesLiveAnimator=row.maximumTemporalMovement>.005f;
            row.passed=row.generic&&row.noAvatar&&row.noController&&row.missingBindings.Length==0&&row.movesLiveAnimator&&samples.All(x=>x.passed);
            return row;
        }
    }
    static float MeshDifference(MeshFrame actual,MeshFrame expected,out int count)
    {
        count=0;float max=0;
        if(actual.vertices.Count!=expected.vertices.Count)throw new InvalidOperationException("Mesh count changed during offline validation.");
        foreach(var item in actual.vertices)
        {
            var other=expected.vertices[item.Key];
            if(item.Value.Length!=other.Length)throw new InvalidOperationException("Vertex count changed for "+item.Key);
            for(int i=0;i<other.Length;i++)
            {
                float difference=Vector3.Distance(item.Value[i],other[i]);
                if(float.IsNaN(difference)||float.IsInfinity(difference))return float.PositiveInfinity;
                max=Mathf.Max(max,difference);count++;
            }
        }
        return max;
    }
    static string Hash(string path)
    {
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
    }
}
#endif
