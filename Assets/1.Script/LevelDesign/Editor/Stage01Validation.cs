using UnityEditor;

/// <summary>Compatibility entry point for the current vertical stage asset checks.</summary>
public static class Stage01Validation
{
    [MenuItem("OZ/Level Design/Validate Stage 01")]
    public static void ValidateAndCapture() => Stage01VerticalAssetQA.Run();
}
