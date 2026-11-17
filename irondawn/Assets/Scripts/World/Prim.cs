using UnityEngine;
using Object = UnityEngine.Object;

namespace IronDawn
{
    /// <summary>
    /// 灰盒几何工厂。所有关卡内容都由代码在运行时生成，工程里不需要摆任何预制体。
    /// </summary>
    public static class Prim
    {
        public static GameObject Empty(string name, Transform parent, Vector3 worldPos)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.identity;
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Color color, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = size;
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            Palette.Paint(go, color);
            return go;
        }

        public static GameObject BoxRot(string name, Transform parent, Vector3 center, Vector3 size, Vector3 euler, Color color)
        {
            GameObject go = Box(name, parent, center, size, color, true);
            go.transform.rotation = Quaternion.Euler(euler);
            return go;
        }

        /// <summary>Capsule 原始高度为 2、半径 0.5，这里按“目标高/目标半径”换算。</summary>
        public static GameObject Capsule(string name, Transform parent, Vector3 center, float radius, float height, Color color, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            Palette.Paint(go, color);
            return go;
        }

        /// <summary>Cylinder 原始高度为 2、半径 0.5。</summary>
        public static GameObject Cylinder(string name, Transform parent, Vector3 center, float radius, float height, Color color, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            Palette.Paint(go, color);
            return go;
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 center, float radius, Color color, bool keepCollider = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * (radius * 2f);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            Palette.Paint(go, color);
            return go;
        }

        // 说明：这里曾经有一个 Decor()（无碰撞体的纯装饰件）。
        // 对战场地要求"看得见就挡得住"，所以整条链路都不再生成无实体物件。
    }
}
