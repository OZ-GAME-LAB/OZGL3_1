using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class BaseManager: MonoBehaviour
{
    public abstract UniTask InitializeAsync();
}
