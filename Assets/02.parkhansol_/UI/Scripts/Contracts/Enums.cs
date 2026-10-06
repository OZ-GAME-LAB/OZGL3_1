namespace OZ.UI.Contracts
{
    /// <summary>플레이 계열 (기획서 v0.2: 검술 / 마법)</summary>
    public enum PlayerClass
    {
        Sword = 0,
        Magic = 1,
    }

    /// <summary>액티브 스킬 고정 슬롯 (Q/E/R)</summary>
    public enum SkillSlot
    {
        Q = 0,
        E = 1,
        R = 2,
    }

    /// <summary>헌터 랭크. 데모는 F → E → D 까지 사용.</summary>
    public enum HunterRank
    {
        F = 0,
        E = 1,
        D = 2,
        C = 3,
        B = 4,
        A = 5,
        S = 6,
    }

    /// <summary>UI가 여닫는 화면 ID</summary>
    public enum ScreenId
    {
        None = 0,
        Title = 1,
        ClassSelect = 2,
        SkillWindow = 3,
        Pause = 4,
        Death = 5,
        StageClear = 6,
        DemoEnd = 7,
        Inventory = 8,
        Map = 9,
        Dialogue = 10,
    }

    public enum ToastType
    {
        Info = 0,
        Success = 1,
        Warning = 2,
    }

    /// <summary>스킬 사용 실패 사유 (HUD 흔들림/색 피드백용)</summary>
    public enum SkillUseFailReason
    {
        NotLearned = 0,
        Cooldown = 1,
        Blocked = 2,
    }

    public enum ItemEffectType
    {
        Heal = 0,
        AttackBuff = 1,
        DefenseBuff = 2,
        SpeedBuff = 3,
        KeyItem = 4, // 사용 불가(퀘스트·열쇠 등) — 인벤토리 표시용
    }

    /// <summary>대화 초상화 위치</summary>
    public enum DialogueSide
    {
        Left = 0,
        Right = 1,
    }

    /// <summary>지도 방 아이콘</summary>
    public enum MapRoomIcon
    {
        None = 0,
        Start = 1,
        Gate = 2,
        Boss = 3,
        Save = 4,
        Item = 5,
    }

    public static class HunterRankExtensions
    {
        /// <summary>"F급" 형태의 표시 문자열</summary>
        public static string ToDisplay(this HunterRank rank) => rank + "급";
    }
}
