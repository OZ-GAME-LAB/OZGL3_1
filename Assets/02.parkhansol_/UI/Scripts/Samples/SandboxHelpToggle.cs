using UnityEngine;
using UnityEngine.InputSystem;

namespace OZ.UI.Samples
{
    /// <summary>Sandbox 도움말 F1 토글</summary>
    [AddComponentMenu("OZ/UI/Samples/Sandbox Help Toggle")]
    public class SandboxHelpToggle : MonoBehaviour
    {
        [SerializeField] internal GameObject target;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame && target != null)
                target.SetActive(!target.activeSelf);
        }
    }
}
