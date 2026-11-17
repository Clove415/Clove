using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 玩家灰盒模型的骨架与动作。
    ///
    /// 层级（视觉根在脚底，y 向上，z 朝前）：
    ///   visual
    ///     hips            腰／上身枢轴：下蹲下沉、探头侧倾
    ///       chest 部件、head
    ///       arm_L/R  → forearm_L/R   （肩、肘关节）
    ///       leg_L/R  → shin_L/R      （髋、膝关节）
    ///
    /// 四肢是按关节旋转的，不是缩放贴图：
    ///   · 走路时大腿前后摆、膝盖回勾、手臂反向摆，步频随速度变化；
    ///   · 下蹲时髋部下沉 + 屈膝，脚仍贴近地面；
    ///   · 探头时腰和头再额外侧倾，形成"头先探出去、身体跟上"的层次。
    ///
    /// 本地玩家和远端玩家共用这一套：持有者每帧写 Speed / Grounded / Aiming 即可。
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        public Transform hips;
        public Transform head;

        public Transform armL;
        public Transform armR;
        public Transform forearmL;
        public Transform forearmR;

        public Transform legL;
        public Transform legR;
        public Transform shinL;
        public Transform shinR;

        /// <summary>水平速度（米/秒），由持有者每帧写入。</summary>
        public float Speed;

        public bool Grounded = true;
        public bool Aiming;

        [Header("下蹲")]
        public float hipsCrouchDrop = 0.33f;
        public float crouchThigh = 52f;
        public float crouchShin = 96f;

        [Header("行走")]
        public float strideDegrees = 34f;
        public float shinBendDegrees = 46f;
        public float armSwingDegrees = 26f;
        public float referenceSpeed = 6.4f;

        [Header("探头")]
        public float waistLeanAngle = 14f;
        public float waistLeanShift = 0.09f;
        public float headLeanAngle = 6f;

        /// <summary>当前手持的槽位，决定双臂的持械姿势。</summary>
        public WeaponSlot HoldPose { get; private set; }

        private float _crouch;
        private float _crouchTarget;
        private float _lean;
        private float _leanTarget;
        private float _phase;
        private float _stride;
        private float _airborne;

        private Vector3 _hipsBase;
        private bool _captured;

        /// <summary>由装配器在搭好骨架之后调用，记录原始姿态。</summary>
        public void CaptureBase()
        {
            if (hips != null) _hipsBase = hips.localPosition;
            _captured = true;
        }

        public void SetCrouch(bool crouching)
        {
            _crouchTarget = crouching ? 1f : 0f;
        }

        /// <summary>切换持械姿势：步枪 / 手枪 / 匕首 / 投掷物，各自的双臂角度不同。</summary>
        public void SetHoldPose(WeaponSlot slot)
        {
            HoldPose = slot;
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
            if (dt <= 0f) return;

            _crouch = Mathf.Lerp(_crouch, _crouchTarget, 1f - Mathf.Exp(-13f * dt));
            _lean = Mathf.Lerp(_lean, _leanTarget, 1f - Mathf.Exp(-11f * dt));

            // 步幅随速度收敛；停下时相位不再推进，四肢回到中立位
            float speed01 = Mathf.Clamp01(Speed / Mathf.Max(0.01f, referenceSpeed));
            _stride = Mathf.Lerp(_stride, speed01, 1f - Mathf.Exp(-9f * dt));

            if (Grounded) _phase += Speed * 1.9f * dt;
            else _phase += Speed * 1.2f * dt;

            _airborne = Mathf.Lerp(_airborne, Grounded ? 0f : 1f, 1f - Mathf.Exp(-10f * dt));

            ApplyHips();
            ApplyLegs();
            ApplyArms();
            ApplyHead();
        }

        // ---------------- 各部位 ----------------

        private void ApplyHips()
        {
            if (hips == null) return;

            Vector3 pos = _hipsBase;
            pos.y = _hipsBase.y - hipsCrouchDrop * _crouch;
            pos.x = _hipsBase.x + _lean * waistLeanShift;
            hips.localPosition = pos;

            // 下蹲时上身略前倾，探头时绕 Z 轴侧倒（右探头取负角）
            float pitch = 12f * _crouch;
            float roll = -_lean * waistLeanAngle;
            hips.localEulerAngles = new Vector3(pitch, 0f, roll);
        }

        private void ApplyLegs()
        {
            float swing = Mathf.Sin(_phase) * strideDegrees * _stride;

            // 下蹲时两腿同时前抬、小腿回勾，脚因此仍落在髋部下方
            float crouchThighAngle = crouchThigh * _crouch;

            SetPitch(legL, swing + crouchThighAngle);
            SetPitch(legR, -swing + crouchThighAngle);

            // 摆动到身后的那条腿回勾更深，膝盖才有"折"的感觉
            float bendL = Mathf.Max(0f, -swing) * shinBendDegrees / Mathf.Max(1f, strideDegrees);
            float bendR = Mathf.Max(0f, swing) * shinBendDegrees / Mathf.Max(1f, strideDegrees);

            SetPitch(shinL, -(bendL * _stride + crouchShin * _crouch));
            SetPitch(shinR, -(bendR * _stride + crouchShin * _crouch));
        }

        /// <summary>
        /// 双臂姿势。每个槽位给出一组"肩俯仰 / 肩偏航 / 肘弯曲"，
        /// 再叠加走路摆臂与瞄准抬高，尽量贴近真实的持械姿态：
        ///   · 步枪：右手握把（肘部收），左手前伸托护木；
        ///   · 手枪：双手前伸，肘几乎伸直；
        ///   · 匕首：右臂屈肘提在胸前，左臂自然下垂；
        ///   · 投掷物：右臂后引蓄力，左臂前伸平衡。
        /// </summary>
        private void ApplyArms()
        {
            float swing = Mathf.Sin(_phase) * armSwingDegrees * _stride;

            float rPitch, rYaw, rElbow;
            float lPitch, lYaw, lElbow;

            switch (HoldPose)
            {
                case WeaponSlot.Primary:
                    rPitch = 62f; rYaw = -12f; rElbow = -40f;
                    lPitch = 74f; lYaw = 26f; lElbow = -54f;
                    break;

                case WeaponSlot.Secondary:
                    rPitch = 76f; rYaw = -8f; rElbow = -16f;
                    lPitch = 70f; lYaw = 22f; lElbow = -30f;
                    break;

                case WeaponSlot.Melee:
                    rPitch = 44f; rYaw = -16f; rElbow = -78f;
                    lPitch = -6f; lYaw = 4f; lElbow = -18f;
                    break;

                default:
                    rPitch = 24f; rYaw = -18f; rElbow = -96f;
                    lPitch = 30f; lYaw = 10f; lElbow = -42f;
                    break;
            }

            // 瞄准时右臂再抬高一点，枪口更贴近准心
            if (Aiming)
            {
                rPitch += 9f;
                rElbow -= 7f;
                lPitch += 5f;
            }

            // 摆臂只加在非持械侧和双腿上：持枪的手要保持稳定，不能跟着甩
            SetJoint(armR, rPitch - swing * 0.22f, rYaw, 0f);
            SetJoint(forearmR, rElbow, 0f, 0f);

            SetJoint(armL, lPitch + swing * 0.55f, lYaw, 0f);
            SetJoint(forearmL, lElbow, 0f, 0f);
        }

        private void SetJoint(Transform joint, float pitch, float yaw, float roll)
        {
            if (joint == null) return;

            joint.localEulerAngles = new Vector3(pitch, yaw, roll);
        }

        private void ApplyHead()
        {
            if (head == null) return;

            // 头在腰的基础上再偏一点，探头方向更明确
            head.localEulerAngles = new Vector3(-10f * _crouch + 6f * _airborne, 0f, -_lean * headLeanAngle);
        }

        private static void SetPitch(Transform joint, float degrees)
        {
            if (joint == null) return;

            Vector3 euler = joint.localEulerAngles;
            euler.x = degrees;
            euler.y = 0f;
            euler.z = 0f;
            joint.localEulerAngles = euler;
        }
    }
}
