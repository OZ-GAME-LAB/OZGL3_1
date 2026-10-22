#if UNITY_EDITOR
using UnityEngine;

public static partial class ChibiMotionSet
{
    public static Motion[] CombatSet()
    {
        return new[]
        {
            new Motion { name = "Punch_Right_Jab", duration = .55f, loop = false, prop = "",
                intent = "Compact straight punch: small compression, fast full reach, two-frame impact hold, controlled return.",
                phases = new[] { .10f, .17f, .23f, .42f, .55f }, evaluate = CompactPunch },
            new Motion { name = "Sword_Horizontal_Slash", duration = .65f, loop = false, prop = "Sword",
                intent = "One waist-height inside-to-outside cut. A lower guarded hilt follows the arm's reach outward, then retracts with the elbow before a modest point rise to ready.",
                phases = new[] { .11f, .24f, .32f, .46f, .65f }, evaluate = CompactSwordSlash },
            new Motion { name = "Staff_Cast_Forward", duration = .65f, loop = false, prop = "Staff",
                intent = "Continuous compact draw, forward release and flowing recovery; hand, staff pitch and body share C2 timing without a frozen release hold.",
                phases = new[] { .11f, .24f, .32f, .46f, .65f }, evaluate = CompactStaffCast }
        };
    }

    static float CombatPulse(float t, float wind, float strike, float hold, float recoil, float finish)
    { return Track(t, 0, 0, wind, -.22f, strike, 1, hold, 1, recoil, -.08f, Mathf.Max(recoil + .02f, finish - .07f), 0, finish, 0); }

    static void CompactPunch(Rig r, float t)
    {
        float p = CombatPulse(t, .10f, .17f, .23f, .36f, .55f);
        r.Body(t,
            x => .014f * Track(x, 0, 0, .10f, 1, .17f, .14f, .23f, .14f, .32f, .20f, .45f, 0, .55f, 0),
            x => 2 * Mathf.Max(0, CombatPulse(x, .10f, .17f, .23f, .36f, .55f)),
            x => -r.rightSign * 8 * CombatPulse(x, .10f, .17f, .23f, .36f, .55f),
            x => .018f * CombatPulse(x, .10f, .17f, .23f, .36f, .55f));
        CombatFeet(r, Mathf.Max(0, p), .006f);
        Vector3 carry = Vector3.forward * (.018f * p) + Vector3.down * (.004f * Mathf.Max(0, p));
        Vector3 guard = r.rShoulder + new Vector3(r.rightSign * r.rightArmLength * .03f, -r.rightArmLength * .65f, r.rightArmLength * .30f) + carry;
        Vector3 wind = guard - Vector3.forward * .012f;
        Vector3 direction = (Vector3.forward - Vector3.up * .25f).normalized;
        Vector3 hit = r.rua.position + direction * (r.rightArmLength * .985f);
        Vector3 right = CombatPath(t,
            new[] { 0f, .075f, .10f, .17f, .23f, .34f, .42f, .55f },
            new[] { r.rHandNeutral, guard, wind, hit, hit, guard - Vector3.forward * .006f, guard, r.rHandNeutral });
        float guardAmount = Track(t, 0, 0, .095f, 1, .23f, 1, .39f, .65f, .55f, 0);
        Vector3 leftGuard = r.lShoulder + new Vector3(r.leftSign * r.leftArmLength * .04f, -r.leftArmLength * .65f, r.leftArmLength * .30f);
        r.Hand(true, right); r.Fist(true);
        r.Hand(false, Vector3.Lerp(r.lHandNeutral, leftGuard + carry * .5f, guardAmount)); r.Fist(false);
        r.Gaze(); r.Grip(100, 100);
    }

    static void CompactSwordSlash(Rig r, float t)
    {
        // AHF cut 6: pass through the target, then recover the point to guard.
        // The pelvis starts the compact turn; weapon motion is authored at the hilt,
        // so the grip calibration, not an arbitrary wrist axis, determines the hand.
        r.Body(t,
            x => CombatFlow(CombatLeadTime(x), 0, .006f, .010f, .008f, .002f, 0),
            x => CombatFlow(CombatLeadTime(x), 0, -.4f, 1.8f, 1.4f, .35f, 0),
            x => r.rightSign * CombatFlow(CombatLeadTime(x), 0, -4, 7, 9, 1.6f, 0),
            x => CombatFlow(CombatLeadTime(x), 0, -.003f, .016f, .018f, .004f, 0));
        float supportWeight = CombatFlow(t, 0, .15f, .8f, 1, .20f, 0);
        CombatFeet(r, Mathf.Clamp01(supportWeight), .005f);
        Vector3 ready = r.WeaponReadyHilt(true);
        // Sword guard is lower than the staff guard: the forearm must be able to
        // follow the diagonal grasp instead of the palm folding below a high wrist.
        ready.y = r.HipNeutral.y + .025f;
        Vector3 inside = new Vector3(r.rightSign * .070f, ready.y, r.HipNeutral.z + .190f);
        Vector3 through = new Vector3(r.rightSign * .230f, ready.y, r.HipNeutral.z + .205f);
        Vector3 outside = new Vector3(r.rightSign * .260f, ready.y + .001f, r.HipNeutral.z + .205f);
        // The hilt comes inward before the point finishes realigning. There is no
        // below-waist blade loop or separate wrist flourish during the recovery.
        Vector3 retracted = new Vector3(r.rightSign * .160f, ready.y + .005f, r.HipNeutral.z + .170f);
        Vector3 hilt = CombatFlow(t, ready, inside, through, outside, retracted, ready);
        float yaw = r.rightSign * CombatFlow(t, 0, -48, 46, 59, 18, 0);
        float readyPitch = 30f;
        float pitch = readyPitch * (1 - S01(t / .11f)) + readyPitch * S01((t - .32f) / .33f);
        Vector3 blade = Quaternion.AngleAxis(yaw, Vector3.up) *
            new Vector3(0, Mathf.Sin(pitch * Mathf.Deg2Rad), Mathf.Cos(pitch * Mathf.Deg2Rad));
        // Keep the blade's broad face on one consistent frame. A changing arbitrary
        // plane normal previously added a roll that was unrelated to the cut.
        r.WeaponHand(true, hilt, blade, Vector3.up);
        r.WeaponHandFrame(true, blade, Vector3.up);
        float offhand = CombatFlow(t, 0, .35f, 1, .8f, .2f, 0);
        r.Hand(false, r.lHandNeutral + new Vector3(r.leftSign * .009f, .025f, .018f) * offhand); r.Fist(false);
        r.Gaze(); r.Grip(ChibiCylinderGripBuilder.SwordGripWeight, 100);
    }

    static float CombatLeadTime(float t)
    {
        float u = Mathf.Clamp01(t / .65f);
        return t + .018f * Mathf.Sin(Mathf.PI * u);
    }

    static void CompactStaffCast(Rig r, float t)
    {
        r.Body(t,
            x => CombatFlow(x, 0, .008f, .002f, .003f, .001f, 0),
            x => CombatFlow(x, 0, -.4f, 1.5f, 1, .3f, 0),
            x => -r.rightSign * CombatFlow(x, 0, -.8f, 3, 2.5f, .6f, 0),
            x => CombatFlow(x, 0, -.003f, .012f, .009f, .002f, 0));
        // Internal knots share velocity and acceleration. Charge, release and return
        // are one flowing gesture rather than three eased moves separated by a stop.
        Vector3 offset = CombatFlow(t, Vector3.zero, new Vector3(0, .012f, -.014f),
            new Vector3(-r.rightSign * .005f, .025f, .080f), new Vector3(-r.rightSign * .003f, .021f, .075f),
            new Vector3(0, .008f, .035f), Vector3.zero);
        float aim = CombatFlow(t, 0, .10f, 1, .94f, .38f, 0);
        float pitch = Mathf.LerpUnclamped(Mathf.PI * .25f, Mathf.Atan2(.38f, .925f), aim);
        Vector3 blade = new Vector3(0, Mathf.Sin(pitch), Mathf.Cos(pitch));
        Vector3 hilt = r.WeaponReadyHilt(true) + offset;
        r.WeaponHand(true, hilt, blade, Vector3.right);
        r.WeaponHandFrame(true, blade, Vector3.right);
        Vector3 support = CombatFlow(t, Vector3.zero, new Vector3(-r.leftSign * .002f, .012f, .010f),
            new Vector3(-r.leftSign * .006f, .033f, .038f), new Vector3(-r.leftSign * .005f, .027f, .032f),
            new Vector3(-r.leftSign * .002f, .009f, .010f), Vector3.zero);
        r.Hand(false, r.lHandNeutral + support); r.Fist(false);
        r.Gaze(); r.Grip(150, 100);
    }

    static readonly float[] CombatFlowTimes = { 0, .11f, .24f, .32f, .46f, .65f };

    static float CombatFlow(float t, float a, float b, float c, float d, float e, float f)
    {
        return CombatFlow(t, new Vector3(a, 0, 0), new Vector3(b, 0, 0), new Vector3(c, 0, 0),
            new Vector3(d, 0, 0), new Vector3(e, 0, 0), new Vector3(f, 0, 0)).x;
    }

    static Vector3 CombatFlow(float t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e, Vector3 f)
    {
        Vector3[] p = { a, b, c, d, e, f };
        if (t <= CombatFlowTimes[0]) return a;
        if (t >= CombatFlowTimes[5]) return f;
        int i = 0;
        while (i < 4 && t > CombatFlowTimes[i + 1]) i++;
        float dt = CombatFlowTimes[i + 1] - CombatFlowTimes[i];
        float u = (t - CombatFlowTimes[i]) / dt;
        Vector3 v0 = CombatFlowVelocity(p, i) * dt, v1 = CombatFlowVelocity(p, i + 1) * dt;
        Vector3 a0 = CombatFlowAcceleration(p, i) * (dt * dt), a1 = CombatFlowAcceleration(p, i + 1) * (dt * dt);
        Vector3 delta = p[i + 1] - p[i];
        Vector3 c3 = 10 * delta - 6 * v0 - 4 * v1 - 1.5f * a0 + .5f * a1;
        Vector3 c4 = -15 * delta + 8 * v0 + 7 * v1 + 1.5f * a0 - a1;
        Vector3 c5 = 6 * delta - 3 * v0 - 3 * v1 - .5f * a0 + .5f * a1;
        return p[i] + v0 * u + .5f * a0 * u * u + (c3 + (c4 + c5 * u) * u) * (u * u * u);
    }

    static Vector3 CombatFlowVelocity(Vector3[] p, int i)
    {
        if (i == 0 || i == 5) return Vector3.zero;
        return (p[i + 1] - p[i - 1]) / (CombatFlowTimes[i + 1] - CombatFlowTimes[i - 1]);
    }

    static Vector3 CombatFlowAcceleration(Vector3[] p, int i)
    {
        if (i == 0 || i == 5) return Vector3.zero;
        Vector3 before = (p[i] - p[i - 1]) / (CombatFlowTimes[i] - CombatFlowTimes[i - 1]);
        Vector3 after = (p[i + 1] - p[i]) / (CombatFlowTimes[i + 1] - CombatFlowTimes[i]);
        return 2 * (after - before) / (CombatFlowTimes[i + 1] - CombatFlowTimes[i - 1]);
    }

    static void CombatFeet(Rig r, float amount, float frontStep)
    {
        Quaternion roll = Quaternion.AngleAxis(4 * Mathf.Clamp01(amount), Vector3.right);
        Vector3 pivot = r.rearPivot;
        r.Plant(r.leftFootTarget + Vector3.forward * (frontStep * amount),
            pivot + roll * (r.rightFootTarget - pivot), r.lfRotation, roll * r.rfRotation);
    }
    static Vector3 CombatPath(float t, float[] times, Vector3[] positions)
    {
        if (t <= times[0]) return positions[0];
        for (int i = 0; i < times.Length - 1; i++)
            if (t <= times[i + 1]) return Vector3.LerpUnclamped(positions[i], positions[i + 1], S01(Mathf.InverseLerp(times[i], times[i + 1], t)));
        return positions[positions.Length - 1];
    }
}
#endif
