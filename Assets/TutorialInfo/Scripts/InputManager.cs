using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    [Header("Input Action")]
    [SerializeField] private InputActionReference _moveAction;   // Vector2 (A/D, 나중에 W/S 사다리)
    [SerializeField] private InputActionReference _runAction;    // Button (Shift)
    [SerializeField] private InputActionReference _jumpAction;   // Button (Space)
    [SerializeField] private InputActionReference _attackAction; // Button (마우스 좌클릭)
    [SerializeField] private InputActionReference _aimAction;    // Vector2 (마우스 위치)

    private Camera _camera;

    public Vector2 MoveInput { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action OnJumpAction;
    public event Action OnAttackAction;

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
        _runAction.action.Enable();
        _jumpAction.action.Enable();
        _attackAction.action.Enable();
        _aimAction.action.Enable();

        _moveAction.action.performed += OnMove;
        _moveAction.action.canceled += OnMove;
        _runAction.action.performed += OnRun;
        _runAction.action.canceled += OnRun;
        _jumpAction.action.started += OnJump;
        _attackAction.action.started += OnAttack;
    }

    private void OnDisable()
    {
        _moveAction.action.performed -= OnMove;
        _moveAction.action.canceled -= OnMove;
        _runAction.action.performed -= OnRun;
        _runAction.action.canceled -= OnRun;
        _jumpAction.action.started -= OnJump;
        _attackAction.action.started -= OnAttack;

        _moveAction.action.Disable();
        _runAction.action.Disable();
        _jumpAction.action.Disable();
        _attackAction.action.Disable();
        _aimAction.action.Disable();
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
        Debug.Log($"Move: {MoveInput}");
    }

    private void OnRun(InputAction.CallbackContext context)
    {
        IsRunning = context.ReadValue<float>() != 0;
        Debug.Log($"Run: {IsRunning}");
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        Debug.Log("Jump 입력");
        OnJumpAction?.Invoke();
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        Debug.Log("Attack 입력");
        OnAttackAction?.Invoke();
    }
}