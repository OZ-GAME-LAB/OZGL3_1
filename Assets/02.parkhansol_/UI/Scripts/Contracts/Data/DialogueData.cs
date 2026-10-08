using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>대화 화자 (이름 + 초상화)</summary>
    [CreateAssetMenu(menuName = "OZ/UI/Speaker Data", fileName = "Speaker_")]
    public class SpeakerData : ScriptableObject
    {
        public string id;
        public string displayName;
        public Color nameColor = Color.white;
        public Sprite portrait;
        [Tooltip("표정별 초상화 (선택). DialogueLine.expression 인덱스")]
        public Sprite[] expressions;

        public Sprite GetPortrait(int expression)
        {
            if (expressions != null && expression >= 0 && expression < expressions.Length && expressions[expression] != null)
                return expressions[expression];
            return portrait;
        }
    }

    /// <summary>
    /// 대화 한 묶음. 기획/팀원이 줄만 채우면 GameUI.Dialogue.Play(data) 로 재생.
    /// </summary>
    [CreateAssetMenu(menuName = "OZ/UI/Dialogue Data", fileName = "Dialogue_")]
    public class DialogueData : ScriptableObject
    {
        public string id;
        public List<DialogueLine> lines = new List<DialogueLine>();
        [Tooltip("재생 중 게임 일시정지")]
        public bool pauseGame = true;
    }

    [Serializable]
    public class DialogueLine
    {
        public SpeakerData speaker;
        public DialogueSide side;
        [Tooltip("SpeakerData.expressions 인덱스, -1이면 기본")]
        public int expression = -1;
        [TextArea(2, 5)] public string text;
        [Tooltip("초당 글자 수. 0이면 기본값")]
        public float charsPerSecond;
    }
}
