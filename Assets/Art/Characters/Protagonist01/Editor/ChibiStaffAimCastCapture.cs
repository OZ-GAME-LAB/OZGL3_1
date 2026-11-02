#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// The saved controller drives both the equipped skin and the effect. Each facing is a new actor.
public static partial class ChibiMotionSet
{
    const string StaffCastAssets = "Assets/Art/Characters/Protagonist01/AnimationStudies/StaffTipBolt20261102/";
    const string StaffCastPrefab = StaffCastAssets + "Player_Chibi_StaffTipBolt.prefab";
    const string StaffCastClip = StaffCastAssets + "Study_StaffTipBolt_Chibi.anim";
    const string StaffIdleClip = StaffCastAssets + "Study_StaffTipIdle_Chibi.anim";
    const string StaffCastFx = StaffCastAssets + "Study_StaffTipBolt_FX.anim";
    const int SwaySubsteps = 4;

    [Serializable]
    sealed class StaffShotSample
    {
        public int frame;
        public float seconds;
        public string phase;
        public Vector3 rightShoulder, rightElbow, rightWrist, torso;
        public Vector3 effectTip, shaftDirection, boltCenter, normalizedTipOnScreen;
        public bool spawned;
    }

    [Serializable]
    sealed class StaffShotCapture
    {
        public string prefab = StaffCastPrefab, clip = StaffCastClip, idle = StaffIdleClip, fx = StaffCastFx;
        public int fps = RenderRate, releaseFrame = -1;
        public float idleSeconds, castSeconds, triggerSeconds, releaseSeconds;
        public string facing;
        public StaffShotSample[] samples;
    }

    static bool PlaysClip(Animator animator, AnimationClip clip)
        => animator.GetCurrentAnimatorClipInfo(0).Any(info => info.clip == clip);

    [MenuItem("Project M/Studies/Render Chibi staff idle and cast")]
    public static void CaptureStaffAimStudy()
    {
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(StaffIdleClip);
        var cast = AssetDatabase.LoadAssetAtPath<AnimationClip>(StaffCastClip);
        if (!idle || !cast || !AssetDatabase.LoadAssetAtPath<AnimationClip>(StaffCastFx))
            throw new FileNotFoundException("The idle, cast and paired effect clips must all exist.");
        var media = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "OZGL3_ArtReview/Characters/Protagonist01/Studies/ChibiStaffAimCast20261102/idle-sway");
        foreach (int direction in new[] {1, -1})
        {
            string facing = direction == 1 ? "right" : "left";
            string frames = Path.Combine(media, "frames", facing);
            string detailFolder = Path.Combine(media, "frames", "right-hand-detail");
            string frontFolder = Path.Combine(media, "frames", "right-hand-front");
            Directory.CreateDirectory(frames);
            if (direction == 1) { Directory.CreateDirectory(detailFolder); Directory.CreateDirectory(frontFolder); }
            using (var stage = new Stage())
            {
                var sideCenter = new Vector3(direction * .25f, .62f, 0f);
                var sideOffset = new Vector3(0f, .55f, -10f);
                stage.SetView(sideCenter, sideOffset, .87f);
                stage.camera.backgroundColor = new Color(.83f, .83f, .78f);
                var floor = stage.scene.GetRootGameObjects().Single(root => root.name == "Plane");
                floor.transform.localScale = new Vector3(.8f, 1f, 1.6f);
                floor.transform.position = new Vector3(0f, 0f, -5f);
                floor.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", new Color(.75f, .77f, .73f));

                var actor = CompatibilityActor(stage, StaffCastPrefab);
                actor.transform.rotation = Quaternion.LookRotation(Vector3.right * direction);
                Transform Bone(string name) => actor.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
                var tip = Bone("EffectTip");
                var shoulder = Bone("RightArm");
                var elbow = Bone("RightForeArm");
                var wrist = Bone("RightHand");
                var torso = Bone("Spine");
                var bolt = actor.transform.Find("PreviewBolt");
                var animator = actor.GetComponent<Animator>();
                if (!bolt || !animator || !animator.runtimeAnimatorController)
                    throw new InvalidOperationException("The equipped preview needs its Animator controller and PreviewBolt.");
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Update(0f);
                if (!PlaysClip(animator, idle))
                    throw new InvalidOperationException("The saved controller did not start in the idle clip.");

                // Absolute, fixed world positions for the whole take. The torso remains a reference for hand travel.
                var armCenter = Vector3.Lerp(shoulder.position, wrist.position, .60f) + Vector3.down * .02f;
                float armSize = Mathf.Max(.245f, Vector3.Distance(shoulder.position, wrist.position) * .91f);
                var detailOffset = actor.transform.right * 2f + actor.transform.forward * .67f + Vector3.up * .12f;
                var frontOffset = actor.transform.forward * 1.65f + actor.transform.right + Vector3.up * .06f;
                var skin = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(renderer => renderer.enabled)
                    .Select(renderer => new CpuSkin(renderer)).ToArray();
                var samples = new List<StaffShotSample>();
                const int simulationRate = RenderRate * SwaySubsteps;
                int triggerTick = Mathf.RoundToInt(idle.length * simulationRate);
                var capture = new StaffShotCapture { facing = facing, idleSeconds = idle.length,
                    castSeconds = cast.length, triggerSeconds = triggerTick / (float)simulationRate };
                bool sawCast = false, ended = false, lastVisible = false;
                int shots = 0;
                try
                {
                    int limit = Mathf.CeilToInt((idle.length * 2f + cast.length * 2f + 1f) * simulationRate);
                    for (int tick = 0; tick <= limit; tick++)
                    {
                        if (tick == triggerTick + 1) animator.SetTrigger("Cast");
                        if (tick > 0) animator.Update(1f / simulationRate);
                        bool visible = bolt.localScale.magnitude > .01f;
                        if (visible && !lastVisible)
                        {
                            if (tick <= triggerTick || Vector3.Distance(bolt.position, tip.position) > .065f ||
                                Vector3.Dot(tip.parent.up, actor.transform.forward) < .95f)
                                throw new InvalidOperationException("Controller release is mistimed or misaligned with the staff tip.");
                            shots++;
                            capture.releaseSeconds = tick / (float)simulationRate;
                            capture.releaseFrame = Mathf.CeilToInt(tick / (float)SwaySubsteps);
                        }
                        lastVisible = visible;
                        if (tick % SwaySubsteps != 0) continue;

                        int frame = tick / SwaySubsteps;
                        bool inCast = PlaysClip(animator, cast);
                        bool blend = animator.IsInTransition(0);
                        sawCast |= inCast;
                        if (sawCast && !inCast && !blend && PlaysClip(animator, idle) &&
                            animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f)
                        {
                            ended = true;
                            break;
                        }
                        string phase = blend ? (inCast ? "blendOut" : "blendIn") :
                            inCast ? "cast" : (sawCast ? "idleAfter" : "idle");
                        if ((phase == "idle" || phase == "idleAfter") && visible)
                            throw new InvalidOperationException("PreviewBolt is visible in the idle state.");
                        foreach (var snapshot in skin) snapshot.Bake();
                        string filename = frame.ToString("D3") + ".png";
                        stage.Save(Path.Combine(frames, filename));
                        samples.Add(new StaffShotSample {
                            frame = frame, seconds = tick / (float)simulationRate, phase = phase,
                            effectTip = tip.position, shaftDirection = tip.parent.up, boltCenter = bolt.position,
                            rightShoulder = shoulder.position, rightElbow = elbow.position, rightWrist = wrist.position,
                            torso = torso.position, normalizedTipOnScreen = stage.camera.WorldToViewportPoint(tip.position),
                            spawned = visible
                        });
                        if (direction == 1)
                        {
                            stage.SetView(armCenter, detailOffset, armSize);
                            stage.Save(Path.Combine(detailFolder, filename));
                            stage.SetView(armCenter, frontOffset, armSize);
                            stage.Save(Path.Combine(frontFolder, filename));
                            stage.SetView(sideCenter, sideOffset, .87f);
                        }
                    }
                    if (!ended || shots != 1 || skin.Length == 0)
                        throw new InvalidOperationException($"Expected one cast between two complete idle cycles; shots={shots}, returned={ended}.");
                    capture.samples = samples.ToArray();
                    File.WriteAllText(Path.Combine(frames, "capture.json"), JsonUtility.ToJson(capture, true));
                }
                finally
                {
                    foreach (var snapshot in skin) snapshot.Dispose();
                    Object.DestroyImmediate(actor);
                }
            }
        }
        Debug.Log("STAFF_IDLE_SWAY_CAPTURE_COMPLETE");
    }
}
#endif
