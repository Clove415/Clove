using IronDawn.Multiplayer;
using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 玩家射击。从相机中心出发的射线，命中 IDamageable 就扣血。
    /// 只有一把全自动步枪——对枪的公平性全靠它，不需要武器系统。
    ///
    /// 每一发都会通过 ShotResolved 抛出落点与被命中的席位，
    /// 联机层据此把弹道广播出去、由被打的一方结算伤害。
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        /// <summary>单发结算结果。</summary>
        public struct ShotInfo
        {
            public Vector3 from;
            public Vector3 to;
            public byte hitId;
            public float damage;
        }

        public event System.Action<ShotInfo> ShotResolved;

        public Camera viewCamera;
        public Transform muzzle;
        public PlayerController controller;

        public float fireInterval = 0.105f;
        public float damage = 13f;
        public float range = 95f;

        public int magazineSize = 30;
        public float reloadTime = 1.65f;

        public float hipSpread = 1.7f;
        public float aimSpread = 0.28f;
        public float recoilPerShot = 0.32f;

        public LayerMask hitMask = ~0;

        public int Ammo { get; private set; }
        public bool Reloading { get; private set; }
        public float ReloadProgress
        {
            get
            {
                if (!Reloading) return 1f;
                return Mathf.Clamp01(1f - (_reloadEnd - Time.time) / reloadTime);
            }
        }

        private static readonly System.Comparison<RaycastHit> ByDistance =
            (a, b) => a.distance.CompareTo(b.distance);

        private float _nextFire;
        private float _reloadEnd;
        private float _fireBlockedUntil;

        /// <summary>短暂禁止开火：用于从暂停面板返回游戏时，避免把"点继续"的那一下当成扣扳机。</summary>
        public void SuppressFire(float seconds)
        {
            _fireBlockedUntil = Mathf.Max(_fireBlockedUntil, Time.time + Mathf.Max(0f, seconds));
        }

        void Awake()
        {
            if (controller == null) controller = GetComponent<PlayerController>();
            Ammo = magazineSize;
        }

        void Update()
        {
            if (Reloading && Time.time >= _reloadEnd)
            {
                Reloading = false;
                Ammo = magazineSize;
            }

            if (controller != null && controller.InputLocked) return;
            if (controller != null && controller.health != null && !controller.health.IsAlive) return;

            if (Input.GetKeyDown(KeyCode.R)) StartReload();
            if (Ammo <= 0 && !Reloading) StartReload();
            if (Reloading) return;

            if (Input.GetMouseButton(0))
            {
                if (!controller.IsAiming && Cursor.lockState != CursorLockMode.Locked) return;
                TryFire();
            }
        }

        public void StartReload()
        {
            if (Reloading || Ammo >= magazineSize) return;
            Reloading = true;
            _reloadEnd = Time.time + reloadTime;
        }

        private void TryFire()
        {
            if (Time.time < _nextFire || Ammo <= 0) return;
            if (Time.time < _fireBlockedUntil) return;
            _nextFire = Time.time + fireInterval;
            Ammo--;

            Vector3 origin = viewCamera != null ? viewCamera.transform.position : transform.position;
            Vector3 dir = viewCamera != null ? viewCamera.transform.forward : transform.forward;

            float spread = (controller != null && controller.IsAiming) ? aimSpread : hipSpread;
            dir = MathUtil.Spread(dir, spread);

            // 从相机前方一点点开始，避免射线打到玩家自己身上
            Vector3 start = origin + dir * 0.55f;

            RaycastHit[] hits = Physics.RaycastAll(start, dir, range, hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, ByDistance);

            Vector3 end = start + dir * range;
            byte hitId = 0;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue;

                end = hits[i].point;
                IDamageable target = col.GetComponentInParent<IDamageable>();
                if (target != null && target.IsAlive)
                {
                    INetHitTarget netTarget = target as INetHitTarget;
                    if (netTarget != null) hitId = netTarget.NetId;

                    target.ApplyDamage(damage, hits[i].point, hits[i].normal, gameObject);
                    Tracer.Flash(hits[i].point, 0.1f, Palette.Muzzle, 0.06f);
                }
                break;
            }

            Vector3 tracerStart = muzzle != null ? muzzle.position : start;
            Tracer.Draw(tracerStart, end, Palette.PlayerTracer, 0.032f, 0.05f);

            if (ShotResolved != null)
            {
                ShotInfo info;
                info.from = tracerStart;
                info.to = end;
                info.hitId = hitId;
                info.damage = damage;
                ShotResolved(info);
            }

            if (controller != null) controller.AddRecoil(recoilPerShot);
        }
    }
}
