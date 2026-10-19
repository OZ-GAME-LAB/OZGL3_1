#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

// Disposable pose diagnostics, never a gameplay component.
public static class NaturalMotionProbe
{
    const string Out="ArtSources/Characters/Protagonist01/v005-natural-motion/probe";
    [Serializable] class Bone { public string name,parent;public Vector3 position,localPosition;public Quaternion rotation,localRotation; }
    [Serializable] class Shot {public string name;public Bone[] bones;public float torsoPitch,headPitch;public Vector3 meshMin,meshMax;}
    [Serializable] class Report {public Shot[] shots;}
    public static void Inspect()
    {
        Directory.CreateDirectory(Out);var shots=new List<Shot>();
        foreach(string name in new[]{"BindPose","AvatarZero","OldIdle","PrimeIdle","OldWalk","OldPunch"})
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Protagonist01/MotionPolish_v004/Prefabs/Protagonist01_MotionReview.prefab"));
            SceneManager.MoveGameObjectToScene(actor,scene);actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var animator=actor.GetComponent<Animator>();PlayableGraph graph=default;Mesh mesh=new Mesh();GameObject snapshot=null,camObj=null;
            try
            {
                var avatar=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Characters/Protagonist01/Runtime/Protagonist01_Rig.fbx").OfType<Avatar>().First();
                if(name=="BindPose")animator.enabled=false;
                else if(name=="AvatarZero")
                {
                    animator.avatar=avatar;animator.Rebind();
                    using(var handler=new HumanPoseHandler(avatar,actor.transform))
                    {var pose=new HumanPose();handler.GetHumanPose(ref pose);pose.muscles=new float[HumanTrait.MuscleCount];handler.SetHumanPose(ref pose);}
                }
                else
                {
                    bool prime=name=="PrimeIdle";
                    animator.avatar=prime?avatar:null;animator.Rebind();
                    string clipName=name=="OldWalk"?"Walk_Fists":name=="OldPunch"?"Punch_Right_Jab":"Idle_Fists";
                    string path="Assets/Art/Characters/Protagonist01/MotionPolish_v004/"+(prime?"Sources/":"Clips/")+clipName+(prime?".fbx":".anim");
                    var clip=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
                    graph=PlayableGraph.Create();graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var p=AnimationClipPlayable.Create(graph,clip);p.SetApplyFootIK(false);p.SetApplyPlayableIK(false);
                    AnimationPlayableOutput.Create(graph,"Probe",animator).SetSourcePlayable(p);graph.Play();p.SetTime(name=="OldPunch"?.333:prime?1:0);graph.Evaluate(0);
                }
                var all=actor.GetComponentsInChildren<Transform>();Transform B(string n)=>all.First(t=>t.name==n);
                var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();skin.BakeMesh(mesh);
                var vertices=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
                var axis=B("neck").position-B("Hips").position;var haxis=B("Head").position-B("neck").position;
                shots.Add(new Shot{name=name,bones=all.Select(t=>new Bone{name=t.name,parent=t.parent?t.parent.name:"",position=t.position,localPosition=t.localPosition,rotation=t.rotation,localRotation=t.localRotation}).ToArray(),torsoPitch=Mathf.Atan2(axis.z,axis.y)*Mathf.Rad2Deg,headPitch=Mathf.Atan2(haxis.z,haxis.y)*Mathf.Rad2Deg,meshMin=bounds.min,meshMax=bounds.max});
                snapshot=new GameObject("Actual CPU Skin",typeof(MeshFilter),typeof(MeshRenderer));SceneManager.MoveGameObjectToScene(snapshot,scene);snapshot.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);snapshot.transform.localScale=skin.transform.lossyScale;
                snapshot.GetComponent<MeshFilter>().sharedMesh=mesh;snapshot.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
                camObj=new GameObject("ProbeCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(camObj,scene);var camera=camObj.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=1.0f;camera.backgroundColor=new Color(.12f,.14f,.17f);camera.clearFlags=CameraClearFlags.SolidColor;
                camera.transform.position=new Vector3(4,.86f,-.15f);camera.transform.LookAt(new Vector3(0,.86f,0));
                var rt=new RenderTexture(720,720,24);var previous=RenderTexture.active;var pixels=new Texture2D(720,720,TextureFormat.RGB24,false);
                try {rt.Create();camera.targetTexture=rt;RenderTexture.active=rt;camera.Render();pixels.ReadPixels(new Rect(0,0,720,720),0,0);pixels.Apply();File.WriteAllBytes(Out+"/"+name+".png",pixels.EncodeToPNG());}
                finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(pixels);rt.Release();Object.DestroyImmediate(rt);}
            }
            finally{if(graph.IsValid())graph.Destroy();if(camObj)Object.DestroyImmediate(camObj);if(snapshot)Object.DestroyImmediate(snapshot);Object.DestroyImmediate(mesh);Object.DestroyImmediate(actor);EditorSceneManager.ClosePreviewScene(scene);}
        }
        File.WriteAllText(Out+"/pose-probe.json",JsonUtility.ToJson(new Report{shots=shots.ToArray()},true));Debug.Log("V005_POSE_PROBE_SUCCESS");
    }
}
#endif
