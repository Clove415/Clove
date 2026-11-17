using UnityEngine;

namespace IronDawn
{
    /// <summary>
    /// 玩家手里的武器模型。全部是运行时用基本体拼的灰盒件：
    /// 步枪、手枪、匕首各一个，四种投掷物各一个，按当前槽位只显示一个。
    ///
    /// 挂点是 weaponPivot（右手位置，随瞄准俯仰旋转），所以武器会跟着视线抬落，
    /// 不再是贴在腰上的静态方盒。
    /// </summary>
    public sealed class WeaponModels : MonoBehaviour
    {
        private GameObject _rifle;
        private GameObject _pistol;
        private GameObject _knife;
        private readonly GameObject[] _throwables = new GameObject[WeaponDefs.ThrowableKindCount];

        private WeaponSlot _slot = WeaponSlot.Primary;
        private ThrowableKind _kind = ThrowableKind.Smoke;
        private bool _built;

        public static WeaponModels Build(Transform pivot)
        {
            WeaponModels models = pivot.gameObject.AddComponent<WeaponModels>();
            models.Create();
            models.Show(WeaponSlot.Primary, ThrowableKind.Smoke);
            return models;
        }

        public void Show(WeaponSlot slot, ThrowableKind kind)
        {
            if (!_built) return;

            _slot = slot;
            _kind = kind;

            SetActive(_rifle, slot == WeaponSlot.Primary);
            SetActive(_pistol, slot == WeaponSlot.Secondary);
            SetActive(_knife, slot == WeaponSlot.Melee);

            for (int i = 0; i < _throwables.Length; i++)
                SetActive(_throwables[i], slot == WeaponSlot.Throwable && i == (int)kind);
        }

        private static void SetActive(GameObject go, bool on)
        {
            if (go != null && go.activeSelf != on) go.SetActive(on);
        }

        // ---------------- 建模 ----------------

        private void Create()
        {
            Transform root = transform;

            BuildRifle(root);
            BuildPistol(root);
            BuildKnife(root);

            for (int i = 0; i < WeaponDefs.ThrowableKindCount; i++)
                _throwables[i] = BuildThrowable(root, (ThrowableKind)i);

            _built = true;
            Show(_slot, _kind);
        }

        private void BuildRifle(Transform root)
        {
            _rifle = Root(root, "weapon_rifle");
            Transform t = _rifle.transform;

            Color body = new Color(0.16f, 0.17f, 0.19f);
            Color metal = new Color(0.28f, 0.29f, 0.32f);
            Color grip = new Color(0.11f, 0.12f, 0.14f);

            Part(t, PrimitiveType.Cube, "receiver", new Vector3(0f, 0.02f, 0.14f), new Vector3(0.075f, 0.10f, 0.42f), body);
            Part(t, PrimitiveType.Cube, "handguard", new Vector3(0f, 0.02f, 0.40f), new Vector3(0.060f, 0.070f, 0.26f), metal);
            Part(t, PrimitiveType.Cylinder, "barrel", new Vector3(0f, 0.03f, 0.62f), new Vector3(0.032f, 0.150f, 0.032f), metal, new Vector3(90f, 0f, 0f));
            Part(t, PrimitiveType.Cylinder, "brake", new Vector3(0f, 0.03f, 0.755f), new Vector3(0.048f, 0.030f, 0.048f), grip, new Vector3(90f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "magazine", new Vector3(0f, -0.095f, 0.10f), new Vector3(0.050f, 0.145f, 0.090f), grip, new Vector3(12f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "stock", new Vector3(0f, 0.005f, -0.115f), new Vector3(0.058f, 0.088f, 0.22f), body);
            Part(t, PrimitiveType.Cube, "grip", new Vector3(0f, -0.075f, -0.01f), new Vector3(0.045f, 0.11f, 0.055f), grip, new Vector3(18f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "sight_back", new Vector3(0f, 0.088f, 0.06f), new Vector3(0.034f, 0.042f, 0.048f), metal);
            Part(t, PrimitiveType.Cube, "sight_front", new Vector3(0f, 0.080f, 0.455f), new Vector3(0.024f, 0.048f, 0.030f), metal);
        }

        private void BuildPistol(Transform root)
        {
            _pistol = Root(root, "weapon_pistol");
            Transform t = _pistol.transform;

            Color body = new Color(0.17f, 0.18f, 0.21f);
            Color metal = new Color(0.30f, 0.31f, 0.34f);

            Part(t, PrimitiveType.Cube, "slide", new Vector3(0f, 0.035f, 0.145f), new Vector3(0.050f, 0.070f, 0.235f), body);
            Part(t, PrimitiveType.Cube, "frame", new Vector3(0f, -0.020f, 0.125f), new Vector3(0.044f, 0.048f, 0.180f), metal);
            Part(t, PrimitiveType.Cube, "grip", new Vector3(0f, -0.100f, 0.045f), new Vector3(0.044f, 0.150f, 0.070f), body, new Vector3(14f, 0f, 0f));
            Part(t, PrimitiveType.Cylinder, "barrel", new Vector3(0f, 0.035f, 0.272f), new Vector3(0.024f, 0.022f, 0.024f), metal, new Vector3(90f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "sight", new Vector3(0f, 0.078f, 0.225f), new Vector3(0.016f, 0.020f, 0.020f), metal);
        }

        private void BuildKnife(Transform root)
        {
            _knife = Root(root, "weapon_knife");
            Transform t = _knife.transform;

            Color blade = new Color(0.62f, 0.64f, 0.68f);
            Color handle = new Color(0.12f, 0.13f, 0.15f);
            Color guard = new Color(0.22f, 0.23f, 0.26f);

            Part(t, PrimitiveType.Cube, "blade", new Vector3(0f, 0.030f, 0.205f), new Vector3(0.016f, 0.048f, 0.250f), blade);
            Part(t, PrimitiveType.Cube, "tip", new Vector3(0f, 0.030f, 0.335f), new Vector3(0.014f, 0.028f, 0.060f), blade, new Vector3(0f, 0f, 0f));
            Part(t, PrimitiveType.Cube, "guard", new Vector3(0f, 0.024f, 0.072f), new Vector3(0.070f, 0.020f, 0.028f), guard);
            Part(t, PrimitiveType.Cube, "handle", new Vector3(0f, 0.010f, -0.005f), new Vector3(0.038f, 0.052f, 0.130f), handle);
        }

        private GameObject BuildThrowable(Transform root, ThrowableKind kind)
        {
            GameObject go = Root(root, "weapon_throwable_" + kind);
            Transform t = go.transform;
            Color color = WeaponDefs.ThrowableColor(kind);
            Color cap = new Color(0.32f, 0.32f, 0.34f);

            Part(t, PrimitiveType.Sphere, "body", new Vector3(0f, 0.030f, 0.115f), new Vector3(0.130f, 0.135f, 0.130f), color);

            if (kind == ThrowableKind.Fire)
            {
                // 燃烧瓶：瓶颈 + 破布条
                Part(t, PrimitiveType.Cylinder, "neck", new Vector3(0f, 0.115f, 0.115f), new Vector3(0.045f, 0.045f, 0.045f), color);
                Part(t, PrimitiveType.Cube, "rag", new Vector3(0f, 0.165f, 0.115f), new Vector3(0.030f, 0.055f, 0.030f), new Color(0.55f, 0.48f, 0.32f), new Vector3(0f, 24f, 0f));
            }
            else
            {
                Part(t, PrimitiveType.Cylinder, "cap", new Vector3(0f, 0.100f, 0.115f), new Vector3(0.048f, 0.030f, 0.048f), cap);
                Part(t, PrimitiveType.Cube, "lever", new Vector3(0.034f, 0.115f, 0.100f), new Vector3(0.016f, 0.070f, 0.016f), cap, new Vector3(0f, 0f, 18f));
            }

            return go;
        }

        private static GameObject Root(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }

        private static GameObject Part(Transform parent, PrimitiveType type, string name,
            Vector3 localPosition, Vector3 localScale, Color color)
        {
            return Part(parent, type, name, localPosition, localScale, color, Vector3.zero);
        }

        private static GameObject Part(Transform parent, PrimitiveType type, string name,
            Vector3 localPosition, Vector3 localScale, Color color, Vector3 localEuler)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;

            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.transform.localRotation = Quaternion.Euler(localEuler);

            Palette.Paint(go, color);
            return go;
        }
    }
}
