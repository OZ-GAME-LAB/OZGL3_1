using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

public class ResourceManager : BaseManager
{
    // 한 주소에는 한 타입만 허용합니다. 주소 별칭/label의 동일성은 합치지 않습니다.
    private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private UniTaskCompletionSource initialization;
    private bool ready;
    private bool disposed;

    private sealed class Entry
    {
        public Type Type;
        public Object Asset;
        public AsyncOperationHandle<Object> Handle;
        public readonly UniTaskCompletionSource<Object> Completion = new UniTaskCompletionSource<Object>();
    }

    public readonly struct PreloadRequest
    {
        public string Key { get; }
        public Type AssetType { get; }
        public PreloadRequest(string key, Type assetType) { Key = key; AssetType = assetType; }
        public static PreloadRequest For<T>(string key) where T : Object => new PreloadRequest(key, typeof(T));
    }

    public int CachedCount => entries.Count;

    public override UniTask InitializeAsync()
    {
        RequireMainThread();
        if (disposed) throw new ObjectDisposedException(nameof(ResourceManager));
        if (ready) return UniTask.CompletedTask;
        if (initialization == null)
        {
            var completion = new UniTaskCompletionSource();
            initialization = completion;
            InitializeAddressablesAsync(completion).Forget();
            // 즉시 실패하면 재시도를 위해 initialization이 null로 돌아갈 수 있습니다.
            return completion.Task;
        }
        return initialization.Task;
    }

    private async UniTask InitializeAddressablesAsync(UniTaskCompletionSource completion)
    {
        AsyncOperationHandle<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator> handle = default;
        try
        {
            handle = Addressables.InitializeAsync(false);
            await handle.ToUniTask();
            if (disposed) throw new ObjectDisposedException(nameof(ResourceManager));
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw handle.OperationException ?? new InvalidOperationException("Addressables 초기화 실패");
            ready = true;
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            initialization = null;
            Debug.LogError($"[ResourceManager] 초기화 실패: {exception.Message}");
            completion.TrySetException(exception);
        }
        finally
        {
            if (handle.IsValid()) Addressables.Release(handle);
        }
    }

    public async UniTask<T> LoadAssetAsync<T>(string key) where T : Object
    {
        Entry entry = GetOrLoad(key, typeof(T));
        return (T)await entry.Completion.Task;
    }

    public bool TryGetAsset<T>(string key, out T asset) where T : Object
    {
        RequireMainThread();
        asset = null;
        if (key == null || !entries.TryGetValue(key, out Entry entry) ||
            entry.Type != typeof(T) || entry.Asset == null) return false;
        asset = entry.Asset as T;
        return asset != null;
    }

    private Entry GetOrLoad(string key, Type type)
    {
        RequireMainThread();
        if (disposed) throw new ObjectDisposedException(nameof(ResourceManager));
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("에셋 주소가 비어 있습니다.", nameof(key));
        if (type == null || !typeof(Object).IsAssignableFrom(type))
            throw new ArgumentException("UnityEngine.Object 타입을 지정하세요.", nameof(type));
        if (entries.TryGetValue(key, out Entry existing))
        {
            if (existing.Type != type)
                throw new InvalidOperationException($"'{key}' 타입 불일치: 등록 {existing.Type.Name}, 요청 {type.Name}");
            return existing;
        }
        var entry = new Entry { Type = type };
        entries.Add(key, entry);
        LoadEntryAsync(key, entry).Forget();
        return entry;
    }

    private async UniTask LoadEntryAsync(string key, Entry entry)
    {
        try
        {
            await InitializeAsync();
            if (!IsCurrent(key, entry)) throw new OperationCanceledException($"'{key}' 로드 해제");
            entry.Handle = Addressables.LoadAssetAsync<Object>(key);
            Object asset = await entry.Handle.ToUniTask();
            if (!IsCurrent(key, entry)) throw new OperationCanceledException($"'{key}' 로드 해제");
            if (entry.Handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                throw entry.Handle.OperationException ?? new InvalidOperationException("로드 결과가 없습니다.");
            if (!entry.Type.IsInstanceOfType(asset))
                throw new InvalidOperationException($"실제 타입 {asset.GetType().Name}, 요청 타입 {entry.Type.Name}");
            entry.Asset = asset;
            entry.Completion.TrySetResult(asset);
        }
        catch (Exception exception)
        {
            if (IsCurrent(key, entry)) entries.Remove(key);
            if (!(exception is OperationCanceledException) && !(exception is ObjectDisposedException))
                Debug.LogError($"[ResourceManager] '{key}' 로드 실패: {exception.Message}");
            entry.Completion.TrySetException(exception);
        }
        finally
        {
            // 실패/해제된 로드의 핸들은 이 작업이 끝날 때 정리합니다.
            if (!IsCurrent(key, entry)) ReleaseHandle(entry);
        }
    }

    // 프리로드도 캐시만 채우며 Instantiate하지 않습니다. 같은 주소는 배치 내에서 중복 제거합니다.
    public async UniTask PreloadAsync(IEnumerable<PreloadRequest> requests)
    {
        RequireMainThread();
        if (requests == null) throw new ArgumentNullException(nameof(requests));
        var unique = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (PreloadRequest request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.Key) || request.AssetType == null ||
                !typeof(Object).IsAssignableFrom(request.AssetType))
                throw new ArgumentException("프리로드 주소와 Unity 에셋 타입을 지정하세요.", nameof(requests));
            if (unique.TryGetValue(request.Key, out Type previous) && previous != request.AssetType)
                throw new ArgumentException($"프리로드 주소 '{request.Key}'에 서로 다른 타입이 지정되었습니다.");
            unique[request.Key] = request.AssetType;
        }
        // 전체 요청을 먼저 검증하고, 순차 로딩으로 부팅 중 순간 메모리 사용을 제한합니다.
        foreach (var request in unique)
            if (entries.TryGetValue(request.Key, out Entry existing) &&
                existing.Type != request.Value)
                throw new InvalidOperationException($"'{request.Key}' 기존 캐시 상태와 프리로드 요청이 충돌합니다.");
        foreach (var request in unique)
            await GetOrLoad(request.Key, request.Value).Completion.Task;
    }

    public UniTask PreloadAsync<T>(IEnumerable<string> keys) where T : Object
    {
        if (keys == null) throw new ArgumentNullException(nameof(keys));
        var requests = new List<PreloadRequest>();
        foreach (string key in keys) requests.Add(PreloadRequest.For<T>(key));
        return PreloadAsync(requests);
    }

    // 호출자는 풀·오디오 등 모든 사용자를 먼저 정리해야 합니다.
    public bool Release(string key)
    {
        RequireMainThread();
        if (key == null || !entries.TryGetValue(key, out Entry entry)) return false;
        entries.Remove(key);
        entry.Completion.TrySetCanceled();
        // 로딩 중이면 LoadEntryAsync의 finally에서 해제합니다.
        if (entry.Asset != null) ReleaseHandle(entry);
        return true;
    }

    public void ReleaseAll()
    {
        RequireMainThread();
        foreach (string key in new List<string>(entries.Keys)) Release(key);
    }

    private bool IsCurrent(string key, Entry entry) =>
        entries.TryGetValue(key, out Entry current) && ReferenceEquals(current, entry);

    private static void ReleaseHandle(Entry entry)
    {
        if (entry.Handle.IsValid()) Addressables.Release(entry.Handle);
        entry.Handle = default;
        entry.Asset = null;
    }

    private static void RequireMainThread()
    {
        if (!PlayerLoopHelper.IsMainThread)
            throw new InvalidOperationException("ResourceManager API는 Unity 메인 스레드에서 호출하세요.");
    }

    private void OnDestroy()
    {
        disposed = true;
        ReleaseAfterDestroyAsync().Forget();
    }

    private async UniTask ReleaseAfterDestroyAsync()
    {
        // 씬 종료 시 같은 프레임의 인스턴스 파괴를 먼저 완료시킵니다.
        // 정상 경로에서는 GameManager.ReleaseResourcesAsync를 명시적으로 기다립니다.
        await UniTask.NextFrame();
        ReleaseAll();
    }
}
