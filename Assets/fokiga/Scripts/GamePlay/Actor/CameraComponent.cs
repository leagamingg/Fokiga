using Fokiga.Runtime.Core;
using UnityEngine;

namespace Fokiga.Runtime.Gameplay
{
    public class CameraComponent : ComponentBase
    {
        private Camera mThirdPersonCamera;
        private Transform mTargetTransform;
        private Transform mCameraPivot; // 相机旋转支点
        private CameraProfile mProfile;
        private bool mOwnsRuntimeProfile;
        private readonly RaycastHit[] mCameraCollisionHits = new RaycastHit[16];

        private float mCurrentYaw;    // 水平旋转角度（围绕角色Y轴）
        private float mCurrentPitch;  // 垂直旋转角度（上下视角）
        private float mCurrentArmLength;

        public CameraProfile Profile => mProfile;

        /// <summary>
        /// 将输入方向转换为相机相对的水平移动方向。
        /// </summary>
        public Vector3 GetPlanarMovementDirection(Vector2 inputDirection)
        {
            var fallbackDirection = new Vector3(inputDirection.x, 0f, inputDirection.y);
            if (mCameraPivot == null)
            {
                return fallbackDirection;
            }

            var forward = mCameraPivot.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= Mathf.Epsilon)
            {
                return fallbackDirection;
            }

            forward.Normalize();
            var right = mCameraPivot.right;
            right.y = 0f;
            right.Normalize();
            return right * inputDirection.x + forward * inputDirection.y;
        }

        /// <summary>
        /// 预制体加载后初始化相机
        /// </summary>
        public override void AfterGetPrefab(GameObject prefab)
        {
            base.AfterGetPrefab(prefab);

            if (Owner?.RealObject == null)
            {
                Debug.LogError("CameraComponent: Owner's RealObject is null!");
                return;
            }

            ResolveProfile();
            mTargetTransform = Owner.RealObject.transform;
            InitializeRotationFromOffset();
            CreateThirdPersonCamera();

            // 初始化旋转角度（基于初始偏移计算，确保初始看向角色）
            mCameraPivot.rotation = Quaternion.Euler(mCurrentPitch, mCurrentYaw, 0f);
        }

        /// <summary>
        /// 从偏移量计算初始旋转角度（确保相机初始看向角色）
        /// </summary>
        private void InitializeRotationFromOffset()
        {
            // 计算从相机到角色支点的方向（用于初始朝向）
            Vector3 offsetFromTarget = mProfile.Offset;
            if (offsetFromTarget.sqrMagnitude <= Mathf.Epsilon)
            {
                offsetFromTarget = new Vector3(0f, 2f, -4f);
            }

            Vector3 offsetFromPivot = offsetFromTarget - Vector3.up * mProfile.LookAtHeight;
            if (offsetFromPivot.sqrMagnitude <= Mathf.Epsilon)
            {
                offsetFromPivot = Vector3.back;
            }

            Vector3 directionToTarget = -offsetFromPivot.normalized;
            Quaternion initialRotation = Quaternion.LookRotation(directionToTarget);

            // 将欧拉角转换到[-180, 180]，避免俯仰角在跨越0度时被错误限制。
            Vector3 euler = initialRotation.eulerAngles;
            mCurrentYaw = NormalizeAngle(euler.y); // 水平旋转角度（绕Y轴）
            mCurrentPitch = NormalizeAngle(euler.x); // 垂直旋转角度（绕X轴）

            // 限制初始俯仰角在设定范围内（避免初始角度异常）
            mCurrentPitch = Mathf.Clamp(mCurrentPitch, mProfile.MinVerticalAngle, mProfile.MaxVerticalAngle);
            mCurrentArmLength = Mathf.Clamp(
                offsetFromPivot.magnitude,
                mProfile.MinArmLength,
                mProfile.MaxArmLength);
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        /// <summary>
        /// 创建第三人称相机及旋转支点
        /// </summary>
        private void CreateThirdPersonCamera()
        {
            // 创建相机旋转支点（位于角色看向的高度，作为旋转中心）
            var pivotObj = new GameObject($"{Owner.RealObject.name}mCameraPivot");
            pivotObj.transform.SetParent(null);
            pivotObj.transform.position = mTargetTransform.position + Vector3.up * mProfile.LookAtHeight;
            mCameraPivot = pivotObj.transform;

            // 创建相机对象
            var cameraObj = new GameObject($"{Owner.RealObject.name}mThirdPersonCamera");
            mThirdPersonCamera = cameraObj.AddComponent<Camera>();
            mThirdPersonCamera.transform.SetParent(mCameraPivot);

            ApplyCameraProfile();
        }

        private void ResolveProfile()
        {
            mProfile = Resources.Load<CameraProfile>(CameraProfile.DefaultResourcesPath);
            if (mProfile != null)
            {
                return;
            }

            mProfile = CameraProfile.CreateRuntimeFallback();
            mOwnsRuntimeProfile = true;
            Debug.LogWarning(
                $"未找到相机配置：Resources/{CameraProfile.DefaultResourcesPath}. 将使用代码默认值。");
        }

        private void ApplyCameraProfile()
        {
            if (mProfile == null || mThirdPersonCamera == null || mCameraPivot == null)
            {
                return;
            }

            mCameraPivot.position = mTargetTransform.position + Vector3.up * mProfile.LookAtHeight;
            mThirdPersonCamera.clearFlags = mProfile.ClearFlags;
            mThirdPersonCamera.fieldOfView = mProfile.FieldOfView;
            mThirdPersonCamera.nearClipPlane = mProfile.NearClipPlane;
            mThirdPersonCamera.farClipPlane = mProfile.FarClipPlane;
            mThirdPersonCamera.depth = mProfile.Depth;

            mThirdPersonCamera.transform.localPosition = Vector3.back * mCurrentArmLength;
            mThirdPersonCamera.transform.localRotation = Quaternion.identity;
        }

        private void ReleaseRuntimeProfile()
        {
            if (mOwnsRuntimeProfile && mProfile != null)
            {
                Object.Destroy(mProfile);
            }

            mProfile = null;
            mOwnsRuntimeProfile = false;
        }

        /// <summary>
        /// 切换相机配置。传入null时恢复Resources中的默认配置。
        /// </summary>
        public void SetProfile(CameraProfile profile)
        {
            if (profile == mProfile)
            {
                return;
            }

            ReleaseRuntimeProfile();
            mProfile = profile;
            if (mProfile == null)
            {
                ResolveProfile();
            }

            if (mThirdPersonCamera == null || mCameraPivot == null)
            {
                return;
            }

            InitializeRotationFromOffset();
            ApplyCameraProfile();
            mCameraPivot.rotation = Quaternion.Euler(mCurrentPitch, mCurrentYaw, 0f);
        }

        /// <summary>
        /// 处理相机旋转输入
        /// </summary>
        private void HandleCameraRotation(Vector2 rotateInput)
        {
            if (mProfile == null || mCameraPivot == null || rotateInput.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            // 水平旋转（围绕角色Y轴）
            mCurrentYaw += rotateInput.x * mProfile.MouseSensitivity;

            // 垂直旋转（限制角度范围）
            mCurrentPitch -= rotateInput.y * mProfile.MouseSensitivity;
            mCurrentPitch = Mathf.Clamp(
                mCurrentPitch,
                mProfile.MinVerticalAngle,
                mProfile.MaxVerticalAngle);

            // 应用旋转到支点（带动相机旋转）
            mCameraPivot.rotation = Quaternion.Euler(mCurrentPitch, mCurrentYaw, 0);
        }

        private void HandleCameraZoom(float scrollInput)
        {
            if (mProfile == null || Mathf.Abs(scrollInput) <= Mathf.Epsilon)
            {
                return;
            }

            mCurrentArmLength = Mathf.Clamp(
                mCurrentArmLength - scrollInput * mProfile.ZoomSpeed * 0.01f,
                mProfile.MinArmLength,
                mProfile.MaxArmLength);
        }

        /// <summary>
        /// 更新相机位置（包含碰撞检测）
        /// </summary>
        private void UpdateCameraPosition()
        {
            if (mProfile == null || mTargetTransform == null || mThirdPersonCamera == null || mCameraPivot == null)
            {
                return;
            }

            Vector3 targetPivotPosition = mTargetTransform.position + Vector3.up * mProfile.LookAtHeight;
            mCameraPivot.position = targetPivotPosition;

            // 计算相机理想位置（基于支点和初始偏移距离）
            float targetDistance = mCurrentArmLength;
            Vector3 desiredDirection = mCameraPivot.TransformDirection(Vector3.back); // 支点后方（基于当前旋转）

            // 碰撞检测：避免相机穿模，同时忽略角色自身的碰撞体。
            var hitCount = Physics.SphereCastNonAlloc(
                mCameraPivot.position,
                mProfile.SphereRadius,
                desiredDirection,
                mCameraCollisionHits,
                targetDistance,
                mProfile.ObstacleLayers,
                QueryTriggerInteraction.Ignore);
            var hasObstacle = false;
            var closestHitDistance = targetDistance;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = mCameraCollisionHits[index];
                if (hit.collider == null || IsTargetCollider(hit.collider))
                {
                    continue;
                }

                if (hit.distance < closestHitDistance)
                {
                    closestHitDistance = hit.distance;
                    hasObstacle = true;
                }
            }

            if (hasObstacle)
            {
                targetDistance = Mathf.Max(
                    closestHitDistance - mProfile.SphereRadius,
                    mProfile.MinDistance);
            }

            mThirdPersonCamera.transform.localPosition = Vector3.back * targetDistance;
            mThirdPersonCamera.transform.localRotation = Quaternion.identity;
        }

        private bool IsTargetCollider(Collider collider)
        {
            return mTargetTransform != null &&
                (collider.transform == mTargetTransform || collider.transform.IsChildOf(mTargetTransform));
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (Application.isPlaying)
            {
                SetCursorLocked(false);
            }

            if (mThirdPersonCamera != null)
            {
                Object.Destroy(mThirdPersonCamera.gameObject);
                mThirdPersonCamera = null;
            }
            if (mCameraPivot != null)
            {
                Object.Destroy(mCameraPivot.gameObject);
                mCameraPivot = null;
            }
            mTargetTransform = null;
            ReleaseRuntimeProfile();
        }

        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);
            var inputComponent = Owner?.GetComponent<PlayerInputComponent>();
            if (inputComponent != null)
            {
                HandleCameraRotation(inputComponent.LookDelta);
                HandleCameraZoom(inputComponent.ZoomDelta);
            }

        }

        public override void OnLateUpdate(float deltaTime)
        {
            base.OnLateUpdate(deltaTime);
            UpdateCameraPosition();
        }

        public override void OnStart()
        {
            base.OnStart();
            if (Application.isPlaying)
            {
                SetCursorLocked(true);
            }
        }

        public override void OnEnable()
        {
            base.OnEnable();
            if (mThirdPersonCamera != null)
            {
                mThirdPersonCamera.enabled = true;
            }

            if (Application.isPlaying)
            {
                SetCursorLocked(true);
            }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (mThirdPersonCamera != null)
            {
                mThirdPersonCamera.enabled = false;
            }

            if (Application.isPlaying)
            {
                SetCursorLocked(false);
            }
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        public override void OnRemovedFromActor()
        {
            base.OnRemovedFromActor();
            OnDestroy();
        }
    }
}
