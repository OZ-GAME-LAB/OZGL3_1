#if UNITY_EDITOR
using System;
using UnityEngine;

public static partial class ChibiMotionSet
{
    public static Motion[] UtilitySet()
    {
        return new[]
        {
            new Motion { name = "Potion_Drink", prop = "Potion", duration = 2.0f, loop = false,
                intent = "Lift a small potion, keep its actual nozzle at the mouth during the sip, lower and return upright with closed fists.",
                phases = new[] { 0f, .18f, .46f, .72f, 1.18f, 1.48f, 1.80f, 2f }, evaluate = UtilityDrink },
            new Motion { name = "Item_Throw_Overhand", prop = "ThrowItem", duration = 1.3f, loop = false,
                releaseTime = .52f,
                intent = "Compact shoulder-led overhand throw beside the large head; fast release, forward gaze, restrained follow-through and recovery.",
                phases = new[] { 0f, .18f, .35f, .45f, .52f, .62f, .80f, 1.05f, 1.3f }, evaluate = UtilityThrow },
            new Motion { name = "Hit_Reaction", prop = "", duration = .65f, loop = false,
                intent = "Short sharp recoil and small compression, one damped settle, then upright recovery; no prolonged folded stomach pose.",
                phases = new[] { 0f, .07f, .15f, .26f, .40f, .65f }, evaluate = UtilityHit },
            new Motion { name = "Interact_Reach", prop = "", duration = .95f, loop = false,
                intent = "Small deliberate chest-height button press with a readable contact pause, soft hand withdrawal and closed-fist ready.",
                phases = new[] { 0f, .14f, .31f, .40f, .54f, .76f, .95f }, evaluate = UtilityInteract },
            new Motion { name = "Pickup_Low", prop = "", duration = 1.35f, loop = false,
                intent = "Deep short-legged squat and downward hand reach to ground level in front of the shoes, grasp, lift and stand upright.",
                phases = new[] { 0f, .16f, .42f, .58f, .70f, .91f, 1.12f, 1.35f }, evaluate = UtilityPickup }
        };
    }

    static Vector3 UtilityRelaxed(Rig r, bool right)
    {
        return right ? r.rua.position + (r.rHandNeutral - r.rShoulder)
                     : r.lua.position + (r.lHandNeutral - r.lShoulder);
    }

    static Vector3 UtilityGuard(Rig r, bool right)
    {
        float length = right ? r.rightArmLength : r.leftArmLength;
        float sign = right ? r.rightSign : r.leftSign;
        Vector3 shoulder = right ? r.rua.position : r.lua.position;
        return shoulder + new Vector3(sign * length * .08f, -length * .35f, length * .48f);
    }

    static Vector3 UtilityPath(float t, float[] times, Vector3[] positions)
    {
        if (t <= times[0]) return positions[0];
        for (int i = 0; i < times.Length - 1; i++)
        {
            if (t > times[i + 1]) continue;
            float u = Mathf.InverseLerp(times[i], times[i + 1], t);
            return Vector3.LerpUnclamped(positions[i], positions[i + 1], u * u * (3 - 2 * u));
        }
        return positions[positions.Length - 1];
    }

    static void UtilityFinish(Rig r, float rightGrip = 100, float leftGrip = 100)
    {
        r.Grip(rightGrip, leftGrip);
        r.CorrectFeet();
    }

    static void UtilityDrink(Rig r, float t)
    {
        float active = Track(t, 0, 0, .12f, 0, .42f, 1, 1.42f, 1, 1.80f, 0, 2, 0);
        float sip = Track(t, 0, 0, .46f, 0, .68f, 1, 1.16f, 1, 1.40f, 0, 2, 0);
        r.Body(t,
            x => Track(x, 0, 0, .18f, .010f, .42f, 0, 1.45f, 0, 1.65f, .005f, 2, 0),
            x => 0,
            x => 0,
            x => .006f * Track(x, 0, 0, .40f, 1, 1.45f, 1, 1.80f, 0, 2, 0));
        r.Gaze(-3f * sip, 0);

        // Protract the real new shoulder before solving the short arm's contact.
        // This changes the evaluated shoulder instead of copying an old wrist coordinate.
        r.rs.rotation = Quaternion.AngleAxis(-r.rightSign * 40f * active, Vector3.up) * r.rs.rotation;
        Vector3 bottleDirection = Vector3.Slerp(Vector3.up,
            new Vector3(0, .9063078f, -.4226183f), sip).normalized;
        r.HandFrame(true, bottleDirection, Vector3.right);
        Vector3 contact = r.Mouth - r.rh.rotation * r.PotionNozzleLocal;
        Vector3 relaxedR = UtilityRelaxed(r, true);
        Vector3 chest = r.rua.position + new Vector3(r.rightSign * .012f,
            -r.rightArmLength * .48f, r.rightArmLength * .36f);
        Vector3 rightGoal = UtilityPath(t,
            new[] { 0f, .14f, .46f, 1.42f, 1.67f, 1.88f, 2f },
            new[] { relaxedR, chest, contact, contact, chest, relaxedR, relaxedR });
        r.Hand(true, rightGoal);
        Quaternion relaxedHandFrame = r.rh.rotation;
        r.HandFrame(true, bottleDirection, Vector3.right);
        r.rh.rotation = Quaternion.Slerp(relaxedHandFrame, r.rh.rotation, active);
        r.Hand(false, UtilityRelaxed(r, false));
        r.Fist(false);
        UtilityFinish(r, Track(t, 0, 100, .12f, 75, 1.72f, 75, 1.90f, 100, 2, 100));
    }

    static void UtilityThrow(Rig r, float t)
    {
        r.Body(t,
            x => .028f * Track(x, 0, 0, .22f, .25f, .42f, 1, .54f, .10f,
                .68f, .25f, .90f, 0, 1.3f, 0),
            x => 2f * Track(x, 0, 0, .40f, -.5f, .54f, 1, .72f, -.15f, .95f, 0, 1.3f, 0),
            x => -r.rightSign * 9f * Track(x, 0, 0, .40f, -.35f, .53f, 1,
                .67f, .65f, .86f, -.08f, 1.05f, 0, 1.3f, 0),
            x => .033f * Track(x, 0, 0, .40f, -.35f, .53f, 1,
                .67f, .65f, .86f, -.08f, 1.05f, 0, 1.3f, 0));
        r.Gaze(0, 0);
        float length = r.rightArmLength;
        Vector3 shoulder = r.rua.position;
        Vector3 relaxed = UtilityRelaxed(r, true);
        Vector3 guard = UtilityGuard(r, true);
        // The three-head character cannot reach over the cap. Wind beside the ear,
        // outside the actual large head silhouette, then throw across the shoulder.
        Vector3 wind = shoulder + new Vector3(r.rightSign * length * .68f, length * .55f, -length * .20f);
        Vector3 release = shoulder + new Vector3(r.rightSign * length * .16f, length * .27f, length * .91f);
        Vector3 follow = release + Vector3.down * (length * .14f);
        r.Hand(true, UtilityPath(t,
            new[] { 0f, .16f, .35f, .44f, .52f, .62f, .78f, .94f, 1.12f, 1.3f },
            new[] { relaxed, guard, wind, wind, release, follow, guard - Vector3.forward * .014f,
                guard, relaxed, relaxed }));
        r.Fist(true);
        float otherGuard = Track(t, 0, 0, .20f, 1, .70f, 1, 1.08f, 0, 1.3f, 0);
        r.Hand(false, Vector3.Lerp(UtilityRelaxed(r, false), UtilityGuard(r, false), otherGuard));
        r.Fist(false);
        UtilityFinish(r, Track(t, 0, 100, .12f, 75, .48f, 75, .52f, 0, .66f, 0, .83f, 100, 1.3f, 100));
    }

    static void UtilityHit(Rig r, float t)
    {
        r.Body(t,
            x => Track(x, 0, 0, .07f, .028f, .16f, .014f, .30f, .003f, .46f, 0, .65f, 0),
            x => Track(x, 0, 0, .065f, -7, .15f, -3, .26f, 1.4f, .40f, 0, .65f, 0),
            x => -r.rightSign * Track(x, 0, 0, .07f, 4, .16f, 2, .28f, -.6f, .43f, 0, .65f, 0),
            x => Track(x, 0, 0, .065f, -.028f, .16f, -.014f, .28f, .004f, .43f, 0, .65f, 0));
        r.Gaze(Track(t, 0, 0, .07f, -2, .18f, -.5f, .30f, .4f, .44f, 0, .65f, 0), 0);
        float guard = Track(t, 0, 0, .075f, 1, .18f, .85f, .35f, .2f, .53f, 0, .65f, 0);
        r.Hand(true, Vector3.Lerp(UtilityRelaxed(r, true), UtilityGuard(r, true), guard)); r.Fist(true);
        r.Hand(false, Vector3.Lerp(UtilityRelaxed(r, false), UtilityGuard(r, false), guard * .8f)); r.Fist(false);
        UtilityFinish(r);
    }

    static void UtilityInteract(Rig r, float t)
    {
        r.Body(t,
            x => .007f * Track(x, 0, 0, .18f, .5f, .31f, 1, .42f, 1, .66f, 0, .95f, 0),
            x => .8f * Track(x, 0, 0, .31f, 1, .42f, 1, .65f, 0, .95f, 0),
            x => 0,
            x => .012f * Track(x, 0, 0, .31f, 1, .42f, 1, .65f, 0, .95f, 0));
        r.Gaze(0, 0);
        Vector3 relaxed = UtilityRelaxed(r, true);
        Vector3 pre = r.rua.position + new Vector3(r.rightSign * .008f,
            -r.rightArmLength * .22f, r.rightArmLength * .48f);
        Vector3 contact = r.rua.position + new Vector3(r.rightSign * .008f,
            -r.rightArmLength * .12f, r.rightArmLength * .76f);
        r.Hand(true, UtilityPath(t,
            new[] { 0f, .14f, .31f, .40f, .54f, .76f, .95f },
            new[] { relaxed, pre, contact, contact, pre - Vector3.forward * .008f, relaxed, relaxed }));
        r.Fist(true);
        r.Hand(false, UtilityRelaxed(r, false)); r.Fist(false);
        UtilityFinish(r, Track(t, 0, 100, .17f, 45, .43f, 45, .62f, 100, .95f, 100));
    }

    static void UtilityPickup(Rig r, float t)
    {
        float reach = Track(t, 0, 0, .16f, .08f, .42f, 1, .62f, 1, .76f, .72f, 1.07f, 0, 1.35f, 0);
        r.Body(t,
            x => .255f * Track(x, 0, 0, .15f, .08f, .42f, 1, .64f, 1, .82f, .48f, 1.10f, 0, 1.35f, 0),
            x => 55f * Track(x, 0, 0, .18f, .10f, .42f, 1, .62f, 1, .82f, .42f, 1.08f, 0, 1.35f, 0),
            x => -r.rightSign * 4f * Track(x, 0, 0, .42f, 1, .64f, 1, 1.08f, 0, 1.35f, 0),
            x => -.015f * Track(x, 0, 0, .42f, 1, .64f, 1, 1.08f, 0, 1.35f, 0));
        r.Gaze(10f * reach, 0);
        r.rs.rotation=Quaternion.AngleAxis(-r.rightSign*20f*reach,Vector3.up)*r.rs.rotation;
        Vector3 ground = new Vector3(r.rightSign * .045f, .173f, .28f);
        // Bring the body over the grounded feet instead of stretching or clamping the arm.
        Vector3 gap=ground-r.rua.position;float reachLength=r.rightArmLength*.985f;
        float availableForward=Mathf.Sqrt(Mathf.Max(.0001f,reachLength*reachLength-gap.x*gap.x-gap.y*gap.y));
        float transfer=Mathf.Clamp(gap.z-availableForward,0,.08f)*reach;
        r.hips.position+=Vector3.forward*transfer;r.Plant(r.leftFootTarget,r.rightFootTarget);
        Vector3 relaxedR = UtilityRelaxed(r, true);
        Vector3 lift = relaxedR + Vector3.forward * .055f + Vector3.up * .035f;
        r.Hand(true, UtilityPath(t,
            new[] { 0f, .16f, .42f, .62f, .82f, 1.10f, 1.35f },
            new[] { relaxedR, relaxedR + Vector3.forward * .035f, ground, ground, lift, relaxedR, relaxedR }));
        r.Fist(true);
        Quaternion relaxedRotation = r.rh.rotation;
        // Right-hand local +Y follows the fingers. Point it down for ground contact.
        r.HandFrame(true, Vector3.back, Vector3.right);
        r.rh.rotation = Quaternion.Slerp(relaxedRotation, r.rh.rotation,
            Track(t, 0, 0, .22f, .25f, .40f, 1, .61f, 1, .82f, .25f, 1.06f, 0, 1.35f, 0));
        Vector3 leftSupport = r.lua.position + new Vector3(r.leftSign * .015f,
            -r.leftArmLength * .60f, r.leftArmLength * .25f);
        r.Hand(false, Vector3.Lerp(UtilityRelaxed(r, false), leftSupport, reach)); r.Fist(false);
        UtilityFinish(r, Track(t, 0, 100, .18f, 65, .36f, 0, .53f, 0, .63f, 75, .88f, 75, 1.10f, 100, 1.35f, 100));
    }
}
#endif
