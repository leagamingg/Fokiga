using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Fokiga.Runtime.Core;

namespace Fokiga.Runtime.Gameplay
{
    public class PlayerInputComponent : ComponentBase
    {
        // 输入资产实例（假设通过外部注入或单例获取）
        private InputAsset mInputAsset;
        // 角色控制器输入映射
        private InputAsset.CharacterControllerMapActions mControllerMap;

        // 移动输入缓存
        private Vector2 mMoveDirection;
        // 动作状态缓存
        private bool mIsJumping;
        private bool mIsRunning;
        private bool mIsSlipping;

        public override void OnAwake()
        {
            base.OnAwake();
            InitializeInput();
        }

        public override void OnStart()
        {
            base.OnStart();
            // 启用输入映射
            mControllerMap.Enable();
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
            mControllerMap.Disable();
            mInputAsset = null;
        }

        private void InitializeInput()
        {
            mInputAsset = new InputAsset();
            if (mInputAsset == null)
            {
                Debug.LogError("InputAsset instance is null!");
                return;
            }
            mControllerMap = mInputAsset.CharacterControllerMap;
            SubscribeInputEvents();
        }

        private void SubscribeInputEvents()
        {
            // 移动输入事件
            mControllerMap.MoveForward.performed += OnMoveForwardPerformed;
            mControllerMap.MoveForward.canceled += OnMoveForwardCanceled;
            mControllerMap.MoveBack.performed += OnMoveBackPerformed;
            mControllerMap.MoveBack.canceled += OnMoveBackCanceled;
            mControllerMap.MoveLeft.performed += OnMoveLeftPerformed;
            mControllerMap.MoveLeft.canceled += OnMoveLeftCanceled;
            mControllerMap.MoveRight.performed += OnMoveRightPerformed;
            mControllerMap.MoveRight.canceled += OnMoveRightCanceled;

            // 跳跃输入事件
            mControllerMap.Jump.performed += OnJumpPerformedHandler;
            mControllerMap.Jump.canceled += OnJumpCanceledHandler;

            // 奔跑输入事件
            mControllerMap.Run.performed += OnRunPerformedHandler;
            mControllerMap.Run.canceled += OnRunCanceledHandler;

            // 滑行输入事件
            mControllerMap.Slip.performed += OnSlipPerformedHandler;
            mControllerMap.Slip.canceled += OnSlipCanceledHandler;
        }

        private void UnsubscribeInputEvents()
        {
            // 移动输入事件
            mControllerMap.MoveForward.performed -= OnMoveForwardPerformed;
            mControllerMap.MoveForward.canceled -= OnMoveForwardCanceled;
            mControllerMap.MoveBack.performed -= OnMoveBackPerformed;
            mControllerMap.MoveBack.canceled -= OnMoveBackCanceled;
            mControllerMap.MoveLeft.performed -= OnMoveLeftPerformed;
            mControllerMap.MoveLeft.canceled -= OnMoveLeftCanceled;
            mControllerMap.MoveRight.performed -= OnMoveRightPerformed;
            mControllerMap.MoveRight.canceled -= OnMoveRightCanceled;

            // 跳跃输入事件
            mControllerMap.Jump.performed -= OnJumpPerformedHandler;
            mControllerMap.Jump.canceled -= OnJumpCanceledHandler;

            // 奔跑输入事件
            mControllerMap.Run.performed -= OnRunPerformedHandler;
            mControllerMap.Run.canceled -= OnRunCanceledHandler;

            // 滑行输入事件
            mControllerMap.Slip.performed -= OnSlipPerformedHandler;
            mControllerMap.Slip.canceled -= OnSlipCanceledHandler;
        }

        #region 移动输入处理
        private void OnMoveForwardPerformed(InputAction.CallbackContext context)
        {
            mMoveDirection.y = context.ReadValue<float>();
            Debug.Log("Input Player MoveForward");
            Owner.EventManager.Broadcast(new InputEvents.MoveInputChangedEvent { MoveDirection = mMoveDirection });
        }

        private void OnMoveForwardCanceled(InputAction.CallbackContext context)
        {
            mMoveDirection.y = 0;
        }

        private void OnMoveBackPerformed(InputAction.CallbackContext context)
        {
            mMoveDirection.y = -context.ReadValue<float>(); // 向后为负值
            Debug.Log("Input Player MoveBack");
        }

        private void OnMoveBackCanceled(InputAction.CallbackContext context)
        {
            mMoveDirection.y = 0;
        }

        private void OnMoveLeftPerformed(InputAction.CallbackContext context)
        {
            mMoveDirection.x = -context.ReadValue<float>(); // 向左为负值
        }

        private void OnMoveLeftCanceled(InputAction.CallbackContext context)
        {
            mMoveDirection.x = 0;
        }

        private void OnMoveRightPerformed(InputAction.CallbackContext context)
        {
            mMoveDirection.x = context.ReadValue<float>();
        }

        private void OnMoveRightCanceled(InputAction.CallbackContext context)
        {
            mMoveDirection.x = 0;
        }

        // 限制移动方向的大小（防止斜向移动速度过快）
        private void ClampMoveDirection()
        {
            if (mMoveDirection.magnitude > 1f)
            {
                mMoveDirection.Normalize();
            }
        }
        #endregion

        #region 动作输入处理
        private void OnJumpPerformedHandler(InputAction.CallbackContext context)
        {
            mIsJumping = true;
        }

        private void OnJumpCanceledHandler(InputAction.CallbackContext context)
        {
            mIsJumping = false;
        }

        private void OnRunPerformedHandler(InputAction.CallbackContext context)
        {
            mIsRunning = true;
        }

        private void OnRunCanceledHandler(InputAction.CallbackContext context)
        {
            mIsRunning = false;
        }

        private void OnSlipPerformedHandler(InputAction.CallbackContext context)
        {
            mIsSlipping = true;
        }

        private void OnSlipCanceledHandler(InputAction.CallbackContext context)
        {
            mIsSlipping = false;
        }
        #endregion

        public override void OnEnable()
        {
            base.OnEnable();
            if (mControllerMap.enabled)
            {
                mControllerMap.Enable();
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (mControllerMap.enabled)
            {
                mControllerMap.Disable();
            }
        }
    }
}
