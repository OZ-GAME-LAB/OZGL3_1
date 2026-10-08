using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>씬에 남겨 두는 안내 메모 (인스펙터에서 읽기용, 실행에는 영향 없음)</summary>
    [AddComponentMenu("OZ/UI/Links/UI Note")]
    public class UINote : MonoBehaviour
    {
        [TextArea(6, 30)] public string note;
    }
}
