using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>UISound → 소리 묶음. 같은 종류에 여러 개 넣으면 무작위로 재생</summary>
    [CreateAssetMenu(menuName = "OZ/UI/UI Sound Set", fileName = "UISoundSet")]
    public class UISoundSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public UISound sound;
            public AudioClip[] clips = new AudioClip[0];
            [Range(0f, 1.5f)] public float volume = 0.7f;
            [Tooltip("재생마다 음높이를 ± 이만큼 흔듦 (반복 피로 줄이기)")]
            [Range(0f, 0.2f)] public float pitchJitter = 0.03f;
            [Tooltip("같은 소리를 이 시간 안에 다시 부르면 무시")]
            [Range(0f, 1f)] public float cooldown = 0.04f;
            [Tooltip("출처 메모 (라이선스 확인용)")] public string note;
        }

        public Entry[] entries = new Entry[0];

        public Entry Find(UISound s)
        {
            foreach (var e in entries) if (e != null && e.sound == s) return e;
            return null;
        }
    }
}
