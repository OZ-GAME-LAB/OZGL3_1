using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Keyboard-only art preview controls. Never added to the shipping player prefab.</summary>
public sealed class UrbanKitPreviewControls : MonoBehaviour
{
    [SerializeField] private SocketWeapon _sword;
    [SerializeField] private SocketWeapon _staff;
    [SerializeField] private GameObject _potion;
    private HandSocketEquipment _equipment;
    private ProtagonistActionAnimator _actions;
    private GameObject _heldPotion;

    public void Configure(SocketWeapon sword,SocketWeapon staff,GameObject potion) { _sword=sword;_staff=staff;_potion=potion; }
    private void Awake()
    {
        _equipment=GetComponent<HandSocketEquipment>();_actions=GetComponent<ProtagonistActionAnimator>();
        if(_actions!=null) _actions.Cancelled+=OnCancelled;
    }
    private void OnDestroy() { if(_actions!=null)_actions.Cancelled-=OnCancelled; ClearPotion(); }
    private void OnCancelled(ProtagonistExtraAction action) { ClearPotion(); }
    private void Update()
    {
        Keyboard keys=Keyboard.current;if(keys==null||_equipment==null||_actions==null)return;
        if(keys.f1Key.wasPressedThisFrame)_equipment.Equip(_sword);
        if(keys.f2Key.wasPressedThisFrame)_equipment.Equip(_staff);
        if(keys.f3Key.wasPressedThisFrame)_equipment.Unequip();
        if(keys.digit5Key.wasPressedThisFrame)
        {
            if(_actions.TryDrink(null,ClearPotion)) HoldPotion(false);
        }
        if(keys.digit6Key.wasPressedThisFrame&&_actions.TryThrow(ReleasePotion,ClearPotion))HoldPotion(true);
        if(keys.digit7Key.wasPressedThisFrame) _actions.TryInteract();
        if(keys.digit8Key.wasPressedThisFrame) _actions.TryPickup();
    }
    private void HoldPotion(bool throwing)
    {
        ClearPotion();if(_potion==null)return;
        Transform socket=_equipment.GetNearHandSocket();if(socket==null)return;
        _heldPotion=Instantiate(_potion,socket,false);
        var grip=_heldPotion.GetComponent<ConsumableHandProp>();
        if(grip!=null)
        {
            if(throwing)grip.AttachForThrow(socket,_equipment.UsesLeftHand);
            else grip.AttachTo(socket,_equipment.UsesLeftHand);
        }
    }
    private void ReleasePotion()
    {
        if(_heldPotion==null)return;
        var prop=_heldPotion;_heldPotion=null;prop.transform.SetParent(null,true);
        float direction=transform.forward.x<0?-1:1;
        prop.AddComponent<UrbanPreviewThrownProp>().Launch(new Vector3(direction*7,3.2f,0));
    }
    private void ClearPotion() { if(_heldPotion!=null)Destroy(_heldPotion);_heldPotion=null; }
    private void OnGUI()
    {
        GUI.Label(new Rect(14,12,760,25),"ART PREVIEW  |  A/D run  Ctrl walk  Space jump  Shift dash  Mouse punch/weapon attack");
        GUI.Label(new Rect(14,36,760,25),"F1 sword   F2 staff   F3 unarmed   |   5 drink   6 throw   7 interact   8 pickup");
    }
}
