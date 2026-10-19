using OZ.UI.Contracts;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [Header("Input Action")]
    [SerializeField] private InputActionReference _moveAction;   // Vector2 (A/D, 나중에 W/S 사다리)
    [SerializeField] private InputActionReference _dashAction;   // Button (Shift)
    [SerializeField] private InputActionReference _jumpAction;   // Button (Space)
    [SerializeField] private InputActionReference _attackAction; // Button (마우스 좌클릭)
    [SerializeField] private InputActionReference _aimAction;    // Vector2 (마우스 위치)

    // 슬롯마다 액션을 따로 둠 -> 어느 키를 눌렀는지 문자열로 구분할 필요가 없고,
    // 나중에 키 변경이나 슬롯별 비활성화가 필요할 때 액션 하나만 건드리면 됨
    [Header("Skill")]
    [SerializeField] private InputActionReference _skillQAction; // Button (Q)
    [SerializeField] private InputActionReference _skillEAction; // Button (E)
    [SerializeField] private InputActionReference _skillRAction; // Button (R)

    private Camera _camera;

    public Vector2 MoveInput { get; private set; }

    public event Action OnJumpAction;
    public event Action OnDashAction;
    public event Action OnAttackAction;

    //이벤트 1개 + 슬롯 번호 -> 컨트롤러는 핸들러 하나로 Q/E/R을 모두 처리
    public event Action<SkillSlot> OnSkillAction;

    private void Awake()
    {
        _camera = Camera.main;

        if (_camera == null)
        {
            Debug.LogError("[InputManager:Awake] 카메라를 찾을 수 없습니다.");
        }
    }

    private void OnEnable()
    {
        _moveAction.action.Enable();
        _dashAction.action.Enable();
        _jumpAction.action.Enable();
        _attackAction.action.Enable();
        _aimAction.action.Enable();
        _skillQAction.action.Enable();
        _skillEAction.action.Enable();
        _skillRAction.action.Enable();

        _moveAction.action.performed += OnMove;
        _moveAction.action.canceled += OnMove;
        _dashAction.action.started += OnDash;
        _jumpAction.action.started += OnJump;
        _attackAction.action.started += OnAttack;
        _skillQAction.action.started += OnSkillQ;
        _skillEAction.action.started += OnSkillE;
        _skillRAction.action.started += OnSkillR;
    }

    private void OnDisable()
    {
        MoveInput = Vector2.zero;

        _moveAction.action.performed -= OnMove;
        _moveAction.action.canceled -= OnMove;
        _dashAction.action.started -= OnDash;
        _jumpAction.action.started -= OnJump;
        _attackAction.action.started -= OnAttack;
        _skillQAction.action.started -= OnSkillQ;
        _skillEAction.action.started -= OnSkillE;
        _skillRAction.action.started -= OnSkillR;

        _moveAction.action.Disable();
        _dashAction.action.Disable();
        _jumpAction.action.Disable();
        _attackAction.action.Disable();
        _aimAction.action.Disable();
        _skillQAction.action.Disable();
        _skillEAction.action.Disable();
        _skillRAction.action.Disable();
    }

    // 커서 위치 → origin이 있는 Z 평면 위의 월드 좌표
    public Vector3 GetAimPoint(Vector3 origin)
    {
        Vector2 screenPos = _aimAction.action.ReadValue<Vector2>();
        Plane plane = new Plane(Vector3.forward, origin);
        Ray ray = _camera.ScreenPointToRay(screenPos);

        if (plane.Raycast(ray, out float dist))
        {
            return ray.GetPoint(dist);
        }

        return origin;
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    private void OnDash(InputAction.CallbackContext context)
    {
        OnDashAction?.Invoke();
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        OnJumpAction?.Invoke();
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        OnAttackAction?.Invoke();
    }

    private void OnSkillQ(InputAction.CallbackContext context)
    {
        OnSkillAction?.Invoke(SkillSlot.Q);
    }

    private void OnSkillE(InputAction.CallbackContext context)
    {
        OnSkillAction?.Invoke(SkillSlot.E);
    }

    private void OnSkillR(InputAction.CallbackContext context)
    {
        OnSkillAction?.Invoke(SkillSlot.R);
    }
}
