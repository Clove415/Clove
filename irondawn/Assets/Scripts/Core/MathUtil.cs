using UnityEngine;

namespace IronDawn
{
    public static class MathUtil
    {
        /// <summary>把方向向量按圆锥角（度）做随机扰动。</summary>
        public static Vector3 Spread(Vector3 dir, float degrees)
        {
            if (degrees <= 0f) return dir;

            float tan = Mathf.Tan(degrees * Mathf.Deg2Rad);
            Vector2 c = Random.insideUnitCircle * tan;

            Vector3 right = Vector3.Cross(dir, Vector3.up);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            right.Normalize();

            Vector3 up = Vector3.Cross(right, dir).normalized;
            return (dir + right * c.x + up * c.y).normalized;
        }

        /// <summary>只保留水平分量并归一化；退化时返回 forward。</summary>
        public static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            if (v.sqrMagnitude < 0.0001f) return fallback;
            return v.normalized;
        }

        /// <summary>给定法线求水平切线方向，偏向指定朝向。</summary>
        public static Vector3 HorizontalTangent(Vector3 normal, Vector3 prefer)
        {
            Vector3 t = Vector3.Cross(Vector3.up, normal);
            if (t.sqrMagnitude < 0.0001f) return prefer;
            t.Normalize();
            if (Vector3.Dot(t, prefer) < 0f) t = -t;
            return t;
        }

        public static float DistanceFlat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
