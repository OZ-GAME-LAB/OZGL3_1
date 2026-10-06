namespace OZ.UI.EditorTools
{
    /// <summary>02.parkhansol_ 폴더 경로 모음 (폴더를 옮기면 Root만 고치면 됨)</summary>
    internal static class OZPaths
    {
        public const string Root = "Assets/02.parkhansol_";

        public const string ImportZip = Root + "/_Import~/PixelUIHUD_UnityDemo.zip";
        public const string ThirdParty = Root + "/ThirdParty";
        public const string PixelUI = ThirdParty + "/PixelUIHUD";
        public const string SciFi = ThirdParty + "/SciFiPixelUI";
        public const string TextMeshPro = ThirdParty + "/TextMesh Pro";
        public const string Galmuri = ThirdParty + "/Fonts/Galmuri";

        public const string UI = Root + "/UI";
        public const string Fonts = UI + "/Fonts";
        public const string Data = UI + "/Data";
        public const string SampleData = Data + "/Samples";
        public const string Scenes = UI + "/Scenes";
        public const string SandboxScene = Scenes + "/UI_Sandbox.unity";
    }
}
