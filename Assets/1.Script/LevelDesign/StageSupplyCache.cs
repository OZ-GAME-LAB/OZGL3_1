using OZ.UI.Contracts;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A single-use field supply. A full inventory leaves the supply available.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class StageSupplyCache : MonoBehaviour
{
    [SerializeField] private ItemData[] rewards;
    [Tooltip("Optional visuals in the same order as Rewards; only the selected reward is shown.")]
    [SerializeField] private GameObject[] rewardVisuals;
    [SerializeField, Min(1)] private int amount = 1;

    private ItemData selectedItem;
    private bool rewardSelected;
    private bool collected;
    private float nextAttemptAt;
    private float nextFullToastAt;
    private static readonly HashSet<string> claimedSupplies = new HashSet<string>();
    private static GameManager claimOwner;
    private static uint claimRunId;

    public ItemData SelectedItem => selectedItem;
    public bool IsCollected => collected;

    public void Configure(ItemData item, int count = 1)
    {
        Configure(new[] { item }, null, count);
    }

    public void Configure(ItemData[] items, GameObject[] visuals = null, int count = 1)
    {
        if (Application.isPlaying && rewardSelected) return;
        rewards = items;
        rewardVisuals = visuals;
        amount = Mathf.Max(1, count);
        if (Application.isPlaying) SelectReward();
    }

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        SelectReward();
        string claimKey = GetClaimKey();
        if (claimKey != null && claimedSupplies.Contains(claimKey))
        {
            collected = true;
            gameObject.SetActive(false);
        }
    }

    private void SelectReward()
    {
        if (rewardSelected) return;
        int validCount = 0;
        if (rewards != null)
            foreach (ItemData item in rewards)
                if (IsFieldSupply(item)) validCount++;

        if (validCount == 0) return;
        rewardSelected = true;

        int chosenIndex = -1;
        if (validCount > 0)
        {
            int choice = Random.Range(0, validCount);
            for (int i = 0; i < rewards.Length; i++)
            {
                if (!IsFieldSupply(rewards[i])) continue;
                if (choice-- != 0) continue;
                selectedItem = rewards[i];
                chosenIndex = i;
                break;
            }
        }

        if (rewardVisuals != null)
            for (int i = 0; i < rewardVisuals.Length; i++)
                if (rewardVisuals[i] != null) rewardVisuals[i].SetActive(i == chosenIndex);
    }

    private static bool IsFieldSupply(ItemData item)
    {
        return item != null && item.IsUsable && !item.GrantsWeapon && item.slotIndex >= 0 && item.slotIndex < 4;
    }

    private string GetClaimKey()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null) return null;
        if (!ReferenceEquals(claimOwner, manager) || claimRunId != manager.RunId)
        {
            claimedSupplies.Clear();
            claimOwner = manager;
            claimRunId = manager.RunId;
        }
        return manager.StageNumber + ":" + name;
    }

    private void OnTriggerEnter(Collider other) => AttemptPickup(other);
    private void OnTriggerStay(Collider other) => AttemptPickup(other);

    private void AttemptPickup(Collider other)
    {
        if (collected || other == null || other.isTrigger || Time.time < nextAttemptAt) return;
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;
        nextAttemptAt = Time.time + 0.4f;
        TryCollect(player);
    }

    /// <summary>Collects once, through the active player's inventory, without spending currency.</summary>
    public bool TryCollect(PlayerController player)
    {
        if (collected || player == null || !player.gameObject.activeInHierarchy || UIState.IsGameplayInputBlocked) return false;
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead) return false;
        SelectReward();
        if (selectedItem == null) return false;

        TempPotionWallet wallet = UISources.Items as TempPotionWallet;
        if (wallet == null) return false;
        if (!wallet.AddFieldItem(selectedItem, amount))
        {
            if (Time.unscaledTime >= nextFullToastAt)
            {
                nextFullToastAt = Time.unscaledTime + 2f;
                GameUI.Notify.Toast("보급품을 보관할 공간이 부족합니다", ToastType.Warning);
            }
            return false;
        }

        collected = true;
        string claimKey = GetClaimKey();
        if (claimKey != null) claimedSupplies.Add(claimKey);
        GameUI.Notify.Toast($"{selectedItem.displayName} +{amount}");
        gameObject.SetActive(false);
        return true;
    }

    private void Reset()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.6f, 0f);
        trigger.size = new Vector3(1.4f, 1.6f, 2f);
    }
}
