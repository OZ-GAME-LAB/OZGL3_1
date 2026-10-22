using UnityEngine;

namespace OZ.UI
{
    /// <summary>상태창 탭 하나의 내용. 상속해서 Refresh만 채우면 된다.</summary>
    public abstract class StatusPage : MonoBehaviour
    {
        internal StatusWindow Owner { get; set; }
        /// <summary>아래 조작 안내 (탭마다)</summary>
        internal virtual string Hint => "";
        /// <summary>머리 재화 중 이 탭이 쓰는 쪽을 크게 (true = 아이템 포인트, false = 성장 포인트)</summary>
        internal virtual bool UsesItemPoints => false;
        internal virtual void OnShow() { }
        internal virtual void OnHide() { }
        internal virtual void Focus() { }
        internal abstract void Refresh();
    }
}
