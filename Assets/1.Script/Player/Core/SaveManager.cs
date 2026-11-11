using System.Collections;
using System.IO;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;



//  저장 파일 (둘 다 Application.persistentDataPath 폴더)
//    oz_save.json     : 스탯·스킬·무기·포인트·포션  (한솔님 GameSave)
//    oz_progress.json : 이어하기 시작 스테이지 번호
[DisallowMultipleComponent] // 한 오브젝트에 두 개 붙으면 저장·스테이지 이동이 두 번 일어나서 막음
public class SaveManager : MonoBehaviour
{
    // 추후 교체 지점 A
    //   GameSave.Data에 스테이지 칸이 없어서 스테이지 번호만 별도 파일로 저장
    //   GameSave에 stageNumber를 추가하면 이 파일 관련 코드는 지우면 됨
    private const string ProgressFileName = "oz_progress.json";
    private static string ProgressPath => Path.Combine(Application.persistentDataPath, ProgressFileName);

    [System.Serializable]
    private class ProgressData
    {
        public int resumeStage = 1;
    }

    // 스테이지 옮길 때 화면 어두워지는 시간
    private const float SwitchFade = 0.35f;

    private GameManager _gameManager;
    private StageManager _stageManager;

    // 이어하기 때 시작할 스테이지 번호
    //   0  = 이번 실행에서 아직 게임을 안 함 (저장하면 안 되는 상태)
    //   1~ = 저장할 스테이지 번호
    private int _resumeStage;

    private void Start()
    {
        //   GameManager.Instance는 GameManager.Awake에서 정해지는데,
        //   Awake끼리는 실행 순서가 보장되지 않아서 null일 수 있음
        _gameManager = GameManager.Instance;
        if (_gameManager == null)
        {
            Debug.LogError("[SaveManager] GameManager를 찾을 수 없습니다.");
            enabled = false; // 아래 코드가 전부 GameManager를 쓰니까 아예 꺼 둠
            return;
        }

        _stageManager = _gameManager.StageManager;

        _gameManager.StageStarted += OnStageStarted;                            // 스테이지 들어옴
        if (_stageManager != null) _stageManager.StageCleared += OnStageCleared; // 스테이지 클리어
        UIRequests.ReturnToTitle += OnReturnToTitle;                            // '타이틀로' 버튼
        UIRequests.Continue += OnContinue;                                      // '이어하기' 버튼
    }

    private void OnDestroy()
    {
        // 구독한 건 반드시 해제 -> 안 하면 씬이 바뀐 뒤에도 사라진 객체를 부르다 에러남
        if (_gameManager != null) _gameManager.StageStarted -= OnStageStarted;
        if (_stageManager != null) _stageManager.StageCleared -= OnStageCleared;
        UIRequests.ReturnToTitle -= OnReturnToTitle;
        UIRequests.Continue -= OnContinue;
    }

    //  저장
    // 새 게임 · 재도전 · 다음 스테이지 · 이어하기 모두 이 이벤트를 지나감
    // -> "지금 들어온 스테이지부터 다시 하면 된다"로 기록만 해 둠 (저장은 아직 안 함)
    private void OnStageStarted()
    {
        _resumeStage = _gameManager.StageNumber;
    }

    // 클리어 = 다음 스테이지가 열린 순간 -> 바로 저장해야 여기서 꺼도 진행이 남음
    private void OnStageCleared()
    {
        int next = _stageManager.StageNumber + 1;

        // 마지막 스테이지까지 깼으면 "다음"이 없으니 마지막 스테이지로 둠
        // (HasStage로 확인 -> 없는 번호를 저장해 두면 이어하기 때 시작을 못 함)
        _resumeStage = _stageManager.HasStage(next) ? next : _stageManager.StageNumber;
        SaveNow();
    }

    //   상점에서 산 스탯·스킬·무기는 스테이지 진행과 상관없이 남아야 하니까
    //   (스테이지는 _resumeStage 그대로 -> 이어하면 그 스테이지를 처음부터, 재도전과 같은 규칙)
    private void OnReturnToTitle() => SaveNow();

    // 창 닫기 · 종료 버튼 · 에디터 Play 중지 때 Unity가 자동으로 불러 줌
    private void OnApplicationQuit() => SaveNow();

    private void SaveNow()
    {
        // 이번 실행에서 게임을 한 번도 안 했으면 저장 안 함
        // -> 켜자마자 타이틀에서 끄면, 아무것도 없는 플레이어 정보로
        //    기존 저장 파일을 덮어써서 진행이 날아가는 걸 막음
        if (_resumeStage <= 0) return;

        // 스탯 · 스킬 · 무기 · 포인트 · 포션 -> 한솔님 GameSave가 oz_save.json으로 저장
        if (!GameSave.Save())
        {
            Debug.LogWarning("[SaveManager] 저장에 실패했습니다.");
            // 본 저장이 실패했는데 스테이지 번호만 바뀌면
            // "스테이지는 2인데 스탯은 옛날 것" 처럼 둘이 어긋나니까 여기서 멈춤
            return;
        }

        // 추후 교체 지점 A
        // GameSave에 스테이지 칸이 생기면 -> 위를 GameSave.Save(_resumeStage)로 바꾸고 아래 줄 삭제
        WriteResumeStage(_resumeStage);
    }

    //  스테이지 번호 파일 (oz_progress.json)
    private static void WriteResumeStage(int stage)
    {
        try
        {
            var data = new ProgressData { resumeStage = stage };

            // 임시 파일에 먼저 쓰고 바꿔치기
            // -> 쓰는 도중 꺼져도 기존 파일이 반쯤 쓰인 채로 깨지지 않음
            string tmp = ProgressPath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data));
            if (File.Exists(ProgressPath)) File.Delete(ProgressPath);
            File.Move(tmp, ProgressPath);
        }
        catch (System.Exception e)
        {
            // 파일 쓰기 실패로 게임이 멈추면 안 되니 로그만 남김
            Debug.LogException(e);
        }
    }

    // 없거나 깨졌으면 1스테이지 -> "처음 스테이지부터"로 안전하게 시작
    private static int ReadResumeStage()
    {
        try
        {
            if (!File.Exists(ProgressPath)) return 1;
            var data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(ProgressPath));
            return data != null && data.resumeStage >= 1 ? data.resumeStage : 1;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            return 1;
        }
    }

    //  이어하기
    private void OnContinue()
    {
        // 저장 파일이 없으면 ContinueBridge도 시작을 안 하니까 할 일 없음
        if (!GameSave.HasSave) return;
        StartCoroutine(MoveToSavedStage());
    }

    private IEnumerator MoveToSavedStage()
    {
        // 2프레임 기다리는 이유
        //   1프레임째 : ContinueBridge의 1스테이지 시작이 확실히 끝나도록
        //               (이벤트 구독 순서상 ContinueBridge가 먼저일지 우리가 먼저일지 몰라서
        //                순서에 상관없이 동작하게 하려고)
        //   2프레임째 : ContinueBridge가 다음 프레임에 넣는 플레이어 데이터(LoadSaveData)가
        //               먼저 들어간 뒤에 스테이지를 옮기도록
        yield return null;
        yield return null;

        // 저장 파일이 깨져서 ContinueBridge가 타이틀로 돌려보냈으면 스테이지가 안 돌고 있음
        // -> 그 상태에서 스테이지를 옮기면 타이틀 위에서 게임이 시작돼 버리니 중단
        if (!_gameManager.IsStageRunning) yield break;

        //  추후 교체 지점 A : GameSave에 칸이 생기면 아래 줄을
        // int saved = GameSave.TryLoad(out var d) ? d.stageNumber : 1;  로 교체
        int saved = ReadResumeStage();

        if (saved <= 1 || saved == _gameManager.StageNumber) yield break; // 이미 맞는 스테이지면 그대로
        if (!_stageManager.CanSetStage(saved)) yield break;               // 없는 스테이지 번호면 1스테이지 유지

        // 화면이 어두운 동안 옮겨서 스테이지가 바뀌는 게 덜 보이게
        GameUI.Flow.Transition(() =>
        {
            // 추후 교체 지점 B
            //   GameManager.SetStage(n)가 private이라 공개 함수 두 개를 이어서 같은 효과를 냄
            //     1) StageManager.SetStage(n) : 스테이지 번호를 n으로 바꿈 (이벤트는 안 나감)
            //     2) GameManager.StartStage() : "지금 번호"로 다시 시작 + StageChanging/StageStarted 이벤트
            //        -> 플레이어 위치 · HUD 게이트 수 · 체력이 정식 흐름대로 맞춰짐
            //  "N스테이지 바로 시작" 공개 함수를 만들면 이 두 줄을 그 함수 한 줄로 교체
            if (_stageManager.SetStage(saved))
                _gameManager.StartStage();
        }, SwitchFade);
    }

    //  테스트용 : 저장 전부 삭제
    [ContextMenu("테스트/저장 전부 삭제")]
    private void TestDeleteAll()
    {
        GameSave.Delete();

        try
        {
            if (File.Exists(ProgressPath)) File.Delete(ProgressPath); // oz_progress.json
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }

        // 지운 직후 Play를 멈추면 종료 저장(OnApplicationQuit)이 파일을 다시 만들어서
        // "이번 실행에선 게임 안 함" 상태로 돌려 저장이 안 되게 막음
        _resumeStage = 0;

        Debug.Log("[SaveManager] 저장 전부 삭제 : " + Application.persistentDataPath);
    }

    //  자동 설치
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] // 첫 씬 로드 직후 Unity가 자동 호출
    private static void AutoInstall()
    {
        // 혹시 두 번 불려도 구독이 중복되지 않게
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryInstall();
    }

    // 나중에 씬이 바뀌어도 그 씬에 맞게 다시 설치
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall();

    private static void TryInstall()
    {
        // 이미 있으면 안 만듦 -> 씬 전체에서 하나만 존재
        // ([DisallowMultipleComponent]는 같은 오브젝트 안의 중복만 막아서 이 체크가 따로 필요)
        if (FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include) != null) return;

        // GameManager가 없는 씬(테스트 씬 등)에서는 저장할 게 없으니 안 만듦
        if (FindAnyObjectByType<GameManager>(FindObjectsInactive.Include) == null) return;

        new GameObject("SaveManager (auto)").AddComponent<SaveManager>();
    }
}
