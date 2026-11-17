using UnityEngine;

namespace IronDawn.Multiplayer
{
    /// <summary>
    /// 本地玩家的网络出口：按固定频率上报位置/朝向/姿态/血量/手持武器，
    /// 并把每一发子弹、每一次近战、每一颗投掷物告诉其他人。
    /// 命中与否仍由射击端判定，扣血由被打的一方自己结算。
    /// </summary>
    public sealed class PlayerNetSync : MonoBehaviour
    {
        public PlayerController controller;
        public PlayerCombat combat;
        public Health health;

        private float _nextStateTime;

        void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            if (combat == null) combat = GetComponent<PlayerCombat>();
            if (health == null) health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (combat != null)
            {
                combat.ShotResolved += HandleShot;
                combat.ThrowResolved += HandleThrow;
            }
        }

        void OnDisable()
        {
            if (combat != null)
            {
                combat.ShotResolved -= HandleShot;
                combat.ThrowResolved -= HandleThrow;
            }
        }

        private void HandleShot(PlayerCombat.ShotInfo shot)
        {
            LanNetManager net = LanNetManager.Instance;
            if (net == null || !net.InMatch) return;

            ShotData d;
            d.shooter = net.LocalId;
            d.from = shot.from;
            d.to = shot.to;
            d.hit = shot.hitId;
            d.damage = shot.damage;
            d.kind = shot.kind;
            net.PublishShot(d);
        }

        private void HandleThrow(ThrowableData data)
        {
            LanNetManager net = LanNetManager.Instance;
            if (net == null || !net.InMatch) return;

            net.PublishThrowable(data);
        }

        void Update()
        {
            LanNetManager net = LanNetManager.Instance;
            if (net == null || !net.InMatch || controller == null) return;
            if (Time.time < _nextStateTime) return;

            _nextStateTime = Time.time + 1f / NetConfig.StateHz;

            bool alive = health == null || health.IsAlive;

            PlayerFlags flags = PlayerFlags.None;
            if (alive) flags |= PlayerFlags.Alive;
            if (controller.IsCrouching) flags |= PlayerFlags.Crouching;
            if (controller.IsAiming) flags |= PlayerFlags.Aiming;

            float lean = controller.LeanAmount;
            if (lean < -0.35f) flags |= PlayerFlags.LeanLeft;
            else if (lean > 0.35f) flags |= PlayerFlags.LeanRight;

            PlayerStateData d;
            d.id = net.LocalId;
            d.position = transform.position;
            d.yaw = transform.eulerAngles.y;
            d.pitch = controller.cameraRig != null ? controller.cameraRig.Pitch : 0f;
            d.flags = flags;
            d.health = health != null ? health.Normalized : 1f;
            d.weapon = combat != null ? (byte)combat.Slot : (byte)0;
            d.throwable = combat != null ? (byte)combat.Throwable : (byte)0;

            net.PublishState(d);
        }
    }
}
