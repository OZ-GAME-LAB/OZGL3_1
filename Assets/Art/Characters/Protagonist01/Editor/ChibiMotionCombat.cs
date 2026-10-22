#if UNITY_EDITOR
public static partial class ChibiMotionSet
{
    // The three attack clips were explicitly retired. Weapon model/prefab assets remain.
    // Hit_Reaction belongs to UtilitySet and is retained.
    public static Motion[] CombatSet() => new Motion[0];
}
#endif
