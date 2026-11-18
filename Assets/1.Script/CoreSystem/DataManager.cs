using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;

public class DataManager : BaseManager
{
    private readonly Dictionary<Type, object> dataTables = new Dictionary<Type, object>();
    private ResourceManager resourceManager;
    private bool loadPreloadData;
    private bool isInitializing;
    private bool destroyed;

    public bool IsReady { get; private set; }
    public int TableCount => dataTables.Count;
    public Exception InitializationError { get; private set; }

    public UniTask InitializeAsync(ResourceManager manager, bool includePreloadData = false)
    {
        RequireMainThread();
        if (manager == null) throw new ArgumentNullException(nameof(manager));
        if (resourceManager != null && resourceManager != manager)
            throw new InvalidOperationException("DataManager의 ResourceManager는 교체할 수 없습니다.");
        if (isInitializing) throw new InvalidOperationException("데이터 초기화가 이미 진행 중입니다.");
        if (IsReady && loadPreloadData != includePreloadData)
            throw new InvalidOperationException("초기화된 테이블 주소는 변경할 수 없습니다.");
        resourceManager = manager;
        loadPreloadData = includePreloadData;
        return InitializeAsync();
    }

    public override async UniTask InitializeAsync()
    {
        RequireMainThread();
        if (destroyed) throw new ObjectDisposedException(nameof(DataManager));
        if (resourceManager == null) throw new InvalidOperationException("ResourceManager를 먼저 주입하세요.");
        if (IsReady) return;
        if (isInitializing) throw new InvalidOperationException("데이터 초기화가 이미 진행 중입니다.");

        isInitializing = true;
        InitializationError = null;
        try
        {
            // 필수 테이블은 모두 성공해야 조회할 수 있습니다.
            // 새로운 데이터 종류는 이곳에 로드 호출을 추가합니다.
            await LoadDataTableAsync<MonsterData>(DataTableKey.MonsterData);
            await LoadDataTableAsync<BossMonsterData>(DataTableKey.BossMonsterData);
            await LoadDataTableAsync<ProjectileData>(DataTableKey.ProjectileData);
            if (loadPreloadData)
                await LoadDataTableAsync<PreloadData>(DataTableKey.PreloadData);
            await LoadDataTableAsync<PoolData>(DataTableKey.PoolData);
            await LoadDataTableAsync<StageMonsterData>(DataTableKey.StageMonsterData);

            if (destroyed) throw new ObjectDisposedException(nameof(DataManager));
            IsReady = true;
        }
        catch (Exception exception)
        {
            dataTables.Clear();
            IsReady = false;
            InitializationError = exception;
            Debug.LogError($"[DataManager] 초기화 실패: {exception.Message}");
            throw;
        }
        finally
        {
            isInitializing = false;
        }
    }

    private async UniTask LoadDataTableAsync<T>(DataTableKey tableKey) where T : BaseData
    {
        string key = tableKey.ToString();
        try
        {
            if (dataTables.ContainsKey(typeof(T)))
                throw new InvalidOperationException($"{typeof(T).Name} 테이블이 이미 저장되어 있습니다.");
            TextAsset asset = await resourceManager.LoadAssetAsync<TextAsset>(key);
            if (destroyed) throw new ObjectDisposedException(nameof(DataManager));
            if (asset == null) throw new InvalidDataException("TextAsset을 불러오지 못했습니다.");
            dataTables.Add(typeof(T), ParseTable<T>(asset.text));
            // 원본 에셋과 핸들은 ResourceManager의 수명 관리 정책에 따라 해제합니다.
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"JSON '{key}' 로드 실패: {exception.Message}", exception);
        }
    }

    internal static IReadOnlyDictionary<string, T> ParseTable<T>(string json) where T : BaseData
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("JSON 내용이 비어 있습니다.");
        List<T> rows = JsonConvert.DeserializeObject<List<T>>(json);
        if (rows == null) throw new InvalidDataException("JSON 역직렬화 결과가 null입니다.");
        var table = new Dictionary<string, T>(StringComparer.Ordinal);
        for (int i = 0; i < rows.Count; i++)
        {
            T data = rows[i];
            if (data == null) throw new InvalidDataException($"{i}번 행 역직렬화 결과가 null입니다.");
            if (string.IsNullOrWhiteSpace(data.Id)) throw new InvalidDataException($"{i}번 행의 ID 변환 결과가 비어 있습니다.");
            if (table.ContainsKey(data.Id)) throw new InvalidDataException($"중복 ID: '{data.Id}'");
            table.Add(data.Id, data);
        }
        return new ReadOnlyDictionary<string, T>(table);
    }

    public bool TryGetData<T>(string id, out T data) where T : BaseData
    {
        data = null;
        return id != null && TryGetDataTable<T>(out var table) && table.TryGetValue(id, out data);
    }

    public bool TryGetDataTable<T>(out IReadOnlyDictionary<string, T> table) where T : BaseData
    {
        RequireMainThread();
        table = null;
        if (!IsReady || !dataTables.TryGetValue(typeof(T), out object value)) return false;
        table = (IReadOnlyDictionary<string, T>)value;
        return true;
    }

    private static void RequireMainThread()
    {
        if (!PlayerLoopHelper.IsMainThread)
            throw new InvalidOperationException("DataManager API는 Unity 메인 스레드에서 호출하세요.");
    }

    private void OnDestroy()
    {
        destroyed = true;
        IsReady = false;
        dataTables.Clear();
    }
}
