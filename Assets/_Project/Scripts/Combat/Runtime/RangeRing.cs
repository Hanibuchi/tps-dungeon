using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 地面に寝かせた円の線（LineRenderer）。持続弓の照準の円と、降っている矢の雨の範囲に使う。
    /// 線は世界で水平な面に描く（TransformZ で上を向かせる）ので、カメラの角度で細くなったり太くなったりしない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class RangeRing : MonoBehaviour
    {
        private const int Segments = 48;

        /// <summary>地面と重なってちらつかないよう、少しだけ浮かせる高さ（m）。</summary>
        private const float Lift = 0.05f;

        private LineRenderer line;
        private float radius = -1f;
        private Color color;

        /// <summary>material が無ければ作らず null を返す（円を出さない）。</summary>
        public static RangeRing Create(string name, Material material, Color color, float width = 0.07f, Transform parent = null)
        {
            if (material == null) return null;

            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            // 線は TransformZ に垂直な面（ローカル XY）に描かれる。X を 90° 回してローカル XY を世界の XZ に寝かせる。
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var ring = go.AddComponent<RangeRing>();
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.loop = true;
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.positionCount = Segments;
            line.widthMultiplier = width;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            ring.line = line;
            ring.SetColor(color);
            return ring;
        }

        /// <summary>center（地面の点）を中心に半径 radius の円にする。</summary>
        public void Set(Vector3 center, float newRadius)
        {
            transform.SetPositionAndRotation(center + Vector3.up * Lift, Quaternion.Euler(90f, 0f, 0f));
            if (Mathf.Approximately(newRadius, radius)) return;

            radius = newRadius;
            for (int i = 0; i < Segments; i++)
            {
                float angle = 2f * Mathf.PI * i / Segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }

        public void SetColor(Color value)
        {
            color = value;
            line.startColor = value;
            line.endColor = value;
        }

        /// <summary>色はそのままで、不透明度だけ alpha 倍にする。</summary>
        public void SetAlpha(float alpha)
        {
            Color c = color;
            c.a *= Mathf.Clamp01(alpha);
            line.startColor = c;
            line.endColor = c;
        }

        public void SetVisible(bool visible)
        {
            if (line.enabled != visible) line.enabled = visible;
        }
    }
}
