using UnityEngine;

namespace Fokiga.Runtime.Gameplay
{
    [CreateAssetMenu(
        fileName = "CameraProfile",
        menuName = "Fokiga/Gameplay/相机配置")]
    public sealed class CameraProfile : ScriptableObject
    {
        public const string DefaultResourcesPath = "Config/Camera/DefaultCameraProfile";

        [Header("跟随")]
        [SerializeField]
        private Vector3 mOffset = new Vector3(0f, 2f, -4f);
        [SerializeField, Min(0f)]
        private float mFollowSpeed = 5f;
        [SerializeField]
        private float mLookAtHeight = 1.5f;

        [Header("旋转")]
        [SerializeField, Min(0f)]
        private float mRotationSpeed = 2f;
        [SerializeField]
        private float mMinVerticalAngle = -30f;
        [SerializeField]
        private float mMaxVerticalAngle = 60f;

        [Header("碰撞")]
        [SerializeField, Min(0f)]
        private float mSphereRadius = 0.3f;
        [SerializeField]
        private LayerMask mObstacleLayers = ~0;
        [SerializeField, Min(0f)]
        private float mMinDistance = 0.5f;

        [Header("相机")]
        [SerializeField]
        private CameraClearFlags mClearFlags = CameraClearFlags.Skybox;
        [SerializeField, Range(1f, 179f)]
        private float mFieldOfView = 60f;
        [SerializeField, Min(0.001f)]
        private float mNearClipPlane = 0.1f;
        [SerializeField, Min(0.001f)]
        private float mFarClipPlane = 1000f;
        [SerializeField]
        private float mDepth = -1f;

        public Vector3 Offset => mOffset;

        public float FollowSpeed => mFollowSpeed;

        public float LookAtHeight => mLookAtHeight;

        public float RotationSpeed => mRotationSpeed;

        public float MinVerticalAngle => mMinVerticalAngle;

        public float MaxVerticalAngle => mMaxVerticalAngle;

        public float SphereRadius => mSphereRadius;

        public LayerMask ObstacleLayers => mObstacleLayers;

        public float MinDistance => mMinDistance;

        public CameraClearFlags ClearFlags => mClearFlags;

        public float FieldOfView => mFieldOfView;

        public float NearClipPlane => mNearClipPlane;

        public float FarClipPlane => mFarClipPlane;

        public float Depth => mDepth;

        internal static CameraProfile CreateRuntimeFallback()
        {
            var profile = CreateInstance<CameraProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }

        private void OnValidate()
        {
            mFollowSpeed = Mathf.Max(0f, mFollowSpeed);
            mRotationSpeed = Mathf.Max(0f, mRotationSpeed);
            mSphereRadius = Mathf.Max(0f, mSphereRadius);
            mMinDistance = Mathf.Max(0f, mMinDistance);
            mNearClipPlane = Mathf.Max(0.001f, mNearClipPlane);
            mFarClipPlane = Mathf.Max(mNearClipPlane, mFarClipPlane);

            if (mMaxVerticalAngle < mMinVerticalAngle)
            {
                mMaxVerticalAngle = mMinVerticalAngle;
            }
        }
    }
}
