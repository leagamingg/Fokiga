using Fokiga.Runtime.Core;
using UnityEngine;

namespace Fokiga.Runtime.Gameplay
{
    public class CameraComponent : ComponentBase
    {
        private Camera _thirdPersonCamera;
        private Transform _targetTransform;
        private Transform _cameraPivot; // 相机旋转支点

        // 基础跟随参数（默认值确保后上方位置：z负方向=后方，y正方向=上方）
        [SerializeField] private Vector3 _offset = new Vector3(0, 2f, -4f); // 后上方偏移（y=2上方，z=-4后方）
        [SerializeField] private float _followSpeed = 5f;
        [SerializeField] private float _lookAtHeight = 1.5f; // 看向角色的高度（通常是角色腰部/胸部）

        // 旋转控制参数
        [SerializeField] private float _rotationSpeed = 2f;
        [SerializeField] private float _minVerticalAngle = -30f; // 最小仰角（避免低头过度）
        [SerializeField] private float _maxVerticalAngle = 60f;  // 最大俯角（避免抬头过度）
        private float _currentYaw;    // 水平旋转角度（围绕角色Y轴）
        private float _currentPitch;  // 垂直旋转角度（上下视角）

        // 碰撞检测参数
        [SerializeField] private float _sphereRadius = 0.3f;
        [SerializeField] private LayerMask _obstacleLayers = ~0;
        [SerializeField] private float _minDistance = 0.5f;

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

            _targetTransform = Owner.RealObject.transform;
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
            Vector3 directionToTarget = -_offset.normalized; // 偏移是相机相对于角色的位置，取反就是看向角色的方向
            Quaternion initialRotation = Quaternion.LookRotation(directionToTarget);

            // 提取欧拉角（确保角度在0-360范围内）
            Vector3 euler = initialRotation.eulerAngles;
            _currentYaw = euler.y; // 水平旋转角度（绕Y轴）
            _currentPitch = euler.x; // 垂直旋转角度（绕X轴）

            // 限制初始俯仰角在设定范围内（避免初始角度异常）
            _currentPitch = Mathf.Clamp(_currentPitch, _minVerticalAngle, _maxVerticalAngle);
        }

        /// <summary>
        /// 创建第三人称相机及旋转支点
        /// </summary>
        private void CreateThirdPersonCamera()
        {
            // 创建相机旋转支点（位于角色看向的高度，作为旋转中心）
            var pivotObj = new GameObject($"{Owner.RealObject.name}_CameraPivot");
            pivotObj.transform.SetParent(_targetTransform, false);
            pivotObj.transform.localPosition = new Vector3(0, _lookAtHeight, 0); // 支点在角色的_lookAtHeight高度
            _cameraPivot = pivotObj.transform;

            // 创建相机对象
            var cameraObj = new GameObject($"{Owner.RealObject.name}_ThirdPersonCamera");
            _thirdPersonCamera = cameraObj.AddComponent<Camera>();
            _thirdPersonCamera.transform.SetParent(_cameraPivot);

            // 相机基础设置
            _thirdPersonCamera.clearFlags = CameraClearFlags.Skybox;
            _thirdPersonCamera.fieldOfView = 60f;
            _thirdPersonCamera.nearClipPlane = 0.1f;
            _thirdPersonCamera.farClipPlane = 1000f;
            _thirdPersonCamera.depth = -1; // 确保在其他相机之前渲染

            // 初始位置：基于_offset在角色后上方（相对于支点）
            // 支点在角色的_lookAtHeight处，相机相对于支点的位置由_offset的方向和长度决定
            _thirdPersonCamera.transform.localPosition = new Vector3(
            _offset.x,
            _offset.y - _lookAtHeight, // 抵消支点的Y轴偏移，确保最终在角色后上方
            _offset.z
            );

            // 初始旋转：看向角色支点
            _thirdPersonCamera.transform.LookAt(_cameraPivot.position);
        }

        /// <summary>
        /// 处理相机旋转输入
        /// </summary>
        private void HandleCameraRotation(Vector2 rotateInput)
        {
            if (rotateInput.sqrMagnitude < 0.01f) return;

            // 水平旋转（围绕角色Y轴）
            _currentYaw += rotateInput.x * _rotationSpeed;

            // 垂直旋转（限制角度范围）
            _currentPitch -= rotateInput.y * _rotationSpeed;
            _currentPitch = Mathf.Clamp(_currentPitch, _minVerticalAngle, _maxVerticalAngle);

            // 应用旋转到支点（带动相机旋转）
            _cameraPivot.rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0);
        }

        /// <summary>
        /// 更新相机位置（包含碰撞检测）
        /// </summary>
        private void UpdateCameraPosition()
        {
            if (_targetTransform == null || _thirdPersonCamera == null || _cameraPivot == null) return;

            // 支点跟随角色移动（平滑过渡）
            Vector3 targetPivotPosition = _targetTransform.position + Vector3.up * _lookAtHeight;
            _cameraPivot.position = Vector3.Lerp(
            _cameraPivot.position,
            targetPivotPosition,
            Time.deltaTime * _followSpeed
            );

            // 计算相机理想位置（基于支点和初始偏移距离）
            float targetDistance = _offset.magnitude; // 保持初始设定的距离
            Vector3 desiredDirection = _cameraPivot.TransformDirection(Vector3.back); // 支点后方（基于当前旋转）
            Vector3 desiredPosition = _cameraPivot.position + desiredDirection * targetDistance;

            // 碰撞检测：避免相机穿模
            if (Physics.SphereCast(
            _cameraPivot.position,
            _sphereRadius,
            desiredDirection,
            out RaycastHit hit,
            targetDistance,
            _obstacleLayers))
            {
                // 遇到障碍物时拉近相机（但不小于最小距离）
                targetDistance = Mathf.Max(hit.distance - _sphereRadius, _minDistance);
            }

            // 计算最终位置并平滑过渡
            Vector3 finalPosition = _cameraPivot.position + desiredDirection * targetDistance;
            _thirdPersonCamera.transform.position = Vector3.Lerp(
            _thirdPersonCamera.transform.position,
            finalPosition,
            Time.deltaTime * _followSpeed * 2 // 相机位置调整更快，提升响应感
            );

            // 始终看向角色支点
            _thirdPersonCamera.transform.LookAt(_cameraPivot.position);
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
            if (_thirdPersonCamera != null)
            {
                Object.Destroy(_thirdPersonCamera.gameObject);
                _thirdPersonCamera = null;
            }
            if (_cameraPivot != null)
            {
                Object.Destroy(_cameraPivot.gameObject);
                _cameraPivot = null;
            }
            _targetTransform = null;
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
            if (_thirdPersonCamera != null)
                _thirdPersonCamera.enabled = true;
        }

        public override void OnDisable()
        {
            base.OnDisable();
            if (_thirdPersonCamera != null)
                _thirdPersonCamera.enabled = false;
        }

        public override void OnRemovedFromActor()
        {
            base.OnRemovedFromActor();
            OnDestroy();
        }
    }
}
