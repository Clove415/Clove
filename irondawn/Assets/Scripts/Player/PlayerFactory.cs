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
            controller.visual = BuildVisual(root.transform, position, body, accent);

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
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.5f);

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
            combat.controller = controller;

            LocalRig result;
            result.root = root;
            result.controller = controller;
            result.combat = combat;
            result.health = health;
            result.visual = controller.visual;
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

            PlayerVisual visual = BuildVisual(root.transform, position, body, accent);

            GameObject head = new GameObject("head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.58f, 0f);

            GameObject weapon = new GameObject("weaponPivot");
            weapon.transform.SetParent(root.transform, false);
            weapon.transform.localPosition = new Vector3(0.3f, 1.22f, 0.42f);

            RemoteRig result;
            result.root = root;
            result.visual = visual;
            result.head = head.transform;
            result.weaponPivot = weapon.transform;
            return result;
        }

        /// <summary>灰盒人形。origin 是脚底世界坐标，所有部件都以它为基准算。</summary>
        private static PlayerVisual BuildVisual(Transform parent, Vector3 origin, Color body, Color accent)
        {
            Transform root = new GameObject("visual").transform;
            root.SetParent(parent, false);
            root.localPosition = Vector3.zero;

            // ---- 腿：下蹲时缩短，脚底不动 ----
            Transform legs = new GameObject("legs").transform;
            legs.SetParent(root, false);
            legs.localPosition = new Vector3(0f, 0.5f, 0f);
            Prim.Capsule("legs_mesh", legs, origin + Vector3.up * 0.5f, 0.26f, 1.0f, Palette.PlayerGear, false);

            // ---- 腰：上半身的旋转轴心，探头就是绕它倾斜 ----
            Transform waist = new GameObject("waist").transform;
            waist.SetParent(root, false);
            waist.localPosition = new Vector3(0f, 0.95f, 0f);

            Vector3 w = origin + Vector3.up * 0.95f;

            Prim.Box("torso_mesh", waist, w + Vector3.up * 0.33f,
                new Vector3(0.56f, 0.64f, 0.36f), body, false);

            Prim.Box("shoulder_L", waist, w + Vector3.up * 0.47f + Vector3.right * -0.34f,
                new Vector3(0.18f, 0.20f, 0.26f), Palette.PlayerGear, false);
            Prim.Box("shoulder_R", waist, w + Vector3.up * 0.47f + Vector3.right * 0.34f,
                new Vector3(0.18f, 0.20f, 0.26f), Palette.PlayerGear, false);

            // ---- 头：独立轴心，探头时在腰的基础上再偏 ----
            Transform head = new GameObject("head").transform;
            head.SetParent(waist, false);
            head.localPosition = new Vector3(0f, 0.76f, 0f);

            Vector3 hp = origin + Vector3.up * 1.71f;

            Prim.Sphere("head_mesh", head, hp, 0.21f, body, false);
            Prim.Box("visor", head, hp + Vector3.forward * 0.17f,
                new Vector3(0.26f, 0.06f, 0.05f), accent, false);

            // ---- 枪与胸腔 ----
            Prim.Box("rifle", waist, w + Vector3.up * 0.27f + Vector3.forward * 0.42f,
                new Vector3(0.08f, 0.10f, 0.66f), Palette.PlayerGear, false);

            // 裸露的金属骨架与断线缆
            Prim.Box("exposed_core", waist, w + Vector3.up * 0.27f + Vector3.forward * 0.20f,
                new Vector3(0.20f, 0.17f, 0.06f), Palette.Muzzle, false);
            Prim.Box("cable", waist, w + Vector3.up * 0.13f + Vector3.forward * 0.17f,
                new Vector3(0.05f, 0.20f, 0.05f), Palette.PlayerGear, false);

            PlayerVisual pv = root.gameObject.AddComponent<PlayerVisual>();
            pv.legs = legs;
            pv.waist = waist;
            pv.head = head;
            pv.CaptureBase();
            return pv;
        }
    }
}
