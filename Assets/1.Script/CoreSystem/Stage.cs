using System.Collections.Generic;
using UnityEngine;

// 스테이지 프리팹 루트에 추가하고 자식 게이트들을 인스펙터에서 연결합니다.
public class Stage : MonoBehaviour
{
    [SerializeField] private List<StageGate> gates = new List<StageGate>();

    public IReadOnlyList<StageGate> Gates => gates.AsReadOnly();
}
