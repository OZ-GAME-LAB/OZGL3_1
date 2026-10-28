using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v1.0] 타이틀 '이어하기' 버튼 활성 여부. 저장 담당이 "저장 파일이 있나?"만 알려주면 된다.
    ///   UIContinue.HasSave = () => File.Exists(path);
    /// ShowTitle(canContinue)로 넘긴 값과 OR — 둘 중 하나라도 true면 버튼이 켜진다.
    /// '이어하기'를 누르면 UIRequests.Continue가 발생한다 (불러오기는 그쪽 담당).
    /// </summary>
    public static class UIContinue
    {
        public static Func<bool> HasSave;

        internal static bool Available
        {
            get
            {
                try { return HasSave != null && HasSave(); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); return false; }
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => HasSave = null;
    }
}