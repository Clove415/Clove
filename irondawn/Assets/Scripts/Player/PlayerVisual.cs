using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 玩家占位模型的下蹲与探头表现。
    /// 下蹲：腿缩短、上身下沉，脚底始终贴地。
    /// 探头：以腰为轴侧倾，头再额外偏一点，形成"头先探出去、身体跟上"的层次。
    /// 相机不在这里处理。
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        public Transform legs;
        public Transform waist;
        public Transform head;

        [Header("下蹲")]
        public float legsCrouchScale = 0.55f;
        public float waistCrouchDrop = 0.42f;

        [Header("探头")]
        public float waistLeanAngle = 14f;
        public float waistLeanShift = 0.09f;
        public float headLeanAngle = 6f;

        public float smooth = 14f;
        public float leanSmooth = 11f;

        private float _crouchT;
        private float _crouchTarget;
        private float _leanT;
        private float _leanTarget;

        private Vector3 _legsBasePos;
        private Vector3 _waistBasePos;
        private float _legsHalfHeight = 0.5f;
        private bool _captured;

        /// <summary>由装配器在挂好子物体之后调用，记录原始姿态。</summary>
        public void CaptureBase()
        {
            if (legs != null)
            {
                _legsBasePos = legs.localPosition;
                _legsHalfHeight = Mathf.Max(0.01f, legs.localPosition.y);
            }

            if (waist != null) _waistBasePos = waist.localPosition;

            _captured = true;
        }

        public void SetCrouch(bool crouching)
        {
            _crouchTarget = crouching ? 1f : 0f;
        }

        /// <summary>-1 左探头，+1 右探头，0 居中。</summary>
        public void SetLean(float lean)
        {
            _leanTarget = Mathf.Clamp(lean, -1f, 1f);
        }

        void Update()
        {
            if (!_captured) return;

            float dt = Time.deltaTime;
            _crouchT = Mathf.Lerp(_crouchT, _crouchTarget, 1f - Mathf.Exp(-smooth * dt));
            _leanT = Mathf.Lerp(_leanT, _leanTarget, 1f - Mathf.Exp(-leanSmooth * dt));

            ApplyLegs();
            ApplyWaist();
            ApplyHead();
        }

        private void ApplyLegs()
        {
            if (legs == null) return;

            float s = Mathf.Lerp(1f, legsCrouchScale, _crouchT);

            Vector3 scale = legs.localScale;
            scale.y = s;
            legs.localScale = scale;

            // 胶囊轴心在正中，缩放会把脚底一起抬高，这里压回地面
            Vector3 pos = _legsBasePos;
            pos.y = _legsBasePos.y - (1f - s) * _legsHalfHeight;
            legs.localPosition = pos;
        }

        private void ApplyWaist()
        {
            if (waist == null) return;

            // 下蹲下沉 + 探头横移
            Vector3 pos = _waistBasePos;
            pos.y = _waistBasePos.y - waistCrouchDrop * _crouchT;
            pos.x = _waistBasePos.x + _leanT * waistLeanShift;
            waist.localPosition = pos;

            // 以腰为轴侧倾。绕 Z 轴负角 = 向右倒，所以右探头取负值
            Vector3 euler = Vector3.zero;
            euler.z = -_leanT * waistLeanAngle;
            waist.localEulerAngles = euler;
        }

        private void ApplyHead()
        {
            if (head == null) return;

            // 头在腰的基础上再偏一点，探头的方向感更明确
            Vector3 euler = Vector3.zero;
            euler.z = -_leanT * headLeanAngle;
            head.localEulerAngles = euler;
        }
    }
}
