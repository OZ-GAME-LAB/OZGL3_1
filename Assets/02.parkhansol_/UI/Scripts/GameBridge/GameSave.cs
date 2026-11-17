using System;
using System.IO;
using OZ.UI.Contracts;
using UnityEngine;

/// <summary>
/// [박한솔 UI · v1.0] 이어하기용 저장 파일 한 개 (임시 — 코어에 SaveManager가 생기면 그쪽으로 옮기면 됨).
///   "연결된 것"만 담는다:
///     플레이어  : PlayerController.GetSaveData() (스탯 레벨 · 스킬 보유/장착 — 정명진)
///     직업      : PlayerUILink.Class
///     성장 지갑 : GrowthShopBridge (성장 포인트 · 받은 퀘스트 보상)
///     아이템 지갑: TempPotionWallet (아이템 포인트 · 포션 1~4 · 장비)
///   스테이지  : v1.5 stageNumber (이어하기 때 시작할 스테이지, 0 = 기록 없음 → 1스테이지)
///
/// 저장 시점은 저장 담당이 정한다 — 원하는 곳에서 한 줄:
///   GameSave.Save();              // 스테이지 번호는 이전 저장값 유지
///   GameSave.Save(stageNumber);   // v1.5 스테이지 번호까지 같이 (SaveManager '교체 지점 A')
/// 이어하기(불러오기)는 ContinueBridge가 처리한다.
/// </summary>
public static class GameSave
{
    public const int Version = 1;
    const string FileName = "oz_save.json";

    [Serializable]
    public class Data
    {
        public int version = Version;
        public string savedAt;
        public int playerClass;
        [Tooltip("v1.5 이어하기 때 시작할 스테이지 (0 = 기록 없음 → 1스테이지)")]
        public int stageNumber;
        public PlayerSaveData player;
        public GrowthShopBridge.SaveData growth;
        public TempPotionWallet.SaveData items;
    }

    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);
    public static bool HasSave => File.Exists(FilePath);

    /// <summary>지금 상태를 저장. 연결 안 된 부분(예: 지갑 없음)은 비워 둔다. 스테이지 번호는 이전 저장값을 그대로 둔다</summary>
    public static bool Save() => Save(-1);

    /// <summary>v1.5 스테이지 번호까지 저장 (1~). 0 이하면 이전 저장값 유지</summary>
    public static bool Save(int stageNumber)
    {
        if (stageNumber <= 0) stageNumber = TryLoad(out var prev) ? prev.stageNumber : 0;
        try
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
            var ui = UnityEngine.Object.FindAnyObjectByType<PlayerUILink>(FindObjectsInactive.Include);
            var growth = UnityEngine.Object.FindAnyObjectByType<GrowthShopBridge>(FindObjectsInactive.Include);
            var items = UnityEngine.Object.FindAnyObjectByType<TempPotionWallet>(FindObjectsInactive.Include);
            var d = new Data
            {
                savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                playerClass = ui != null ? (int)ui.Class : 0,
                stageNumber = Mathf.Max(0, stageNumber),
                player = player != null ? player.GetSaveData() : null,
                growth = growth != null ? growth.GetSaveData() : null,
                items = items != null ? items.GetSaveData() : null,
            };
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(d, true));
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(tmp, FilePath); // 쓰다가 꺼져도 기존 파일이 깨지지 않게
            return true;
        }
        catch (Exception e) { Debug.LogException(e); return false; }
    }

    public static bool TryLoad(out Data data)
    {
        data = null;
        try
        {
            if (!HasSave) return false;
            data = JsonUtility.FromJson<Data>(File.ReadAllText(FilePath));
            if (data == null || data.version > Version) { data = null; return false; }
            return true;
        }
        catch (Exception e) { Debug.LogException(e); data = null; return false; }
    }

    /// <summary>v1.5 저장된 시작 스테이지 (없거나 기록 전 파일이면 1)</summary>
    public static int SavedStage => TryLoad(out var d) && d.stageNumber >= 1 ? d.stageNumber : 1;

    public static void Delete()
    {
        try { if (HasSave) File.Delete(FilePath); } catch (Exception e) { Debug.LogException(e); }
    }
}