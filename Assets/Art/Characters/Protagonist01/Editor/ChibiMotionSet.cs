#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Offline animation assets and disposable review scenes only. No runtime component.
public static partial class ChibiMotionSet
{
    public const string Root="Assets/Art/Characters/Protagonist01/ChibiMotion_v007";
    public const string Report="ArtSources/Characters/Protagonist01/v007-compact-action";
    const string Input="Assets/Art/Characters/Protagonist01/ChibiCap_v006";
    const string RigPath=Input+"/Rig/Protagonist_ChibiCap_Rig.fbx";
    const string MaterialPath=Input+"/Materials/Protagonist_ChibiCap_Unlit.mat";
    static string FistsPath=>ChibiCylinderGripBuilder.MeshPath;
    public const string PrefabPath=Root+"/Prefabs/Protagonist_Chibi_MotionReview.prefab";
    public const int BakeRate=60, RenderRate=30, ImageSize=720;
    public sealed class Motion
    {
        public string name,prop="",intent=""; public float duration,sourceDuration;public bool loop;public float[] phases;
        public float releaseTime=-1; public Action<Rig,float> evaluate;
    }
    public sealed class Pose
    {
        public Vector3[] p,s;public Quaternion[] q;public float gripR=100,gripL=100;
        public Pose(){}
        public Pose(Transform[] t){p=t.Select(x=>x.localPosition).ToArray();q=t.Select(x=>x.localRotation).ToArray();s=t.Select(x=>x.localScale).ToArray();}
        public void Apply(Transform[] t){for(int i=0;i<t.Length;i++){t[i].localPosition=p[i];t[i].localRotation=q[i];t[i].localScale=s[i];}}
    }
    [Serializable] public sealed class FrameRow
    {
        public float time,leftSoleY,rightSoleY,gripL,gripR;public Vector3 hips,head,faceForward,leftHand,rightHand,leftFoot,rightFoot;
        public Vector3 rightShoulder,rightElbow,leftShoulder,leftElbow;public Vector3 swordTip,potionNozzle,mouth,rightHandLowest,leftToe,rightToe;
        public Vector3 propHilt,propAxis,propNormal;
    }
    public static Motion[] Definitions()
    {
        var all=LocomotionSet().Concat(CombatSet()).Concat(UtilitySet()).ToArray();
        foreach(var m in all)
        {
            float authored=m.duration,aligned=Mathf.Round(authored*RenderRate)/RenderRate;
            m.sourceDuration=authored;m.duration=aligned;
            var evaluate=m.evaluate;m.evaluate=(r,t)=>evaluate(r,t*authored/aligned);
            if(m.phases!=null)m.phases=m.phases.Select(x=>x*aligned/authored).ToArray();
            if(m.releaseTime>=0)m.releaseTime*=aligned/authored;
        }
        return all;
    }
    public static float S01(float x)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(x));
    public static Vector3 Arc(Vector3 a,Vector3 control,Vector3 b,float u){u=Mathf.Clamp01(u);return (1-u)*(1-u)*a+2*(1-u)*u*control+u*u*b;}
    static void SetGrip(SkinnedMeshRenderer skin,string name,float value){int i=skin.sharedMesh.GetBlendShapeIndex(name);if(i>=0)skin.SetBlendShapeWeight(i,value);}
    static float Grip(SkinnedMeshRenderer skin,string name){int i=skin.sharedMesh.GetBlendShapeIndex(name);return i>=0?skin.GetBlendShapeWeight(i):-1;}
    static int Depth(Transform t,Transform p){int n=0;while(t&&t!=p){n++;t=t.parent;}return n;}
    static Bounds SkinBounds(SkinnedMeshRenderer[] skins){var mesh=new Mesh();bool first=true;Bounds b=new Bounds();try{foreach(var skin in skins){skin.BakeMesh(mesh);foreach(var v in mesh.vertices){var p=skin.transform.TransformPoint(v);if(first){b=new Bounds(p,Vector3.zero);first=false;}else b.Encapsulate(p);}}}finally{Object.DestroyImmediate(mesh);}return b;}
    static Bounds SnapshotBounds(CpuSkin[] skins){var b=skins[0].renderer.bounds;foreach(var s in skins.Skip(1))b.Encapsulate(s.renderer.bounds);return b;}

public static float Track(float t, params float[] keys)
    {
        if (t <= keys[0]) return keys[1];
        for (int i = 0; i < keys.Length - 2; i += 2)
            if (t <= keys[i + 2]) return Mathf.LerpUnclamped(keys[i + 1], keys[i + 3], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(keys[i], keys[i + 2], t)));
        return keys[keys.Length - 1];
    }
public sealed partial class Rig : IDisposable
    {
        public readonly Transform hips, head, lh, rh, lf, rf;
        public readonly Transform neck, headfront, lua, lla, rua, rla, lul, lll, rul, rll, ls, rs;
        public readonly Transform[] spine, transforms;
        public readonly SkinnedMeshRenderer[] skins;
        readonly SoleProbe soles;
        public Pose neutral;
        public Vector3 leftFootTarget, rightFootTarget, rearPivot, lHandNeutral, rHandNeutral, lShoulder, rShoulder, lForearmNeutral, rForearmNeutral;
        public Quaternion lfRotation, rfRotation, lhRotation, rhRotation, headRotation;
        public float leftArmLength, rightArmLength, rightSign, leftSign;
        public Rig(GameObject actor, SkinnedMeshRenderer[] skins, Transform[] transforms)
        {
            this.skins = skins; this.transforms = transforms;
            Transform Find(params string[] names)
            {
                foreach (string name in names) { var t = transforms.FirstOrDefault(x => string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase)); if (t) return t; }
                throw new InvalidOperationException("Missing new rig bone: " + string.Join("/", names));
            }
            hips = Find("Hips", "Pelvis"); head = Find("Head"); neck = Find("neck", "Neck");
            headfront = transforms.FirstOrDefault(t => t.name == "headfront");
            spine = hips.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Spine", StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => Depth(t, hips)).Take(3).ToArray();
            if (spine.Length != 3) throw new InvalidOperationException("Expected the three actual parent-ordered Meshy spine joints.");
            lua = Find("LeftArm", "UpperArm_L"); lla = Find("LeftForeArm", "LowerArm_L"); lh = Find("LeftHand", "Hand_L");
            rua = Find("RightArm", "UpperArm_R"); rla = Find("RightForeArm", "LowerArm_R"); rh = Find("RightHand", "Hand_R");
            lul = Find("LeftUpLeg", "UpperLeg_L"); lll = Find("LeftLeg", "LowerLeg_L"); lf = Find("LeftFoot", "Foot_L");
            rul = Find("RightUpLeg", "UpperLeg_R"); rll = Find("RightLeg", "LowerLeg_R"); rf = Find("RightFoot", "Foot_R");
            ls = Find("LeftShoulder"); rs = Find("RightShoulder"); soles = new SoleProbe(skins, lf, rf, rh);
        }
        static int Depth(Transform t, Transform parent) { int n = 0; while (t && t != parent) { n++; t = t.parent; } return n; }
        public void MakeNeutral()
        {
            hips.rotation = Quaternion.FromToRotation((spine[0].position - hips.position).normalized, Vector3.up) * hips.rotation;
            lfRotation = lf.rotation; rfRotation = rf.rotation;
            Quaternion sourceLH = lh.rotation, sourceRH = rh.rotation;
            Vector3 sourceLDir = (lh.position - lla.position).normalized, sourceRDir = (rh.position - rla.position).normalized;
            soles.Bake();
            float leftOffset = lf.position.y - soles.Minimum(true), rightOffset = rf.position.y - soles.Minimum(false);
            float meanZ = (lf.position.z + rf.position.z) * .5f;
            leftSign = Mathf.Sign(lh.position.x - hips.position.x); rightSign = Mathf.Sign(rh.position.x - hips.position.x);
            float leftX = Mathf.Sign(lf.position.x - hips.position.x) * Mathf.Clamp(Mathf.Abs(lf.position.x), .075f, .14f);
            float rightX = Mathf.Sign(rf.position.x - hips.position.x) * Mathf.Clamp(Mathf.Abs(rf.position.x), .075f, .14f);
            leftFootTarget = new Vector3(leftX, leftOffset + .003f, meanZ + .025f);
            rightFootTarget = new Vector3(rightX, rightOffset + .003f, meanZ - .025f);
            Vector3 p = hips.position; p.x = (leftX + rightX) * .5f; p.z = meanZ;
            hips.position = p;
            float allowedY = Mathf.Min(MaxHipY(lul, lll, lf, leftFootTarget), MaxHipY(rul, rll, rf, rightFootTarget));
            p = hips.position; p.y = Mathf.Min(p.y, allowedY); hips.position = p;
            Legs(leftFootTarget, rightFootTarget, lfRotation, rfRotation);
            leftArmLength = Vector3.Distance(lua.position, lla.position) + Vector3.Distance(lla.position, lh.position);
            rightArmLength = Vector3.Distance(rua.position, rla.position) + Vector3.Distance(rla.position, rh.position);
            Vector3 leftTarget = lua.position + Vector3.down * (leftArmLength * .90f) + Vector3.forward * (leftArmLength * .18f) + Vector3.right * (leftSign * leftArmLength * .14f);
            Vector3 rightTarget = rua.position + Vector3.down * (rightArmLength * .90f) + Vector3.forward * (rightArmLength * .18f) + Vector3.right * (rightSign * rightArmLength * .14f);
            IK(lua, lla, lh, leftTarget, lua.position + Vector3.down * leftArmLength + Vector3.right * leftSign * .1f - Vector3.forward * .08f);
            IK(rua, rla, rh, rightTarget, rua.position + Vector3.down * rightArmLength + Vector3.right * rightSign * .1f - Vector3.forward * .08f);
            lh.rotation = Quaternion.FromToRotation(sourceLDir, (lh.position - lla.position).normalized) * sourceLH;
            rh.rotation = Quaternion.FromToRotation(sourceRDir, (rh.position - rla.position).normalized) * sourceRH;
            if (headfront && (headfront.position - head.position).sqrMagnitude > .000001f)
                head.rotation = Quaternion.FromToRotation((headfront.position - head.position).normalized, Vector3.forward) * head.rotation;
            headRotation = head.rotation; lhRotation = lh.rotation; rhRotation = rh.rotation;
            lForearmNeutral = (lh.position - lla.position).normalized; rForearmNeutral = (rh.position - rla.position).normalized;
            lHandNeutral = lh.position; rHandNeutral = rh.position; lShoulder = lua.position; rShoulder = rua.position;
            soles.Bake(); rearPivot = soles.FrontContact(false);
            neutral = new Pose(transforms); InitializeExtras();
        }
        float MaxHipY(Transform a, Transform b, Transform c, Vector3 foot)
        {
            float reach = (Vector3.Distance(a.position, b.position) + Vector3.Distance(b.position, c.position)) * .985f;
            float horizontal = new Vector2(a.position.x - foot.x, a.position.z - foot.z).magnitude;
            return foot.y + Mathf.Sqrt(Mathf.Max(.0001f, reach * reach - horizontal * horizontal)) - (a.position.y - hips.position.y);
        }
        static float Pulse(float t) => Track(t, 0, 0, .20f, -.30f, .29f, 1, .39f, 1, .49f, -.20f, .62f, .07f, .82f, 0, 1.2f, 0);
        public void EvaluatePrototype(float t)
        {
            neutral.Apply(transforms);
            float pulse = Pulse(t), drive = .04f * pulse;
            float compression = .035f * Track(t, 0, 0, .22f, 1, .30f, .12f, .39f, .12f, .48f, .35f, .65f, 0, 1.2f, 0);
            hips.position += new Vector3(leftSign * .010f * Mathf.Max(0, pulse), -compression, drive);
            hips.rotation = Quaternion.AngleAxis(-rightSign * 8 * pulse, Vector3.up) * Quaternion.AngleAxis(2 * Mathf.Max(0, pulse) + compression * 65, Vector3.right) * hips.rotation;
            for (int i = 0; i < 3; i++)
            {
                float delayed = Pulse(t - .018f * (i + 1));
                spine[i].rotation = Quaternion.AngleAxis(-rightSign * (3 + 2 * i) * delayed, Vector3.up) * Quaternion.AngleAxis((1 + i * .5f) * delayed, Vector3.right) * spine[i].rotation;
            }
            rs.rotation = Quaternion.AngleAxis(-rightSign * 4 * Pulse(t - .035f), Vector3.up) * rs.rotation;
            ls.rotation = Quaternion.AngleAxis(rightSign * 1.5f * Pulse(t - .045f), Vector3.up) * ls.rotation;
            head.rotation = Quaternion.AngleAxis(1.1f * Track(t, 0, 0, .20f, 1, .32f, -.5f, .39f, -.5f, .55f, .35f, .80f, 0, 1.2f, 0), Vector3.right) * headRotation;

            float step = .025f * Track(t, 0, 0, .13f, 0, .29f, 1, .45f, 1, .76f, 0, 1.2f, 0);
            float lift = t > .13f && t < .29f ? .009f * Mathf.Sin(Mathf.PI * (t - .13f) / .16f) : 0;
            float heel = 8 * Mathf.Max(0, Pulse(t - .015f));
            Quaternion roll = Quaternion.AngleAxis(heel, Vector3.right);
            Vector3 leftGoal = leftFootTarget + new Vector3(0, lift, step);
            Vector3 rightGoal = rearPivot + roll * (rightFootTarget - rearPivot);
            Legs(leftGoal, rightGoal, lfRotation, roll * rfRotation);

            Vector3 rightGuard = rShoulder + new Vector3(rightSign * rightArmLength * .05f, -rightArmLength * .40f, rightArmLength * .45f);
            Vector3 rightWind = rightGuard + new Vector3(rightSign * rightArmLength * .07f, .015f, -rightArmLength * .10f);
            // Strike from the evaluated shoulder. Using the cached neutral shoulder
            // lost reach when hip/chest rotation carried the shoulder forward.
            Vector3 rightStrike = rua.position + Vector3.forward * (rightArmLength * .99f);
            Vector3 rightTarget = MotionPath(t, new[] { 0f, .16f, .22f, .32f, .39f, .50f, .62f, .76f, 1.05f, 1.2f },
                new[] { rHandNeutral, rightGuard, rightWind, rightStrike, rightStrike, rightGuard - Vector3.forward * .025f,
                    rightGuard + Vector3.forward * .010f, rightGuard, rHandNeutral, rHandNeutral });
            Vector3 leftGuard = lShoulder + new Vector3(leftSign * leftArmLength * .05f, -leftArmLength * .40f, leftArmLength * .45f);
            Vector3 leftTarget = MotionPath(t, new[] { 0f, .17f, .25f, .35f, .50f, .78f, 1.05f, 1.2f },
                new[] { lHandNeutral, leftGuard, leftGuard, leftGuard - Vector3.forward * .018f + Vector3.up * .008f,
                    leftGuard + Vector3.forward * .008f, leftGuard, lHandNeutral, lHandNeutral });
            float fullExtension = Track(t, 0, 0, .22f, 0, .32f, 1, .39f, 1, .50f, 0, 1.2f, 0);
            rightTarget += new Vector3(leftSign * .006f * Mathf.Max(0, pulse), -compression * .35f, drive) * (1 - fullExtension);
            leftTarget += new Vector3(leftSign * .006f * Mathf.Max(0, pulse), -compression * .35f, drive * .8f);
            IK(rua, rla, rh, rightTarget, rua.position + Vector3.down * rightArmLength + Vector3.right * rightSign * .14f - Vector3.forward * .10f);
            IK(lua, lla, lh, leftTarget, lua.position + Vector3.down * leftArmLength + Vector3.right * leftSign * .14f - Vector3.forward * .10f);
            rh.rotation = Quaternion.FromToRotation(rForearmNeutral, (rh.position - rla.position).normalized) * rhRotation;
            lh.rotation = Quaternion.FromToRotation(lForearmNeutral, (lh.position - lla.position).normalized) * lhRotation;
            for (int iteration = 0; iteration < 2; iteration++)
            {
                soles.Bake(); float l = soles.Minimum(true), r = soles.Minimum(false);
                if (l >= -.0005f && r >= -.0005f) break;
                leftGoal.y += Mathf.Max(0, -l + .001f); rightGoal.y += Mathf.Max(0, -r + .001f);
                Legs(leftGoal, rightGoal, lfRotation, roll * rfRotation);
            }
        }
        void Legs(Vector3 left, Vector3 right, Quaternion leftRot, Quaternion rightRot)
        {
            var forward=hips.root.forward;var side=hips.root.right;
            IK(lul, lll, lf, left, lul.position + forward * .5f + side * leftSign * .04f);
            lf.rotation = leftRot;
            IK(rul, rll, rf, right, rul.position + forward * .5f + side * rightSign * .04f);
            rf.rotation = rightRot;
        }
        public FrameRow Frame(float t)
        {
            soles.Bake(); return new FrameRow { time = t, hips = hips.position, head = head.position,
                faceForward = headfront ? (headfront.position - head.position).normalized : Vector3.zero,
                leftHand = lh.position, rightHand = rh.position, leftFoot = lf.position, rightFoot = rf.position,
                leftSoleY = soles.Minimum(true), rightSoleY = soles.Minimum(false),rightHandLowest=soles.RightHandLowest,leftToe=soles.FrontContact(true,hips.root.forward),rightToe=soles.FrontContact(false,hips.root.forward),
                gripL = skins.Max(s => ChibiMotionSet.Grip(s, "Grip_L")), gripR = skins.Max(s => ChibiMotionSet.Grip(s, "Grip_R")), rightShoulder=rua.position,rightElbow=rla.position,leftShoulder=lua.position,leftElbow=lla.position, swordTip=rh.position+rh.rotation*SwordTipLocal,potionNozzle=rh.position+rh.rotation*PotionNozzleLocal,mouth=Mouth };
        }
        public void Dispose() { DisposeExtras();soles.Dispose(); }
    }
static void IK(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint)
    {
        Vector3 origin = a.position, vector = target - origin;
        float first = Vector3.Distance(origin, b.position), second = Vector3.Distance(b.position, c.position);
        if (first < .00001f || second < .00001f || vector.sqrMagnitude < .0000001f) return;
        Vector3 direction = vector.normalized;
        float distance = Mathf.Clamp(vector.magnitude, Mathf.Abs(first - second) + .0001f, first + second - .0001f);
        Vector3 bend = Vector3.ProjectOnPlane(hint - origin, direction).normalized;
        if (bend.sqrMagnitude < .01f) bend = Vector3.ProjectOnPlane(Vector3.forward, direction).normalized;
        float cosine = Mathf.Clamp((first * first + distance * distance - second * second) / (2 * first * distance), -1, 1);
        Vector3 elbow = origin + direction * (first * cosine) + bend * (first * Mathf.Sqrt(Mathf.Max(0, 1 - cosine * cosine)));
        a.rotation = Quaternion.FromToRotation(b.position - origin, elbow - origin) * a.rotation;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
    }
static Vector3 MotionPath(float t, float[] times, Vector3[] positions)
    {
        if (t <= times[0]) return positions[0];
        for (int i = 0; i < times.Length - 1; i++) if (t <= times[i + 1])
            return Vector3.LerpUnclamped(positions[i], positions[i + 1], Mathf.SmoothStep(0, 1, Mathf.InverseLerp(times[i], times[i + 1], t)));
        return positions[positions.Length - 1];
    }
sealed class SoleProbe : IDisposable
    {
        readonly SkinnedMeshRenderer[] skins;
        readonly Mesh[] meshes;
        readonly int[][] left, right, rightHand;
        readonly List<Vector3> l = new List<Vector3>(), r = new List<Vector3>(), hand = new List<Vector3>();
        public Vector3 RightHandLowest=>hand.OrderBy(p=>p.y).First();
        public SoleProbe(SkinnedMeshRenderer[] skins, Transform lf, Transform rf, Transform rh)
        {
            this.skins = skins; meshes = skins.Select(s => new Mesh()).ToArray();
            left = skins.Select(s => Indices(s, lf)).ToArray(); right = skins.Select(s => Indices(s, rf)).ToArray();rightHand=skins.Select(s=>Indices(s,rh)).ToArray();
        }
        static int[] Indices(SkinnedMeshRenderer skin, Transform foot)
        {
            var bones = skin.bones; var weights = skin.sharedMesh.boneWeights; var result = new List<int>();
            bool Match(int i) => i >= 0 && i < bones.Length && bones[i] && (bones[i] == foot || bones[i].IsChildOf(foot));
            for (int i = 0; i < weights.Length; i++)
            {
                var w = weights[i]; float sum = (Match(w.boneIndex0) ? w.weight0 : 0) + (Match(w.boneIndex1) ? w.weight1 : 0) + (Match(w.boneIndex2) ? w.weight2 : 0) + (Match(w.boneIndex3) ? w.weight3 : 0);
                if (sum > .35f) result.Add(i);
            }
            return result.ToArray();
        }
        public void Bake()
        {
            l.Clear(); r.Clear();hand.Clear();
            for (int i = 0; i < skins.Length; i++)
            {
                skins[i].BakeMesh(meshes[i]); var vertices = meshes[i].vertices;
                foreach (int j in left[i]) l.Add(skins[i].transform.TransformPoint(vertices[j]));
                foreach (int j in right[i]) r.Add(skins[i].transform.TransformPoint(vertices[j]));
                foreach (int j in rightHand[i]) hand.Add(skins[i].transform.TransformPoint(vertices[j]));
            }
            if (l.Count == 0 || r.Count == 0) throw new InvalidOperationException("Cannot identify actual foot-weighted skin vertices.");
        }
        public float Minimum(bool isLeft) => (isLeft ? l : r).Min(p => p.y);
        public Vector3 FrontContact(bool isLeft,Vector3? forward=null)
        {
            var list = isLeft ? l : r; float min = list.Min(p => p.y);
            Vector3 p = list.Where(v => v.y <= min + .012f).OrderByDescending(v => Vector3.Dot(v,forward??Vector3.forward)).First(); p.y = min; return p;
        }
        public void Dispose() { foreach (var mesh in meshes) Object.DestroyImmediate(mesh); }
    }
sealed class CpuSkin : IDisposable
    {
        readonly SkinnedMeshRenderer source;
        readonly GameObject obj;
        readonly Mesh mesh;
        readonly bool enabled;
        public readonly MeshRenderer renderer;
        public CpuSkin(SkinnedMeshRenderer source)
        {
            this.source = source; enabled = source.enabled;
            obj = new GameObject("Disposable CPU Skin Snapshot", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(obj, source.gameObject.scene); obj.transform.SetParent(source.transform, false);
            mesh = new Mesh(); mesh.MarkDynamic(); obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; source.enabled = false;
        }
        public void Bake() { source.BakeMesh(mesh); mesh.RecalculateBounds(); mesh.UploadMeshData(false); }
        public void Dispose() { if (source) source.enabled = enabled; Object.DestroyImmediate(obj); Object.DestroyImmediate(mesh); }
    }
sealed class Stage : IDisposable
    {
        public readonly Scene scene;
        public readonly Camera camera;
        readonly Scene previous;
        readonly RenderTexture target, previousTarget;
        readonly Texture2D pixels;
        readonly Material floorMaterial;
        readonly bool asyncCompilation;
        public Stage()
        {
            previous = SceneManager.GetActiveScene(); previousTarget = RenderTexture.active;
            asyncCompilation = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            scene = EditorSceneManager.NewPreviewScene(); camera = New("Elastic Direction Review Camera").AddComponent<Camera>();
            camera.scene = scene; camera.orthographic = true; camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.22f, .25f, .29f);
            camera.nearClipPlane = .02f; camera.farClipPlane = 50; camera.allowHDR = false;
            target = new RenderTexture(ImageSize, ImageSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            target.Create(); camera.targetTexture = target;
            pixels = new Texture2D(ImageSize, ImageSize, TextureFormat.RGB24, false);
            floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            floorMaterial.SetColor("_BaseColor", new Color(.16f, .185f, .215f));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); SceneManager.MoveGameObjectToScene(floor, scene);
            Object.DestroyImmediate(floor.GetComponent<Collider>()); floor.transform.localScale = new Vector3(.8f, 1, .6f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
        }
        public GameObject New(string name) { var o = new GameObject(name); SceneManager.MoveGameObjectToScene(o, scene); return o; }
        public void Frame(Bounds bounds, Vector3 center, Vector3 offset)
        {
            SetView(center, offset, .80f); float maxX = 0, maxY = 0;
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
            {
                Vector3 d = bounds.center + Vector3.Scale(bounds.extents, new Vector3(x, y, z)) - center;
                maxX = Mathf.Max(maxX, Mathf.Abs(Vector3.Dot(d, camera.transform.right))); maxY = Mathf.Max(maxY, Mathf.Abs(Vector3.Dot(d, camera.transform.up)));
            }
            camera.orthographicSize = Mathf.Max(.80f, maxX * 1.15f, maxY * 1.15f);
        }
        public void SetView(Vector3 center, Vector3 offset, float size)
        { camera.transform.position = center + offset; camera.transform.LookAt(center); camera.orthographicSize = size; }
        public void Save(string path)
        {
            camera.Render(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, ImageSize, ImageSize), 0, 0); pixels.Apply(false, false); File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        public void Dispose()
        {
            RenderTexture.active = previousTarget; camera.targetTexture = null;
            Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); Object.DestroyImmediate(floorMaterial);
            EditorSceneManager.ClosePreviewScene(scene); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            ShaderUtil.allowAsyncCompilation = asyncCompilation;
        }
    }
}
#endif
