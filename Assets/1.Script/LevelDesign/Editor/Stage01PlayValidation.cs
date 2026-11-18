using UnityEditor;

/// <summary>Compatibility entry point. All play QA now uses normal device input.</summary>
public static class Stage01PlayValidation
{
    [MenuItem("OZ/Level Design/Run Stage 01 Play Validation")]
    public static void Start() => Stage01VerticalQA.Start();
}
