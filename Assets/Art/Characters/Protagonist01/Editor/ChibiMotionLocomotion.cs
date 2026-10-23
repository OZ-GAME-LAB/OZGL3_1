#if UNITY_EDITOR
using UnityEngine;

/// <summary>Compact locomotion poses for the isolated v007 motion review.</summary>
public static partial class ChibiMotionSet
{
    const float ChibiIdleDuration = 2.8f;
    const float ChibiWalkDuration = .86f;
    const float ChibiRunDuration = .62f;
    const float ChibiJumpDuration = 1.2f;
    const float ChibiDashDuration = .65f;

    public static Motion[] LocomotionSet()
    {
        return new[]
        {
            new Motion
            {
                name = "Idle_Fists", prop = "", duration = ChibiIdleDuration, loop = true,
                intent = "Upright closed-fist neutral; quiet breathing and a small, continuous weight shift.",
                phases = new[] { 0f, .7f, 1.4f, 2.1f, ChibiIdleDuration },
                evaluate = EvaluateChibiIdle
            },
            new Motion
            {
                name = "Walk_Fists", prop = "", duration = ChibiWalkDuration, loop = true,
                intent = "Compact in-place walk retargeted from the selected AI gait; preserve support/swing timing and closed fists.",
                phases = new[] { 0f, .215f, .43f, .645f, ChibiWalkDuration },
                evaluate = EvaluateChibiWalk
            },
            new Motion
            {
                name = "Run_Fists", prop = "", duration = ChibiRunDuration, loop = true,
                intent = "Short quick running steps from the selected AI cycle; restrained arm travel and a forward gaze.",
                phases = new[] { 0f, .155f, .31f, .465f, ChibiRunDuration },
                evaluate = EvaluateChibiRun
            },
            new Motion
            {
                name = "Jump_Full", prop = "", duration = ChibiJumpDuration, loop = false,
                intent = "Small preparation, compact airborne tuck, readable apex and absorbed landing. Hips contain the preview vertical trajectory; takeoff .16s, apex .44-.52s, landing .78s.",
                phases = new[] { 0f, .12f, .16f, .3f, .44f, .52f, .67f, .78f, .84f, 1.08f, ChibiJumpDuration },
                evaluate = EvaluateChibiJump
            },
            new Motion
            {
                name = "Dash_Forward", prop = "", duration = ChibiDashDuration, loop = false,
                intent = "Short in-place dash pose: slight anticipation, compact forward drive, brief readable hold and one controlled recovery. No gameplay displacement.",
                phases = new[] { 0f, .075f, .13f, .23f, .29f, .42f, .55f, ChibiDashDuration },
                evaluate = EvaluateChibiDash
            }
        };
    }

    static void EvaluateChibiIdle(Rig r, float t)
    {
        float phase = t / ChibiIdleDuration * Mathf.PI * 2;
        float breath = .5f - .5f * Mathf.Cos(phase);
        float drop = .004f * breath;
        float forward = .0015f * Mathf.Sin(phase);
        r.Stance(drop, 0, .55f * Mathf.Sin(phase), forward);

        // Keep both hands near their relaxed neutral. Small phase offsets avoid
        // turning the whole body into a single rigid bobbing block.
        Vector3 body = new Vector3(0, -drop, forward);
        r.Hand(true, r.rHandNeutral + body + Vector3.forward * (.0018f * Mathf.Sin(phase - .22f)));
        r.Hand(false, r.lHandNeutral + body + Vector3.forward * (.0014f * Mathf.Sin(phase - .4f)));
        r.Fist(true); r.Fist(false);
        r.Gaze(.25f * Mathf.Sin(phase - .3f));
        r.Grip();
        r.CorrectFeet();
    }

    static void EvaluateChibiWalk(Rig r, float t)
    {
        // The selected v005 AI source has a useful support/flight structure.
        // Reuse that structure instead of making all joints follow sine waves.
        r.SourceLocomotion("Walk_Fists", Mathf.Repeat(t / ChibiWalkDuration, 1), .74f);
        r.Fist(true); r.Fist(false);
        r.Gaze();
        r.Grip();
        r.CorrectFeet();
    }

    static void EvaluateChibiRun(Rig r, float t)
    {
        r.SourceLocomotion("Run_Fists", Mathf.Repeat(t / ChibiRunDuration, 1), .72f);
        r.ClearRunArms();
        r.Fist(true); r.Fist(false);
        r.Gaze();
        r.Grip();
        r.CorrectFeet();
    }

    static float ChibiJumpDrop(float t)
    {
        return Track(t, 0, 0, .12f, .046f, .19f, 0,
            .76f, 0, .84f, .054f, .94f, .019f, 1.08f, 0, ChibiJumpDuration, 0);
    }

    static float ChibiJumpLean(float t)
    {
        return Track(t, 0, 0, .12f, 3, .22f, 1,
            .44f, 2, .52f, 2, .73f, 1, .84f, 4, 1.08f, 0, ChibiJumpDuration, 0);
    }

    static float ChibiJumpForward(float t)
    {
        return Track(t, 0, 0, .12f, -.003f, .24f, .005f,
            .52f, .005f, .78f, .003f, 1.08f, 0, ChibiJumpDuration, 0);
    }

    static float ChibiJumpHeight(float t)
    {
        const float takeoff = .16f, apexIn = .44f, apexOut = .52f, landing = .78f, height = .47f;
        if (t <= takeoff || t >= landing) return 0;
        if (t < apexIn)
        {
            float u = Mathf.InverseLerp(takeoff, apexIn, t);
            return height * (1 - (1 - u) * (1 - u));
        }
        if (t <= apexOut) return height;
        float fall = Mathf.InverseLerp(apexOut, landing, t);
        return height * (1 - fall * fall);
    }

    static void EvaluateChibiJump(Rig r, float t)
    {
        r.Body(t, ChibiJumpDrop, ChibiJumpLean, _ => 0, ChibiJumpForward);
        float height = ChibiJumpHeight(t);
        float tuck = Track(t, 0, 0, .16f, 0, .3f, .8f, .44f, 1,
            .52f, 1, .69f, .4f, .78f, 0, ChibiJumpDuration, 0);
        r.hips.position += Vector3.up * height;

        // Both feet follow the authored jump trajectory, while the small
        // relative tuck articulates the short legs without a wide split pose.
        Vector3 left = r.leftFootTarget + new Vector3(0, height + .070f * tuck, .012f * tuck);
        Vector3 right = r.rightFootTarget + new Vector3(0, height + .025f * tuck, -.012f * tuck);
        Quaternion leftRotation = Quaternion.AngleAxis(5 * tuck, Vector3.right) * r.lfRotation;
        Quaternion rightRotation = Quaternion.AngleAxis(7 * tuck, Vector3.right) * r.rfRotation;
        r.Plant(left, right, leftRotation, rightRotation);

        Vector3 body = new Vector3(0, height - ChibiJumpDrop(t), ChibiJumpForward(t));
        float absorb = Track(t, 0, 0, .77f, 0, .84f, 1, .97f, 0, ChibiJumpDuration, 0);
        r.Hand(true, r.rHandNeutral + body + new Vector3(0, .035f * tuck - .008f * absorb, .052f * tuck));
        r.Hand(false, r.lHandNeutral + body + new Vector3(0, .03f * tuck - .006f * absorb, .043f * tuck));
        r.Fist(true); r.Fist(false);
        r.Gaze(Track(t, 0, 0, .12f, 1, .26f, -.75f, .52f, -.5f, .74f, 1.5f, .84f, 2, 1.06f, 0, ChibiJumpDuration, 0));
        r.Grip();
        r.CorrectFeet();
    }

    static float ChibiDashDrive(float t)
    {
        return Track(t, 0, 0, .075f, -.12f, .13f, 1,
            .29f, 1, .42f, .18f, .55f, 0, ChibiDashDuration, 0);
    }

    static float ChibiDashDrop(float t)
    {
        return Track(t, 0, 0, .075f, .030f, .13f, .018f,
            .29f, .018f, .4f, .025f, .55f, 0, ChibiDashDuration, 0);
    }

    static void EvaluateChibiDash(Rig r, float t)
    {
        r.Body(t, ChibiDashDrop,
            s => 20f * Mathf.Max(0, ChibiDashDrive(s)),
            _ => 0, s => .060f * ChibiDashDrive(s));
        float drive = Mathf.Max(0, ChibiDashDrive(t));
        float step = Track(t, 0, 0, .075f, 0, .15f, 1, .29f, 1, .5f, 0, ChibiDashDuration, 0);
        float lift = t > .075f && t < .15f ? .008f * Mathf.Sin(Mathf.PI * (t - .075f) / .075f) : 0;
        r.Plant(r.leftFootTarget + new Vector3(0, lift, .045f * step),
            r.rightFootTarget + new Vector3(0, 0, -.030f * step));

        Vector3 body = new Vector3(0, -ChibiDashDrop(t), .060f * ChibiDashDrive(t));
        r.Hand(true, r.rHandNeutral + body + new Vector3(0, .012f * drive, -.080f * drive));
        r.Hand(false, r.lHandNeutral + body + new Vector3(0, .016f * drive, .055f * drive));
        r.Fist(true); r.Fist(false);
        r.Gaze();
        r.Grip();
        r.CorrectFeet();
    }
}
#endif
