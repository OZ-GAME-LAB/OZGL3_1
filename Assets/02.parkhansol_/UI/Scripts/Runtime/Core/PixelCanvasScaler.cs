using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 픽셀 UI용 정수배 스케일러.
    /// 논리 해상도 640×360 기준으로 화면에 들어가는 가장 큰 정수배를 적용한다.
    ///   1920×1080 → 3배 / 2560×1440 → 4배 / 1280×720, 1366×768 → 2배
    /// 에셋 1px이 화면 N px로 번짐 없이 보인다. 좌표 환산: 화면 px ÷ 배율 = UI 좌표.
    /// </summary>
    [AddComponentMenu("OZ/UI/Pixel Canvas Scaler")]
    [RequireComponent(typeof(Canvas))]
    public class PixelCanvasScaler : CanvasScaler
    {
        [Tooltip("UI 논리 해상도")]
        [SerializeField] Vector2Int logicalResolution = new Vector2Int(640, 360);
        [Tooltip("최소 배율")]
        [SerializeField, Min(1)] int minScale = 1;

        public Vector2Int LogicalResolution => logicalResolution;
        public int CurrentScale { get; private set; } = 1;

        public void SetLogicalResolution(Vector2Int resolution)
        {
            logicalResolution = new Vector2Int(Mathf.Max(1, resolution.x), Mathf.Max(1, resolution.y));
        }

        /// <summary>화면 크기 → 정수 배율 (테스트용으로 분리)</summary>
        public static int ComputeScale(int screenW, int screenH, Vector2Int logical, int min = 1)
        {
            int sx = Mathf.Max(1, screenW) / Mathf.Max(1, logical.x);
            int sy = Mathf.Max(1, screenH) / Mathf.Max(1, logical.y);
            return Mathf.Max(min, Mathf.Min(sx, sy));
        }

        protected override void Handle()
        {
            int scale = ComputeScale(Screen.width, Screen.height, logicalResolution, minScale);

            CurrentScale = scale;
            SetScaleFactor(scale);
            SetReferencePixelsPerUnit(m_ReferencePixelsPerUnit);
        }

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            uiScaleMode = ScaleMode.ConstantPixelSize;
            referencePixelsPerUnit = 100f; // 에셋 스프라이트 PPU 100
        }
#endif
    }
}
