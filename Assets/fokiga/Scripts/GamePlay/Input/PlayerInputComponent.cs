using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Base;

public class PlayerInputComponent : ComponentBase
{
    // 输入资产实例（假设通过外部注入或单例获取）
    private InputAsset _inputAsset;
    // 角色控制器输入映射
    private InputAsset.CharacterControllerMapActions _controllerMap;

    // 移动输入缓存
    private Vector2 _moveDirection;
    // 动作状态缓存
    private bool _isJumping;
    private bool _isRunning;
    private bool _isSlipping;

    public override void OnAwake()
    {
        base.OnAwake();
        InitializeInput();
    }

    public override void OnStart()
    {
        base.OnStart();
        // 启用输入映射
        _controllerMap.Enable();
    }

    public override void OnUpdate(float deltaTime)
    {
        base.OnUpdate(deltaTime);
        // 在这里可以处理需要每帧更新的输入逻辑
        ClampMoveDirection();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        UnsubscribeInputEvents();
        _controllerMap.Disable();
        _inputAsset = null;
    }

    private void InitializeInput()
    {
        _inputAsset = new InputAsset();
        if (_inputAsset == null)
        {
            Debug.LogError("InputAsset instance is null!");
            return;
        }
        _controllerMap = _inputAsset.CharacterControllerMap;
        SubscribeInputEvents();
    }

    private void SubscribeInputEvents()
    {
        // 移动输入事件
        _controllerMap.MoveForward.performed += OnMoveForwardPerformed;
        _controllerMap.MoveForward.canceled += OnMoveForwardCanceled;
        _controllerMap.MoveBack.performed += OnMoveBackPerformed;
        _controllerMap.MoveBack.canceled += OnMoveBackCanceled;
        _controllerMap.MoveLeft.performed += OnMoveLeftPerformed;
        _controllerMap.MoveLeft.canceled += OnMoveLeftCanceled;
        _controllerMap.MoveRight.performed += OnMoveRightPerformed;
        _controllerMap.MoveRight.canceled += OnMoveRightCanceled;

        // 跳跃输入事件
        _controllerMap.Jump.performed += OnJumpPerformedHandler;
        _controllerMap.Jump.canceled += OnJumpCanceledHandler;

        // 奔跑输入事件
        _controllerMap.Run.performed += OnRunPerformedHandler;
        _controllerMap.Run.canceled += OnRunCanceledHandler;

        // 滑行输入事件
        _controllerMap.Slip.performed += OnSlipPerformedHandler;
        _controllerMap.Slip.canceled += OnSlipCanceledHandler;
    }

    private void UnsubscribeInputEvents()
    {
        // 移动输入事件
        _controllerMap.MoveForward.performed -= OnMoveForwardPerformed;
        _controllerMap.MoveForward.canceled -= OnMoveForwardCanceled;
        _controllerMap.MoveBack.performed -= OnMoveBackPerformed;
        _controllerMap.MoveBack.canceled -= OnMoveBackCanceled;
        _controllerMap.MoveLeft.performed -= OnMoveLeftPerformed;
        _controllerMap.MoveLeft.canceled -= OnMoveLeftCanceled;
        _controllerMap.MoveRight.performed -= OnMoveRightPerformed;
        _controllerMap.MoveRight.canceled -= OnMoveRightCanceled;

        // 跳跃输入事件
        _controllerMap.Jump.performed -= OnJumpPerformedHandler;
        _controllerMap.Jump.canceled -= OnJumpCanceledHandler;

        // 奔跑输入事件
        _controllerMap.Run.performed -= OnRunPerformedHandler;
        _controllerMap.Run.canceled -= OnRunCanceledHandler;

        // 滑行输入事件
        _controllerMap.Slip.performed -= OnSlipPerformedHandler;
        _controllerMap.Slip.canceled -= OnSlipCanceledHandler;
    }

    #region 移动输入处理
    private void OnMoveForwardPerformed(InputAction.CallbackContext context)
    {
        _moveDirection.y = context.ReadValue<float>();
        Debug.Log("Input Player MoveForward");
        Owner.EventManager.Broadcast(new InputEvents.MoveInputChangedEvent { MoveDirection = _moveDirection });
    }

    private void OnMoveForwardCanceled(InputAction.CallbackContext context)
    {
        _moveDirection.y = 0;
    }

    private void OnMoveBackPerformed(InputAction.CallbackContext context)
    {
        _moveDirection.y = -context.ReadValue<float>(); // 向后为负值
        Debug.Log("Input Player MoveBack");
    }

    private void OnMoveBackCanceled(InputAction.CallbackContext context)
    {
        _moveDirection.y = 0;
    }

    private void OnMoveLeftPerformed(InputAction.CallbackContext context)
    {
        _moveDirection.x = -context.ReadValue<float>(); // 向左为负值
    }

    private void OnMoveLeftCanceled(InputAction.CallbackContext context)
    {
        _moveDirection.x = 0;
    }

    private void OnMoveRightPerformed(InputAction.CallbackContext context)
    {
        _moveDirection.x = context.ReadValue<float>();
    }

    private void OnMoveRightCanceled(InputAction.CallbackContext context)
    {
        _moveDirection.x = 0;
    }

    // 限制移动方向的大小（防止斜向移动速度过快）
    private void ClampMoveDirection()
    {
        if (_moveDirection.magnitude > 1f)
        {
            _moveDirection.Normalize();
        }
    }
    #endregion

    #region 动作输入处理
    private void OnJumpPerformedHandler(InputAction.CallbackContext context)
    {
        _isJumping = true;
    }

    private void OnJumpCanceledHandler(InputAction.CallbackContext context)
    {
        _isJumping = false;
    }

    private void OnRunPerformedHandler(InputAction.CallbackContext context)
    {
        _isRunning = true;
    }

    private void OnRunCanceledHandler(InputAction.CallbackContext context)
    {
        _isRunning = false;
    }

    private void OnSlipPerformedHandler(InputAction.CallbackContext context)
    {
        _isSlipping = true;
    }

    private void OnSlipCanceledHandler(InputAction.CallbackContext context)
    {
        _isSlipping = false;
    }
    #endregion

    public override void OnEnable()
    {
        base.OnEnable();
        if  (_controllerMap.enabled)
        {
            _controllerMap.Enable();
        }
    }

    public override void OnDisable()
    {
        base.OnDisable();
        if (_controllerMap.enabled)
        {
            _controllerMap.Disable();
        }
    }
}