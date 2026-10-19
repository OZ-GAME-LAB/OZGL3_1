using UnityEngine;

namespace OZ.UI
{
    /// <summary>상태창 탭 하나의 내용. 상속해서 Refresh만 채우면 된다.</summary>
    public abstract class StatusPage : MonoBehaviour
    {
        internal StatusWindow Owner { get; set; }
        /// <summary>아래 조작 안내 (탭마다)</summary>
        internal virtual string Hint => "";
        internal virtual void OnShow() { }
        internal virtual void OnHide() { }
        internal virtual void Focus() { }
        internal abstract void Refresh();
    }
}
