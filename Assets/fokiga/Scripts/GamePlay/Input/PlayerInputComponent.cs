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
        private Vector2 mLookDelta;
        private float mZoomDelta;
        private InputAction mLookAction;
        private InputAction mZoomAction;
        // 动作状态缓存
        private bool mIsJumping;
        private bool mIsRunning;
        private bool mIsSlipping;

        public Vector2 MoveDirection => mMoveDirection;

        public Vector2 LookDelta => mLookDelta;

        public float ZoomDelta => mZoomDelta;

        public bool IsRunning => mIsRunning;

        public override void OnAwake()
        {
            base.OnAwake();
            UpdatePriority = 100;
            InitializeInput();
        }

        public override void OnStart()
        {
            base.OnStart();
            // 启用输入映射
            if (mInputAsset != null)
            {
                mControllerMap.Enable();
            }
        }

        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);
            // 在这里可以处理需要每帧更新的输入逻辑
            if (mInputAsset == null || !mControllerMap.enabled)
            {
                mLookDelta = Vector2.zero;
                mZoomDelta = 0f;
                return;
            }

            UpdateMoveDirection();
            mLookDelta = mLookAction != null
                ? mLookAction.ReadValue<Vector2>()
                : Vector2.zero;
            mZoomDelta = mZoomAction != null
                ? mZoomAction.ReadValue<float>()
                : 0f;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            UnsubscribeInputEvents();
            if (mInputAsset != null)
            {
                mControllerMap.Disable();
                mInputAsset.Dispose();
            }

            mInputAsset = null;
            mMoveDirection = Vector2.zero;
            mLookDelta = Vector2.zero;
            mZoomDelta = 0f;
            mLookAction = null;
            mZoomAction = null;
            mIsRunning = false;
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
            mLookAction = mControllerMap.Look;
            mZoomAction = mControllerMap.Zoom;
            if (mLookAction == null || mZoomAction == null)
            {
                Debug.LogError("PlayerInputComponent: Look 或 Zoom 输入动作未在 InputAsset 中找到。");
            }
            SubscribeInputEvents();
        }

        private void SubscribeInputEvents()
        {
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
            if (mInputAsset == null)
            {
                return;
            }

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
        private void UpdateMoveDirection()
        {
            if (mInputAsset == null || !mControllerMap.enabled)
            {
                return;
            }

            var nextDirection = new Vector2(
                mControllerMap.MoveRight.ReadValue<float>() - mControllerMap.MoveLeft.ReadValue<float>(),
                mControllerMap.MoveForward.ReadValue<float>() - mControllerMap.MoveBack.ReadValue<float>());

            if (nextDirection.sqrMagnitude > 1f)
            {
                nextDirection.Normalize();
            }

            if (nextDirection == mMoveDirection)
            {
                return;
            }

            mMoveDirection = nextDirection;
            Owner?.EventManager.Broadcast(new InputEvents.MoveInputChangedEvent
            {
                MoveDirection = mMoveDirection
            });
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
            if (mInputAsset != null && !mControllerMap.enabled)
            {
                mControllerMap.Enable();
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (mInputAsset != null && mControllerMap.enabled)
            {
                mControllerMap.Disable();
            }

            if (mMoveDirection != Vector2.zero)
            {
                mMoveDirection = Vector2.zero;
                Owner?.EventManager.Broadcast(new InputEvents.MoveInputChangedEvent
                {
                    MoveDirection = Vector2.zero
                });
            }

            mLookDelta = Vector2.zero;
            mZoomDelta = 0f;
        }
    }
}
