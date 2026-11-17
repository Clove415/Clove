using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 玩家装配工厂。本地玩家和远端玩家都用同一套灰盒形体，
    /// 只差"有没有输入、有没有相机、有没有真实血量"——所以形体构造只写一次。
    /// 颜色由席位号决定，对阵双方一眼能分清谁是谁。
    /// </summary>
    public static class PlayerFactory
    {
        public struct LocalRig
        {
            public GameObject root;
            public PlayerController controller;
            public PlayerCombat combat;
            public Health health;
            public PlayerVisual visual;
            public WeaponModels weaponModels;
            public ThirdPersonCamera cameraRig;
            public Camera camera;
            public Transform head;
            public Transform weaponPivot;
            public Transform muzzle;
        }

        public struct RemoteRig
        {
            public GameObject root;
            public PlayerVisual visual;
            public WeaponModels weaponModels;
            public Transform head;
            public Transform weaponPivot;
        }

        /// <summary>本地玩家：带碰撞、血量、输入、越肩相机和一把枪。</summary>
        public static LocalRig CreateLocal(Transform parent, Vector3 position, float yaw, int slot)
        {
            Color body = Palette.TeamBody(slot);
            Color accent = Palette.TeamAccent(slot);

            GameObject root = new GameObject("Player");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.34f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 52f;
            cc.stepOffset = 0.4f;
            cc.skinWidth = 0.03f;

            Health health = root.AddComponent<Health>();
            health.maxHealth = 100f;
            health.current = 100f;

            PlayerController controller = root.AddComponent<PlayerController>();
            controller.health = health;
            controller.visual = BuildVisual(root.transform, body, accent);

            GameObject head = new GameObject("head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.58f, 0f);
            controller.head = head.transform;

            GameObject weapon = new GameObject("weaponPivot");
            weapon.transform.SetParent(root.transform, false);
            weapon.transform.localPosition = new Vector3(0.3f, 1.22f, 0.42f);
            controller.weaponPivot = weapon.transform;

            GameObject muzzle = new GameObject("muzzle");
            muzzle.transform.SetParent(weapon.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.03f, 0.62f);

            // 手里的武器模型：步枪 / 手枪 / 匕首 / 四种投掷物，按槽位切换
            WeaponModels models = WeaponModels.Build(weapon.transform);

            GameObject camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(parent, false);
            camGo.tag = "MainCamera";

            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f);
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.06f;
            cam.farClipPlane = 320f;

            camGo.AddComponent<AudioListener>();

            ThirdPersonCamera rig = camGo.AddComponent<ThirdPersonCamera>();
            rig.head = head.transform;
            rig.playerRoot = root.transform;
            controller.cameraRig = rig;

            PlayerCombat combat = root.AddComponent<PlayerCombat>();
            combat.viewCamera = cam;
            combat.muzzle = muzzle.transform;
            combat.head = head.transform;
            combat.controller = controller;
            combat.models = models;

            LocalRig result;
            result.root = root;
            result.controller = controller;
            result.combat = combat;
            result.health = health;
            result.visual = controller.visual;
            result.weaponModels = models;
            result.cameraRig = rig;
            result.camera = cam;
            result.head = head.transform;
            result.weaponPivot = weapon.transform;
            result.muzzle = muzzle.transform;
            return result;
        }

        /// <summary>远端玩家：只有形体和碰撞盒，位置由网络快照驱动。</summary>
        public static RemoteRig CreateRemote(Transform parent, string name, Vector3 position, float yaw, int slot)
        {
            Color body = Palette.TeamBody(slot);
            Color accent = Palette.TeamAccent(slot);

            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.34f;
            col.center = new Vector3(0f, 0.9f, 0f);

            PlayerVisual visual = BuildVisual(root.transform, body, accent);

            GameObject head = new GameObject("head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.58f, 0f);

            GameObject weapon = new GameObject("weaponPivot");
            weapon.transform.SetParent(root.transform, false);
            weapon.transform.localPosition = new Vector3(0.3f, 1.22f, 0.42f);

            WeaponModels models = WeaponModels.Build(weapon.transform);

            RemoteRig result;
            result.root = root;
            result.visual = visual;
            result.weaponModels = models;
            result.head = head.transform;
            result.weaponPivot = weapon.transform;
            return result;
        }

        /// <summary>灰盒人形。origin 是脚底世界坐标，所有部件都以它为基准算。</summary>
        /// <summary>
        /// 灰盒人形骨架。视觉根在脚底，所有部件用局部坐标装配：
        ///   visual → hips →（躯干、头、两条手臂）＋（两条腿）
        /// 四肢都是"关节 + 部件"的结构，由 PlayerVisual 按关节旋转做动作。
        /// </summary>
        private static PlayerVisual BuildVisual(Transform parent, Color body, Color accent)
        {
            Transform root = new GameObject("visual").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;

            PlayerVisual pv = root.gameObject.AddComponent<PlayerVisual>();

            // ---------------- 腰 / 上身 ----------------
            Transform hips = Node(root, "hips", new Vector3(0f, 0.95f, 0f));
            pv.hips = hips;

            Part(hips, "torso", new Vector3(0f, 0.30f, 0f), new Vector3(0.52f, 0.58f, 0.32f), body);
            Part(hips, "shoulder_L", new Vector3(-0.33f, 0.47f, 0f), new Vector3(0.16f, 0.16f, 0.24f), Palette.PlayerGear);
            Part(hips, "shoulder_R", new Vector3(0.33f, 0.47f, 0f), new Vector3(0.16f, 0.16f, 0.24f), Palette.PlayerGear);

            // 胸腔裸露的金属骨架与断线缆
            Part(hips, "exposed_core", new Vector3(0f, 0.30f, 0.17f), new Vector3(0.20f, 0.17f, 0.05f), Palette.Muzzle);
            Part(hips, "cable", new Vector3(0f, 0.16f, 0.15f), new Vector3(0.05f, 0.20f, 0.05f), Palette.PlayerGear);

            // ---------------- 头 ----------------
            Transform head = Node(hips, "head", new Vector3(0f, 0.58f, 0f));
            pv.head = head;

            Part(head, "neck", new Vector3(0f, 0.02f, 0f), new Vector3(0.12f, 0.10f, 0.12f), Palette.PlayerGear);
            Part(head, "head_mesh", new Vector3(0f, 0.15f, 0f), Vector3.one * 0.32f, body, PrimitiveType.Sphere);
            Part(head, "visor", new Vector3(0f, 0.16f, 0.145f), new Vector3(0.225f, 0.05f, 0.05f), accent);

            // ---------------- 手臂 ----------------
            pv.armL = BuildArm(hips, "arm_L", -0.36f, body, out pv.forearmL);
            pv.armR = BuildArm(hips, "arm_R", 0.36f, body, out pv.forearmR);

            // ---------------- 腿 ----------------
            pv.legL = BuildLeg(hips, "leg_L", -0.13f, out pv.shinL);
            pv.legR = BuildLeg(hips, "leg_R", 0.13f, out pv.shinR);

            pv.CaptureBase();
            return pv;
        }

        private static Transform BuildArm(Transform hips, string name, float side, Color body, out Transform forearm)
        {
            Transform arm = Node(hips, name, new Vector3(side, 0.45f, 0f));

            Part(arm, "upper", new Vector3(0f, -0.16f, 0f), new Vector3(0.13f, 0.32f, 0.14f), Palette.PlayerGear);

            forearm = Node(arm, name + "_fore", new Vector3(0f, -0.32f, 0f));
            Part(forearm, "lower", new Vector3(0f, -0.14f, 0f), new Vector3(0.11f, 0.28f, 0.12f), Palette.PlayerGear);
            Part(forearm, "hand", new Vector3(0f, -0.31f, 0.02f), new Vector3(0.10f, 0.10f, 0.13f), body);

            return arm;
        }

        private static Transform BuildLeg(Transform hips, string name, float side, out Transform shin)
        {
            Transform leg = Node(hips, name, new Vector3(side, -0.02f, 0f));

            Part(leg, "thigh", new Vector3(0f, -0.21f, 0f), new Vector3(0.17f, 0.42f, 0.19f), Palette.PlayerGear);

            shin = Node(leg, name + "_shin", new Vector3(0f, -0.42f, 0f));
            Part(shin, "calf", new Vector3(0f, -0.19f, 0f), new Vector3(0.15f, 0.38f, 0.17f), Palette.PlayerGear);
            Part(shin, "foot", new Vector3(0f, -0.39f, 0.06f), new Vector3(0.16f, 0.08f, 0.26f), Palette.PlayerGear);

            return leg;
        }

        private static Transform Node(Transform parent, string name, Vector3 localPosition)
        {
            GameObject node = new GameObject(name);
            node.transform.SetParent(parent, false);
            node.transform.localPosition = localPosition;
            return node.transform;
        }

        private static GameObject Part(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Color color)
        {
            return Part(parent, name, localPosition, localScale, color, PrimitiveType.Cube);
        }

        private static GameObject Part(Transform parent, string name, Vector3 localPosition,
            Vector3 localScale, Color color, PrimitiveType type)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;

            Collider col = part.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);

            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Palette.Paint(part, color);
            return part;
        }
        }
        }
