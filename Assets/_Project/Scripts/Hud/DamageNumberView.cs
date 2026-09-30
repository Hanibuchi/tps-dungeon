using System.Collections.Generic;
using TpsDungeon.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Hud
{
    /// <summary>
    /// 敵に当てたダメージの数字を、当たった場所に浮かべる。
    /// MeleeAttacker.Dealt を受けて 1 撃ごとに 1 つ出し、はじけて上がりながら消す。
    /// クリティカルは大きく金、エンチャント（爆発・衝撃波・追撃）の一撃は小さく橙（見た目は GameHud.uss の .damage-number）。
    /// GameHudView と同じ GameObject に付けて、同じ UIDocument に描く。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    [AddComponentMenu("TPS Dungeon/Damage Number View")]
    public sealed class DamageNumberView : MonoBehaviour
    {
        /// <summary>1 つの数字が出ている時間（秒）。</summary>
        public const float Lifetime = 0.8f;

        // 出た瞬間の大きさと、1 倍に戻るまでの時間（秒）。
        private const float PopScale = 1.4f;
        private const float PopDuration = 0.1f;

        // 出ている間に上がる高さ（m）。
        private const float RiseHeight = 0.6f;

        // 最後のこの割合で消えていく。
        private const float FadeFraction = 0.4f;

        // 重ならないように左右へばらす幅（m）。
        private const float Scatter = 0.25f;

        // 同時に出せる数。足りなければ一番古いものを使い回す。
        private const int MaxNumbers = 32;

        private const string NumberClass = "damage-number";
        private const string CriticalClass = "damage-number--critical";
        private const string MinorClass = "damage-number--minor";

        [SerializeField, Tooltip("当てたダメージの出どころ。未設定なら親から探す。")]
        private MeleeAttacker attacker;

        [SerializeField, Tooltip("数字を画面へ写すカメラ。未設定なら Camera.main。")]
        private Camera viewCamera;

        private sealed class Number
        {
            public Label Label;
            public Vector3 Origin;
            public float Age;
        }

        private UIDocument document;
        private VisualElement layer;
        private readonly List<Number> active = new List<Number>();
        private readonly Stack<Number> idle = new Stack<Number>();

        /// <summary>今出している数。</summary>
        public int ActiveCount => active.Count;

        private void Reset()
        {
            attacker = GetComponentInParent<MeleeAttacker>();
        }

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            if (attacker == null) attacker = GetComponentInParent<MeleeAttacker>();
        }

        private void OnEnable()
        {
            VisualElement root = document.rootVisualElement;
            if (root == null) return;

            // HUD の一番奥に、画面いっぱいの層を敷いて数字を置く。ほかの HUD の枠より下に見えるように。
            layer = new VisualElement { name = "damage-numbers", pickingMode = PickingMode.Ignore };
            layer.AddToClassList("damage-numbers");
            root.Insert(0, layer);
            active.Clear();
            idle.Clear();

            if (attacker != null) attacker.Dealt += OnDealt;
        }

        private void OnDisable()
        {
            if (attacker != null) attacker.Dealt -= OnDealt;
            layer?.RemoveFromHierarchy();
            layer = null;
            active.Clear();
            idle.Clear();
        }

        private void OnDealt(MeleeHitRecord record)
        {
            if (record.Damage <= 0) return;
            Show(record.Point, record.Damage, record.IsCritical, record.Kind != MeleeHitKind.Hit);
        }

        /// <summary>world に amount の数字を出す。minor はエンチャントの一撃の小さな数字。</summary>
        public void Show(Vector3 world, int amount, bool critical, bool minor)
        {
            if (layer == null) return;

            Number number = Take();
            Vector2 scatter = Random.insideUnitCircle * Scatter;
            number.Origin = world + new Vector3(scatter.x, 0f, scatter.y);
            number.Age = 0f;

            Label label = number.Label;
            label.text = critical ? $"{amount}!" : amount.ToString();
            label.EnableInClassList(CriticalClass, critical);
            label.EnableInClassList(MinorClass, minor && !critical);
            label.BringToFront();
            Place(number);
        }

        private Number Take()
        {
            Number number;
            if (idle.Count > 0)
            {
                number = idle.Pop();
            }
            else if (active.Count >= MaxNumbers)
            {
                number = active[0];
                active.RemoveAt(0);
            }
            else
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList(NumberClass);
                label.AddToClassList("rpg-deco-bold");
                layer.Add(label);
                number = new Number { Label = label };
            }

            number.Label.style.display = DisplayStyle.Flex;
            active.Add(number);
            return number;
        }

        private void LateUpdate()
        {
            if (active.Count == 0) return;

            float dt = Time.deltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Number number = active[i];
                number.Age += dt;
                if (number.Age >= Lifetime)
                {
                    number.Label.style.display = DisplayStyle.None;
                    active.RemoveAt(i);
                    idle.Push(number);
                    continue;
                }
                Place(number);
            }
        }

        /// <summary>今の経過に合わせて、画面上の位置・大きさ・濃さを入れる。カメラの後ろにあれば隠す。</summary>
        private void Place(Number number)
        {
            Label label = number.Label;
            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            IPanel panel = layer.panel;
            if (cam == null || panel == null)
            {
                label.style.visibility = Visibility.Hidden;
                return;
            }

            float t = number.Age / Lifetime;
            Vector3 world = number.Origin + Vector3.up * RiseAt(t);
            if (Vector3.Dot(world - cam.transform.position, cam.transform.forward) <= cam.nearClipPlane)
            {
                label.style.visibility = Visibility.Hidden;
                return;
            }

            Vector2 position = RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, cam);
            label.style.visibility = Visibility.Visible;
            label.style.left = position.x;
            label.style.top = position.y;
            float scale = ScaleAt(number.Age);
            label.style.scale = new Scale(new Vector3(scale, scale, 1f));
            label.style.opacity = OpacityAt(t);
        }

        /// <summary>経過の割合 t（0〜1）で、出た場所から上がった高さ（m）。はじめ速く、だんだんゆっくり。</summary>
        public static float RiseAt(float t)
        {
            t = Mathf.Clamp01(t);
            return RiseHeight * (1f - (1f - t) * (1f - t));
        }

        /// <summary>経過の秒 age での大きさ。出た瞬間に大きく、PopDuration で 1 倍に戻る。</summary>
        public static float ScaleAt(float age)
        {
            float k = Mathf.Clamp01(age / PopDuration);
            return Mathf.Lerp(PopScale, 1f, k);
        }

        /// <summary>経過の割合 t（0〜1）での濃さ。最後の FadeFraction で 1 から 0 へ。</summary>
        public static float OpacityAt(float t)
        {
            float fadeStart = 1f - FadeFraction;
            if (t <= fadeStart) return 1f;
            return Mathf.Clamp01(1f - (t - fadeStart) / FadeFraction);
        }
    }
}
