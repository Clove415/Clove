using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IronDawn
{
    /// <summary>
    /// 弹道视觉。概念版没有粒子资源，全部用 LineRenderer 现拉。
    /// </summary>
    public static class Tracer
    {
        public static void Draw(Vector3 from, Vector3 to, Color color, float width, float life)
        {
            GameObject go = new GameObject("~tracer");
            LineRenderer lr = go.AddComponent<LineRenderer>();

            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);

            lr.startWidth = width;
            lr.endWidth = width * 0.25f;
            lr.numCapVertices = 2;

            lr.startColor = color;
            lr.endColor = color;

            lr.material = Palette.Line(color);
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;

            Object.Destroy(go, life);
        }

        public static void Flash(Vector3 point, float radius, Color color, float life)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "~flash";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = point;
            go.transform.localScale = Vector3.one * radius;
            Palette.Paint(go, color);
            Object.Destroy(go, life);
        }
    }
}
