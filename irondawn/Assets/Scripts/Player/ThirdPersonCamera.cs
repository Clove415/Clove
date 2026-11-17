using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 越肩第三人称相机。挂在一个独立物体上，每帧由玩家朝向 + 俯仰驱动。
    /// 带一个最简碰撞回避：被墙体挡住时把镜头拉近，避免穿墙看到关卡外。
    /// 瞄准时同时做三件事：拉近距离、右移肩位、收窄视野（放大）。
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform head;
        public Transform playerRoot;

        public Vector3 shoulder = new Vector3(0.62f, 0.16f, 0f);
        public Vector3 aimShoulder = new Vector3(0.80f, 0.13f, 0f);

        public float hipDistance = 4.3f;
        public float aimDistance = 2.45f;
        public float minDistance = 0.85f;
        public float collisionRadius = 0.22f;
        public float distanceSmooth = 12f;
        public float shoulderSmooth = 10f;

        /// <summary>腰射视野。</summary>
        public float hipFov = 62f;

        /// <summary>瞄准视野。数值越小放大越明显，38 约等于 1.6 倍观感。</summary>
        public float aimFov = 38f;

        public float fovSmooth = 9f;

        /// <summary>探头时相机横向移动的最大距离。</summary>
        public float leanDistance = 0.8f;
        public float leanSmooth = 12f;

        /// <summary>由 PlayerController 写入：-1 左探头，+1 右探头，0 居中。</summary>
        public float Lean { get; set; }

        /// <summary>由 PlayerController 写入的俯仰角。</summary>
        public float Pitch { get; set; }

        /// <summary>
        /// 由 PlayerController 写入的水平朝向。
        /// 相机用自己的 yaw，而不是身体的朝向——身体在平移时会转向移动方向，
        /// 相机如果跟着身体转，视角就会被拖着甩。
        /// </summary>
        public float Yaw { get; set; }

        public bool IsAiming { get; set; }

        private Camera _camera;
        private float _distance;
        private float _currentFov;
        private Vector3 _currentShoulder;
        private float _currentLean;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            _distance = hipDistance;
            _currentFov = hipFov;
            _currentShoulder = shoulder;

            if (_camera != null) _camera.fieldOfView = hipFov;
        }

        /// <summary>肩位/探头偏移可能把镜头推进墙里，这里把偏移长度压到最近障碍之前。</summary>
        private Vector3 ClampShoulder(Vector3 origin, Vector3 shoulderWorld)
        {
            float len = shoulderWorld.magnitude;
            if (len < 0.01f) return shoulderWorld;

            Vector3 dir = shoulderWorld / len;

            RaycastHit[] hits = Physics.SphereCastAll(
                origin, collisionRadius * 0.85f, dir, len, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (col.transform.IsChildOf(playerRoot)) continue;
                len = Mathf.Min(len, hits[i].distance);
            }

            return dir * len;
        }

        void LateUpdate()
        {
            if (head == null || playerRoot == null) return;

            float dt = Time.deltaTime;

            float targetDistance = IsAiming ? aimDistance : hipDistance;
            _distance = Mathf.Lerp(_distance, targetDistance, 1f - Mathf.Exp(-distanceSmooth * dt));

            _currentLean = Mathf.Lerp(_currentLean, Mathf.Clamp(Lean, -1f, 1f), 1f - Mathf.Exp(-leanSmooth * dt));

            Vector3 targetShoulder = IsAiming ? aimShoulder : shoulder;
            targetShoulder += Vector3.right * (_currentLean * leanDistance);
            _currentShoulder = Vector3.Lerp(_currentShoulder, targetShoulder, 1f - Mathf.Exp(-shoulderSmooth * dt));

            float targetFov = IsAiming ? aimFov : hipFov;
            _currentFov = Mathf.Lerp(_currentFov, targetFov, 1f - Mathf.Exp(-fovSmooth * dt));
            if (_camera != null) _camera.fieldOfView = _currentFov;

            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);

            Vector3 shoulderWorld = rot * _currentShoulder;
            Vector3 anchor = head.position + ClampShoulder(head.position, shoulderWorld);
            Vector3 offset = rot * new Vector3(0f, 0f, -_distance);

            float allowed = _distance;
            Vector3 dir = offset.normalized;

            RaycastHit[] hits = Physics.SphereCastAll(
                anchor, collisionRadius, dir, _distance, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (col.transform.IsChildOf(playerRoot)) continue;
                if (hits[i].distance < allowed) allowed = hits[i].distance;
            }

            Vector3 finalPos = anchor + dir * Mathf.Max(allowed - 0.04f, minDistance);
            transform.position = finalPos;
            transform.rotation = rot;
        }
    }
}
