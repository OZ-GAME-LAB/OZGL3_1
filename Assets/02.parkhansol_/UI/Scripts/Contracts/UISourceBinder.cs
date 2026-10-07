using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// 코드 없이 연결하는 방법: 플레이어(또는 스테이지 매니저) 오브젝트에 이 컴포넌트를 붙이면
    /// 같은 오브젝트의 컴포넌트 중 IHealthSource / IProgressionSource / ISkillSource /
    /// IItemSource / IGateSource / IInventorySource / ISkillTreeSource 를 구현한 것을 찾아 활성화 시 등록, 비활성화 시 해제한다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/UI Source Binder")]
    public class UISourceBinder : MonoBehaviour
    {
        [Tooltip("자식 오브젝트의 컴포넌트까지 찾기")]
        [SerializeField] bool includeChildren = false;

        MonoBehaviour[] _bound;

        void OnEnable()
        {
            _bound = includeChildren
                ? GetComponentsInChildren<MonoBehaviour>(true)
                : GetComponents<MonoBehaviour>();

            foreach (var mb in _bound)
            {
                if (mb == null || mb == this) continue;
                UISources.Bind(mb);
            }
        }

        void OnDisable()
        {
            if (_bound == null) return;
            foreach (var mb in _bound)
            {
                if (mb == null || mb == this) continue;
                UISources.Unbind(mb);
            }
            _bound = null;
        }
    }
}
