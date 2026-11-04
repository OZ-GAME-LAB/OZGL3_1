using System.Collections;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [박한솔 UI · v1.0] 타이틀 '이어하기' 연결. 팀원 코드(SceneGameLink · GameManager · PlayerController) 수정 없이 동작한다.
///   버튼: 저장 파일(GameSave)이 있으면 켜짐 (UIContinue.HasSave)
///   누르면: 저장된 직업으로 SceneGameLink.StartGame → 1스테이지 시작 →
///           플레이어 켜진 다음 프레임에 연결된 것만 불러옴 (스탯·스킬 / 성장 포인트 / 아이템 포인트·포션) → 체력 가득
///   저장 파일이 깨졌거나 없으면: 안내 토스트 + 타이틀로
/// 씬에 SceneGameLink가 있으면 Play 시 자동으로 생긴다 (씬·프리팹 수정 불필요).
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Continue Bridge")]
public class ContinueBridge : MonoBehaviour
{
    GameSave.Data _pending;
    GameManager _gm;

    void OnEnable()
    {
        UIContinue.HasSave = () => GameSave.HasSave;
        UIRequests.Continue += OnContinue;
    }

    void OnDisable()
    {
        UIRequests.Continue -= OnContinue;
        if (_gm != null) _gm.StageStarted -= OnStageStarted;
        _gm = null;
    }

    void OnContinue()
    {
        var link = FindAnyObjectByType<SceneGameLink>();
        var gm = GameManager.Instance;
        if (link == null || gm == null || !GameSave.TryLoad(out var data))
        {
            GameUI.Notify.Toast("저장된 진행을 불러오지 못했다", ToastType.Warning);
            GameUI.Screens.ShowTitle(false);
            return;
        }
        if (_gm != gm)
        {
            if (_gm != null) _gm.StageStarted -= OnStageStarted;
            _gm = gm;
            _gm.StageStarted += OnStageStarted;
        }
        _pending = data;
        link.StartGame(data.playerClass); // 직업 표시 + 1스테이지 시작 (팀원 공개 함수)
    }

    void OnStageStarted()
    {
        if (_pending == null) return;
        StartCoroutine(ApplyNextFrame(_pending)); // 플레이어가 켜지고(Awake) 난 뒤에 넣는다
        _pending = null;
    }

    IEnumerator ApplyNextFrame(GameSave.Data d)
    {
        yield return null;
        Apply(d);
    }

    internal static void Apply(GameSave.Data d)
    {
        var player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player != null && d.player != null) player.LoadSaveData(d.player);
        var growth = FindAnyObjectByType<GrowthShopBridge>(FindObjectsInactive.Include);
        if (growth != null && d.growth != null) growth.LoadSaveData(d.growth);
        var items = FindAnyObjectByType<TempPotionWallet>(FindObjectsInactive.Include);
        if (items != null && d.items != null) items.LoadSaveData(d.items);
        // 체력 스탯으로 늘어난 최대 HP까지 채움 (정명진 LoadSaveData 주석: 이후 ResetHealth)
        var health = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (health != null && health.isActiveAndEnabled) health.ResetHealth(); // 꺼진 플레이어는 켜질 때 SceneGameLink가 채움
    }

    [ContextMenu("테스트/지금 저장")]
    void TestSave() => Debug.Log(GameSave.Save() ? "[OZ UI] 저장: " + GameSave.FilePath : "[OZ UI] 저장 실패");

    [ContextMenu("테스트/저장 파일 삭제")]
    void TestDelete() { GameSave.Delete(); Debug.Log("[OZ UI] 저장 파일 삭제"); }

    // ── 자동 설치 ──
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstall()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall();

    static void TryInstall()
    {
        if (FindAnyObjectByType<ContinueBridge>(FindObjectsInactive.Include) != null) return;
        if (FindAnyObjectByType<SceneGameLink>(FindObjectsInactive.Include) == null) return;
        new GameObject("ContinueBridge (auto, UI)").AddComponent<ContinueBridge>(); // Play 중에만 생김
    }
}