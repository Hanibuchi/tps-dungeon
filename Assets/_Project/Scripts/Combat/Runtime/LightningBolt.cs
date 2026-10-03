using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 2 点の間に一瞬だけ走る雷の線（電撃の杖）。白い芯と、色の付いた太い光の 2 本の LineRenderer で描き、
    /// 途中の点を横へ不規則にずらしたギザギザを、短い間隔で引き直して瞬かせる。Duration 秒で薄れて消える。
    /// ダメージは持たない（RangedAttacker が与える）。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class LightningBolt : MonoBehaviour
    {
        /// <summary>ギザギザを引き直す間隔（秒）。</summary>
        private const float FlickerInterval = 0.04f;

        /// <summary>折れ目 1 つあたりの長さ（m）。長い雷ほど折れ目が多い。</summary>
        private const float SegmentLength = 0.45f;

        /// <summary>折れ目を横へずらす最大量（区間の長さに対する割合）。</summary>
        private const float Jitter = 0.3f;

        /// <summary>光の線の太さ（芯に対する倍率）と不透明度。</summary>
        private const float GlowWidthRatio = 4f;
        private const float GlowAlpha = 0.35f;

        private LineRenderer core;
        private LineRenderer glow;
        private Transform anchor;
        private Vector3 from;
        private Vector3 to;
        private Color color;
        private float duration;
        private float elapsed;
        private float nextFlicker;

        /// <summary>
        /// from から to へ雷を走らせる。material が無ければ何も出さない。
        /// anchor を渡すと、見えている間は始まりをその位置に付けていく（杖の先から出る雷が、構えの動きに置いていかれないように）。
        /// </summary>
        public static LightningBolt Spawn(Vector3 from, Vector3 to, Material material, Color color, float width, float duration,
            Transform anchor = null)
        {
            if (material == null) return null;

            var go = new GameObject("LightningBolt");
            var bolt = go.AddComponent<LightningBolt>();
            bolt.anchor = anchor;
            bolt.from = from;
            bolt.to = to;
            bolt.color = color;
            bolt.duration = Mathf.Max(0.02f, duration);
            bolt.glow = CreateLine(go.transform, "Glow", material, width * GlowWidthRatio);
            bolt.core = CreateLine(go.transform, "Core", material, width);
            bolt.Redraw();
            bolt.ApplyFade(1f);
            return bolt;
        }

        private static LineRenderer CreateLine(Transform parent, string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 1;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                Destroy(gameObject);
                return;
            }

            if (anchor != null && anchor.position != from)
            {
                from = anchor.position;
                Redraw();
            }
            else if (elapsed >= nextFlicker) Redraw();

            ApplyFade(1f - elapsed / duration);
        }

        /// <summary>端の 2 点は動かさず、途中の折れ目だけ引き直す。</summary>
        private void Redraw()
        {
            nextFlicker = elapsed + FlickerInterval;

            Vector3 path = to - from;
            float length = path.magnitude;
            int segments = Mathf.Clamp(Mathf.CeilToInt(length / SegmentLength), 2, 64);
            Vector3 forward = length > 1e-4f ? path / length : Vector3.forward;
            Vector3 side = Vector3.Cross(forward, Mathf.Abs(forward.y) > 0.95f ? Vector3.right : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, forward);
            float jitter = length / segments * Jitter * 2f;

            core.positionCount = segments + 1;
            glow.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                Vector3 point = from + path * ((float)i / segments);
                if (i > 0 && i < segments)
                    point += side * Random.Range(-jitter, jitter) + up * Random.Range(-jitter, jitter);
                core.SetPosition(i, point);
                glow.SetPosition(i, point);
            }
        }

        private void ApplyFade(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            Color white = Color.Lerp(Color.white, color, 0.25f);
            white.a = alpha;
            core.startColor = core.endColor = white;

            Color halo = color;
            halo.a = color.a * GlowAlpha * alpha;
            glow.startColor = glow.endColor = halo;
        }
    }
}
