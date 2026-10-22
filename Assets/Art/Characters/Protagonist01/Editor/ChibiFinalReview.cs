#if UNITY_EDITOR
/// <summary>Offline art migration and review only; never connects gameplay.</summary>
public static class ChibiFinalReview
{
    public static void MigrateAndReviewWeapons()
    {
        ChibiGaitSourceMigration.Prepare();
        ChibiMotionSet.ReviewWeaponsOnly();
    }
}
#endif
