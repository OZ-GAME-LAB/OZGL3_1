namespace OZ.UI
{
    /// <summary>레이어별 Canvas sortingOrder. 값이 클수록 위에 그려진다.</summary>
    public enum UILayer
    {
        HUD = 0,
        Screen = 10,     // 스킬 창, 일시정지, 계열 선택
        Popup = 20,      // 확인 팝업, 툴팁
        Cinematic = 30,  // 보스 등장 레터박스/배너
        Fullscreen = 40, // 사망, 타이틀, 데모 종료
        Modal = 45,      // 옵션 창 (타이틀·일시정지 위에 뜸)
        Toast = 50,
        Overlay = 100,   // 화면 플래시, 페이드
    }
}
