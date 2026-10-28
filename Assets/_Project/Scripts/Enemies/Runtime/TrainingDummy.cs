using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// DPS を測るためのカカシ（確認用）。動かず、攻撃せず、倒れない（EnemyHealth の immortal）。ひるみ・ノックバックも無い。
    /// 攻撃からは敵と同じに見える（EnemyHealth と当たり判定を持つ）ので、雷の的・ホーミング・仲間の標的にもなる。
    /// 受けたダメージを <see cref="DpsMeter"/> で数え、Game 画面の頭の上に DPS を出す。
    /// 敵の挙動の試験の窓（EnemyBehaviourTestWindow）が Play 中に <see cref="Spawn"/> で出す。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(EnemyHealth))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        private const int DummyHp = 1000000;
        private const float Height = 1.8f;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");

        private EnemyHealth health;
        private GUIStyle style;

        public DpsMeter Meter { get; } = new DpsMeter();

        /// <summary>Game 画面の頭の上に DPS を出すか。</summary>
        public bool ShowLabel { get; set; } = true;

        /// <summary>カカシを 1 体作る。足元が position、正面（+Z）が rotation の向き。</summary>
        public static TrainingDummy Spawn(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var root = new GameObject("カカシ");
            root.transform.SetPositionAndRotation(position, rotation);
            if (parent != null) root.transform.SetParent(parent, true);

            var body = root.AddComponent<CapsuleCollider>();
            body.height = Height;
            body.radius = 0.35f;
            body.center = new Vector3(0f, Height * 0.5f, 0f);

            var straw = new Color(0.78f, 0.64f, 0.36f);
            var wood = new Color(0.45f, 0.3f, 0.18f);
            var cloth = new Color(0.55f, 0.2f, 0.18f);
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(0.08f, 0.75f, 0.08f), wood);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.25f, 0f), new Vector3(0.5f, 0.6f, 0.26f), cloth);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.38f, 0f), new Vector3(1.4f, 0.1f, 0.1f), wood);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.72f, 0f), new Vector3(0.34f, 0.34f, 0.34f), straw);

            var health = root.AddComponent<EnemyHealth>();
            health.SetMaxHp(DummyHp);
            health.SetImmortal(true);
            return root.AddComponent<TrainingDummy>();
        }

        private static void Part(Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = shape.ToString();
            // 当たり判定はルートのカプセル 1 つだけにする。
            Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;

            var renderer = part.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, color);
            block.SetColor(LegacyColor, color);
            renderer.SetPropertyBlock(block);
        }

        private void Awake() => health = GetComponent<EnemyHealth>();

        private void OnEnable()
        {
            if (health == null) health = GetComponent<EnemyHealth>();
            health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
        }

        private void OnDamaged(EnemyHealth _, DamageInfo damage, int dealt) => Meter.Record(Time.time, dealt, damage.IsCritical);

        private void OnGUI()
        {
            if (!ShowLabel) return;
            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 screen = camera.WorldToScreenPoint(transform.position + Vector3.up * (Height + 0.35f));
            if (screen.z <= 0f) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
                style.normal.textColor = Color.white;
            }
            style.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 50f));

            float now = Time.time;
            string text = Meter.HasHits
                ? $"DPS {Meter.Dps(now):0.0}{(Meter.IsMeasuring(now) ? "" : "（止めた）")}　直近 {Meter.Window:0} 秒 {Meter.RecentDps(now):0.0}\n" +
                  $"合計 {Meter.Total}　{Meter.Hits} 回　{Meter.Elapsed(now):0.0} 秒　最後 {Meter.LastHit}"
                : "カカシ（叩くと DPS を測る）";
            Vector2 size = style.CalcSize(new GUIContent(text));
            var rect = new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y, size.x, size.y);
            GUI.Box(rect, text, style);
        }
    }
}
