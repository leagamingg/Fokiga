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

        private float mCurrentYaw;    // 水平旋转角度（围绕角色Y轴）
        private float mCurrentPitch;  // 垂直旋转角度（上下视角）

        public CameraProfile Profile => mProfile;

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
            CreateThirdPersonCamera();

            // 初始化旋转角度（基于初始偏移计算，确保初始看向角色）
            InitializeRotationFromOffset();
        }

        /// <summary>
        /// 从偏移量计算初始旋转角度（确保相机初始看向角色）
        /// </summary>
        private void InitializeRotationFromOffset()
        {
            // 计算从相机到角色支点的方向（用于初始朝向）
            Vector3 offset = mProfile.Offset;
            if (offset.sqrMagnitude <= Mathf.Epsilon)
            {
                offset = new Vector3(0f, 2f, -4f);
            }

            Vector3 directionToTarget = -offset.normalized; // 偏移是相机相对于角色的位置，取反就是看向角色的方向
            Quaternion initialRotation = Quaternion.LookRotation(directionToTarget);

            // 提取欧拉角（确保角度在0-360范围内）
            Vector3 euler = initialRotation.eulerAngles;
            mCurrentYaw = euler.y; // 水平旋转角度（绕Y轴）
            mCurrentPitch = euler.x; // 垂直旋转角度（绕X轴）

            // 限制初始俯仰角在设定范围内（避免初始角度异常）
            mCurrentPitch = Mathf.Clamp(mCurrentPitch, mProfile.MinVerticalAngle, mProfile.MaxVerticalAngle);
        }

        /// <summary>
        /// 创建第三人称相机及旋转支点
        /// </summary>
        private void CreateThirdPersonCamera()
        {
            // 创建相机旋转支点（位于角色看向的高度，作为旋转中心）
            var pivotObj = new GameObject($"{Owner.RealObject.name}mCameraPivot");
            pivotObj.transform.SetParent(mTargetTransform, false);
            pivotObj.transform.localPosition = new Vector3(0f, mProfile.LookAtHeight, 0f); // 支点在角色看向高度
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

            mCameraPivot.localPosition = new Vector3(0f, mProfile.LookAtHeight, 0f);
            mThirdPersonCamera.clearFlags = mProfile.ClearFlags;
            mThirdPersonCamera.fieldOfView = mProfile.FieldOfView;
            mThirdPersonCamera.nearClipPlane = mProfile.NearClipPlane;
            mThirdPersonCamera.farClipPlane = mProfile.FarClipPlane;
            mThirdPersonCamera.depth = mProfile.Depth;

            Vector3 offset = mProfile.Offset;
            mThirdPersonCamera.transform.localPosition = new Vector3(
                offset.x,
                offset.y - mProfile.LookAtHeight,
                offset.z);
            mThirdPersonCamera.transform.LookAt(mCameraPivot.position);
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

            ApplyCameraProfile();
            InitializeRotationFromOffset();
            mCameraPivot.rotation = Quaternion.Euler(mCurrentPitch, mCurrentYaw, 0f);
        }

        /// <summary>
        /// 处理相机旋转输入
        /// </summary>
        private void HandleCameraRotation(Vector2 rotateInput)
        {
            if (rotateInput.sqrMagnitude < 0.01f) return;

            // 水平旋转（围绕角色Y轴）
            mCurrentYaw += rotateInput.x * mProfile.RotationSpeed;

            // 垂直旋转（限制角度范围）
            mCurrentPitch -= rotateInput.y * mProfile.RotationSpeed;
            mCurrentPitch = Mathf.Clamp(
                mCurrentPitch,
                mProfile.MinVerticalAngle,
                mProfile.MaxVerticalAngle);

            // 应用旋转到支点（带动相机旋转）
            mCameraPivot.rotation = Quaternion.Euler(mCurrentPitch, mCurrentYaw, 0);
        }

        /// <summary>
        /// 更新相机位置（包含碰撞检测）
        /// </summary>
        private void UpdateCameraPosition()
        {
            if (mTargetTransform == null || mThirdPersonCamera == null || mCameraPivot == null) return;

            // 支点跟随角色移动（平滑过渡）
            Vector3 targetPivotPosition = mTargetTransform.position + Vector3.up * mProfile.LookAtHeight;
            mCameraPivot.position = Vector3.Lerp(
            mCameraPivot.position,
            targetPivotPosition,
            Time.deltaTime * mProfile.FollowSpeed
            );

            // 计算相机理想位置（基于支点和初始偏移距离）
            float targetDistance = mProfile.Offset.magnitude; // 保持初始设定的距离
            Vector3 desiredDirection = mCameraPivot.TransformDirection(Vector3.back); // 支点后方（基于当前旋转）

            // 碰撞检测：避免相机穿模
            if (Physics.SphereCast(
            mCameraPivot.position,
            mProfile.SphereRadius,
            desiredDirection,
            out RaycastHit hit,
            targetDistance,
            mProfile.ObstacleLayers))
            {
                // 遇到障碍物时拉近相机（但不小于最小距离）
                targetDistance = Mathf.Max(
                    hit.distance - mProfile.SphereRadius,
                    mProfile.MinDistance);
            }

            // 计算最终位置并平滑过渡
            Vector3 finalPosition = mCameraPivot.position + desiredDirection * targetDistance;
            mThirdPersonCamera.transform.position = Vector3.Lerp(
            mThirdPersonCamera.transform.position,
            finalPosition,
            Time.deltaTime * mProfile.FollowSpeed * 2 // 相机位置调整更快，提升响应感
            );

            // 始终看向角色支点
            mThirdPersonCamera.transform.LookAt(mCameraPivot.position);
        }

        // 监听相机旋转事件（需要输入系统发送该事件）
        public override void OnAwake()
        {
            base.OnAwake();
            AddListener<InputEvents.MoveInputChangedEvent>(OnMovePerformed);
        }


        public override void OnDestroy()
        {
            base.OnDestroy();
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
            RemoveListener<InputEvents.MoveInputChangedEvent>(OnMovePerformed);
        }

        private void OnMovePerformed(InputEvents.MoveInputChangedEvent evt)
        {
            Debug.Log($"Movement direction: {evt.MoveDirection}");
        }

        public override void OnUpdate(float deltaTime)
        {
            base.OnUpdate(deltaTime);
            UpdateCameraPosition();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            if (mThirdPersonCamera != null)
                mThirdPersonCamera.enabled = true;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (mThirdPersonCamera != null)
                mThirdPersonCamera.enabled = false;
        }

        public override void OnRemovedFromActor()
        {
            base.OnRemovedFromActor();
            OnDestroy();
        }
    }
}
