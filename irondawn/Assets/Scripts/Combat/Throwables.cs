using System.Collections.Generic;
using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 投掷物运行时。四种道具的表现取向参考 CS2：
    ///   烟雾：落地立刻起烟，半径约 5 米，持续 18 秒，纯粹挡视线；
    ///   高爆：1.6 秒引信，9 米内按距离衰减，中心最高 98 伤害；
    ///   燃烧：落地铺火，半径 5.5 米，持续 7 秒，站在火里持续掉血；
    ///   闪光：1.5 秒引信，按"是否背对 + 距离 + 有没有墙"决定致盲时长。
    ///
    /// 伤害与致盲都由"被打的一方"自己结算——和子弹同一套规则，
    /// 所以每台机器只处理本地玩家，不需要在主机上复制别人的血量。
    /// </summary>
    public static class Throwables
    {
        private const float GrenadeGravity = -22f;

        /// <summary>本地投出或从网络收到别人的投掷物时，都在这里生成弹体。</summary>
        public static void Spawn(ThrowableKind kind, Vector3 origin, Vector3 velocity, Transform parent, byte owner)
        {
            GameObject go = new GameObject("throwable_" + kind);
            if (parent != null) go.transform.SetParent(parent, false);

            go.transform.position = origin;

            ThrowableProjectile projectile = go.AddComponent<ThrowableProjectile>();
            projectile.Setup(kind, velocity, owner);
        }

        internal static void Detonate(ThrowableKind kind, Vector3 position, byte owner)
        {
            switch (kind)
            {
                case ThrowableKind.Smoke:
                    SpawnSmoke(position);
                    break;

                case ThrowableKind.He:
                    SpawnExplosion(position, owner);
                    break;

                case ThrowableKind.Fire:
                    SpawnFire(position, owner);
                    break;

                case ThrowableKind.Flash:
                    SpawnFlash(position);
                    break;
            }
        }

        internal static float Gravity { get { return GrenadeGravity; } }

        // ---------------- 视觉与效果 ----------------

        private static void SpawnSmoke(Vector3 position)
        {
            GameObject go = new GameObject("~smoke");
            go.transform.position = position;
            go.AddComponent<SmokeCloud>();
        }

        private static void SpawnExplosion(Vector3 position, byte owner)
        {
            // 伤害：本地玩家由自己结算
            ApplyExplosionDamage(position, 9f, 98f, owner);

            GameObject go = new GameObject("~explosion");
            go.transform.position = position;
            go.AddComponent<ExplosionBurst>();
        }

        private static void SpawnFire(Vector3 position, byte owner)
        {
            GameObject go = new GameObject("~fire");
            go.transform.position = position;

            FireZone zone = go.AddComponent<FireZone>();
            zone.owner = owner;
        }

        private static void SpawnFlash(Vector3 position)
        {
            ApplyFlashBlind(position);

            GameObject go = new GameObject("~flash");
            go.transform.position = position;
            go.AddComponent<FlashBurst>();
        }

        // ---------------- 结算 ----------------

        private static void ApplyExplosionDamage(Vector3 center, float radius, float maxDamage, byte owner)
        {
            ArenaRuntime runtime = ArenaRuntime.Instance;
            if (runtime == null) return;

            Health health = runtime.LocalHealth;
            Transform local = runtime.LocalPlayerTransform;
            if (health == null || local == null || !health.IsAlive) return;

            Vector3 origin = local.position + Vector3.up * 1f;
            float distance = Vector3.Distance(origin, center);
            if (distance > radius) return;

            // 距离衰减：贴脸接近满伤，边缘只剩零头
            float falloff = 1f - Mathf.Clamp01(distance / radius);
            float damage = maxDamage * Mathf.Pow(falloff, 0.85f);
            if (damage < 1f) return;

            runtime.NotifyLocalExplosion(damage, center, owner);
        }

        private static void ApplyFlashBlind(Vector3 position)
        {
            ArenaRuntime runtime = ArenaRuntime.Instance;
            if (runtime == null) return;

            Camera camera = runtime.ActiveCamera;
            Transform local = runtime.LocalPlayerTransform;
            Health health = runtime.LocalHealth;
            if (camera == null || local == null || health == null || !health.IsAlive) return;

            Vector3 eye = local.position + Vector3.up * 1.58f;
            Vector3 toFlash = position - eye;
            float distance = toFlash.magnitude;
            if (distance > 34f) return;

            // 墙体遮挡：闪光照不到背后的人
            if (distance > 0.5f)
            {
                RaycastHit hit;
                if (Physics.Raycast(eye, toFlash / distance, out hit, distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform != null && !hit.transform.IsChildOf(local)) return;
                }
            }

            float facing = Vector3.Dot(camera.transform.forward, toFlash.normalized);

            // 角度与距离只决定"白多久"，不决定"白多少"：
            // 一旦被闪到就是整屏全白，和 CS2 的观感一致。
            float angleFactor = Mathf.Clamp01((facing + 0.35f) / 1.35f);
            float distanceFactor = Mathf.Clamp01(1f - distance / 34f);
            float impact = angleFactor * distanceFactor;

            // 完全背对且离得远就当作没被闪到
            if (impact < 0.06f) return;

            float duration = Mathf.Lerp(1.0f, 4.5f, impact);
            runtime.ApplyBlind(duration, 1f);
        }
    }

    /// <summary>飞行中的投掷物：重力 + 球形扫掠，撞墙反弹，引信到点或落地即触发。</summary>
    public sealed class ThrowableProjectile : MonoBehaviour
    {
        private const float Radius = 0.085f;

        private ThrowableKind _kind;
        private Vector3 _velocity;
        private byte _owner;
        private float _fuse;
        private float _age;
        private bool _settled;

        public void Setup(ThrowableKind kind, Vector3 velocity, byte owner)
        {
            _kind = kind;
            _velocity = velocity;
            _owner = owner;
            _fuse = WeaponDefs.FuseSeconds(kind);

            BuildVisual();
        }

        private void BuildVisual()
        {
            Color color = WeaponDefs.ThrowableColor(_kind);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "body";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            body.transform.localScale = Vector3.one * (Radius * 2f);
            Palette.Paint(body, color);

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "cap";
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(transform, false);
            cap.transform.localPosition = new Vector3(0f, Radius * 0.9f, 0f);
            cap.transform.localScale = new Vector3(Radius * 1.1f, Radius * 0.35f, Radius * 1.1f);
            Palette.Paint(cap, new Color(0.30f, 0.30f, 0.32f));
        }

        private void Update()
        {
            if (_settled) return;

            _age += Time.deltaTime;
            _velocity += Vector3.up * (Throwables.Gravity * Time.deltaTime);

            Vector3 step = _velocity * Time.deltaTime;
            float length = step.magnitude;

            if (length > 0.0001f)
            {
                RaycastHit hit;
                if (Physics.SphereCast(transform.position, Radius, step / length, out hit, length,
                        ~0, QueryTriggerInteraction.Ignore) && !IsPlayer(hit.collider))
                {
                    transform.position = hit.point + hit.normal * Radius;
                    OnImpact(hit.normal);
                    return;
                }

                transform.position += step;
            }

            if (transform.position.y < -4f) Settle();

            if (_fuse > 0f && _age >= _fuse) Settle();
        }

        /// <summary>投掷物不与人碰撞：从手里飞出去那一下不该撞到自己。</summary>
        private static bool IsPlayer(Collider col)
        {
            return col != null && col.GetComponentInParent<IDamageable>() != null;
        }

        private void OnImpact(Vector3 normal)
        {
            // 烟雾弹与燃烧瓶要真正停下来才触发，
            // 否则擦到掩体侧面就会当场炸开，烟球中心就跑到了墙边上
            if (_fuse <= 0f)
            {
                if (_velocity.sqrMagnitude > 4f)
                {
                    Vector3 bounced = Vector3.Reflect(_velocity, normal) * 0.35f;
                    _velocity = bounced;
                    return;
                }

                Settle();
                return;
            }

            Vector3 v = Vector3.Reflect(_velocity, normal);
            _velocity = v * 0.42f;

            if (_velocity.sqrMagnitude < 2f) _velocity = Vector3.zero;
        }

        private void Settle()
        {
            if (_settled) return;
            _settled = true;

            // 严格采用道具本体的最终位置：不做抬升、不做下限裁剪，
            // 烟球中心 = 你看到的烟雾弹停下的那个点。
            Vector3 position = transform.position;

            ThrowableKind kind = _kind;
            byte owner = _owner;
            Destroy(gameObject);

            Throwables.Detonate(kind, position, owner);
        }
    }

    /// <summary>
    /// 烟雾：单个巨大烟球，18 秒后散去。
    ///
    /// 内外两层壳，这是让烟"从里面也挡视线"的关键：
    ///   · 外壳正常剔除（只画朝外的面）——站在外面看到的是一个灰球；
    ///   · 内壳反过来只画朝内的面——站在球里看到的是烟的内壁，而不是透过去看见外面。
    /// 只用一个双面材质在部分渲染管线上会漏（背面剔除关不掉），分成两层最稳。
    /// </summary>
    public sealed class SmokeCloud : MonoBehaviour
    {
        /// <summary>烟球半径：直径 9 米，落地后能把整个掩体区裹住。</summary>
        private const float Radius = 4.5f;

        private const float GrowSeconds = 1.4f;
        private const float LifeSeconds = 18f;
        private const float ShrinkSeconds = 1.2f;

        /// <summary>
        /// 球心相对道具实体的额外高度。取 0：烟球就以烟雾弹本体最后停下的位置为中心。
        /// </summary>
        private const float CenterHeight = 0f;

        /// <summary>内层稍微收一点，避免与外壳的深度打架（z-fighting）。</summary>
        private const float InnerShellScale = 0.985f;

        private Transform _outer;
        private Transform _inner;
        private Material _material;
        private Mesh _innerMesh;
        private float _age;

        private void Awake()
        {
            Color smoke = new Color(0.70f, 0.72f, 0.76f);

            // 两层壳，一层朝外、一层朝内，双向都挡视线
            _outer = CreateShell("smoke_outer", smoke, false);
            _inner = CreateShell("smoke_inner", smoke, true);
        }

        /// <summary>
        /// 造一层球壳。
        ///
        /// inward = true 时不能只靠"负缩放"或材质的剔除开关来翻面——
        /// Unity 会对负缩放做绕序补偿，而部分渲染管线会忽略材质的 _Cull，
        /// 两种做法都可能导致站在球里仍然看穿。
        /// 这里直接把网格的三角形绕序反过来、法线取反，让内表面成为正面，
        /// 属于几何层面的确定性做法，跟渲染管线无关。
        /// </summary>
        private Transform CreateShell(string name, Color color, bool inward)
        {
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shell.name = name;

            Collider col = shell.GetComponent<Collider>();
            if (col != null) Destroy(col);

            shell.transform.SetParent(transform, false);
            shell.transform.localPosition = new Vector3(0f, CenterHeight, 0f);
            shell.transform.localScale = Vector3.one * 0.2f;

            if (_material == null)
            {
                // 两层共用一个材质实例，销毁时只清一次
                _material = new Material(Palette.Lit(color));
                _material.SetFloat("_Cull", 0f);   // 双面，作为第二道保险
            }

            Renderer renderer = shell.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = _material;

            if (inward) FlipMeshInward(shell);

            return shell.transform;
        }

        private void FlipMeshInward(GameObject shell)
        {
            MeshFilter filter = shell.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;

            _innerMesh = Instantiate(filter.sharedMesh);
            _innerMesh.name = "smoke_inner_mesh";

            int[] triangles = _innerMesh.triangles;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int swap = triangles[i];
                triangles[i] = triangles[i + 2];
                triangles[i + 2] = swap;
            }

            _innerMesh.triangles = triangles;

            Vector3[] normals = _innerMesh.normals;

            for (int i = 0; i < normals.Length; i++)
                normals[i] = -normals[i];

            _innerMesh.normals = normals;
            _innerMesh.RecalculateBounds();

            filter.sharedMesh = _innerMesh;
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_innerMesh != null) Destroy(_innerMesh);
        }

        private void Update()
        {
            if (_outer == null || _inner == null) return;

            _age += Time.deltaTime;

            float grow = Mathf.Clamp01(_age / GrowSeconds);
            float shrink = Mathf.Clamp01((LifeSeconds - _age) / ShrinkSeconds);
            float scale = Radius * 2f * Mathf.Lerp(0.15f, 1f, grow) * Mathf.Max(0.02f, shrink);

            _outer.localScale = Vector3.one * scale;
            _inner.localScale = Vector3.one * (scale * InnerShellScale);

            if (_age >= LifeSeconds) Destroy(gameObject);
        }
    }

    public sealed class ExplosionBurst : MonoBehaviour
    {
        private GameObject _core;
        private GameObject _ring;
        private float _age;

        private void Awake()
        {
            _core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_core.GetComponent<Collider>());
            _core.transform.SetParent(transform, false);
            Palette.Paint(_core, new Color(1f, 0.86f, 0.52f));

            _ring = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_ring.GetComponent<Collider>());
            _ring.transform.SetParent(transform, false);
            Palette.Paint(_ring, new Color(0.95f, 0.55f, 0.22f));
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float k = Mathf.Clamp01(_age / 0.32f);

            _core.transform.localScale = Vector3.one * Mathf.Lerp(0.4f, 3.6f, k);
            _ring.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, 7.4f, k);

            if (_age >= 0.32f) Destroy(gameObject);
        }
    }

    /// <summary>燃烧：地面一圈火苗，站在里面每秒掉血。</summary>
    public sealed class FireZone : MonoBehaviour
    {
        private const float Radius = 5.5f;
        private const float LifeSeconds = 7f;
        private const float TickInterval = 0.35f;
        private const float TickDamage = 4.5f;

        /// <summary>投掷者席位，用来给击杀记分。</summary>
        public byte owner;

        private readonly List<Transform> _flames = new List<Transform>();
        private float _age;
        private float _nextTick;

        private void Awake()
        {
            Color hot = new Color(1f, 0.52f, 0.16f);
            Color warm = new Color(0.86f, 0.24f, 0.10f);

            for (int i = 0; i < 16; i++)
            {
                float angle = i * 0.78f;
                float ring = (i % 4) / 3f;

                GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flame.name = "flame";
                Destroy(flame.GetComponent<Collider>());
                flame.transform.SetParent(transform, false);
                flame.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * Radius * ring,
                    0.28f,
                    Mathf.Sin(angle) * Radius * ring);
                flame.transform.localScale = new Vector3(0.9f, 0.55f, 0.9f);
                flame.transform.localRotation = Quaternion.Euler(0f, angle * 57.3f, 0f);

                Palette.Paint(flame, i % 2 == 0 ? hot : warm);
                _flames.Add(flame.transform);
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;

            float flicker = 0.85f + Mathf.Sin(_age * 14f) * 0.15f;

            for (int i = 0; i < _flames.Count; i++)
            {
                Transform flame = _flames[i];
                if (flame == null) continue;

                float s = flicker * (0.75f + (i % 3) * 0.16f);
                flame.localScale = new Vector3(0.9f * s, 0.55f * s * 1.35f, 0.9f * s);
            }

            if (_age >= _nextTick) Tick();

            if (_age >= LifeSeconds) Destroy(gameObject);
        }

        private void Tick()
        {
            _nextTick = _age + TickInterval;

            ArenaRuntime runtime = ArenaRuntime.Instance;
            if (runtime == null) return;

            Health health = runtime.LocalHealth;
            Transform local = runtime.LocalPlayerTransform;
            if (health == null || local == null || !health.IsAlive) return;

            Vector3 flat = local.position - transform.position;
            flat.y = 0f;

            if (flat.sqrMagnitude > Radius * Radius) return;

            runtime.NotifyLocalFireDamage(TickDamage, owner);
        }
    }

    /// <summary>闪光爆点：一个极短的白球，真正的致盲已经走 ApplyBlind。</summary>
    public sealed class FlashBurst : MonoBehaviour
    {
        private GameObject _ball;
        private float _age;

        private void Awake()
        {
            _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(_ball.GetComponent<Collider>());
            _ball.transform.SetParent(transform, false);
            Palette.Paint(_ball, new Color(1f, 1f, 0.94f));
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float k = Mathf.Clamp01(_age / 0.22f);

            _ball.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 2.4f, k);

            if (_age >= 0.22f) Destroy(gameObject);
        }
    }
}
