using TpsDungeon.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// 魂の強化の印（効果ごとの小さな絵）を Painter2D で描く。強化の丸の中と、ソウルの数の横の炎に使う。クリックは受けない。
    /// 線の色は USS の color（<see cref="IResolvedStyle.color"/>）。持ち主のクラスで色を変えれば印も変わる。
    /// </summary>
    [UxmlElement]
    public partial class SoulGlyph : VisualElement
    {
        /// <summary>ソウルの炎か、効果の印か。</summary>
        public enum Kind
        {
            Flame,
            Effect,
        }

        private SoulUpgradeEffect effect;
        private Kind kind = Kind.Flame;

        public SoulGlyph()
        {
            AddToClassList("soul-glyph");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(_ => MarkDirtyRepaint());
        }

        /// <summary>効果の印にする。</summary>
        public void SetEffect(SoulUpgradeEffect value)
        {
            effect = value;
            kind = Kind.Effect;
            MarkDirtyRepaint();
        }

        /// <summary>ソウルの炎にする。</summary>
        public void SetFlame()
        {
            kind = Kind.Flame;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            Rect r = contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (s <= 0f) return;

            Painter2D p = context.painter2D;
            Color color = resolvedStyle.color;
            p.strokeColor = color;
            p.fillColor = color;
            p.lineWidth = Mathf.Max(1.5f, s * 0.075f);
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;

            // 中心を原点に、s を 1 とした座標で描く。
            Vector2 c = r.center;
            Vector2 P(float x, float y) => c + new Vector2(x, y) * s;

            if (kind == Kind.Flame)
            {
                Flame(p, P, s);
                return;
            }

            switch (effect)
            {
                case SoulUpgradeEffect.ExpRate: Sparkle(p, P); break;
                case SoulUpgradeEffect.MaxHp: Heart(p, P); break;
                case SoulUpgradeEffect.CritChance: Crosshair(p, P, s); break;
                case SoulUpgradeEffect.CritMultiplier: Sword(p, P); break;
                case SoulUpgradeEffect.PartySize: People(p, P, s); break;
                case SoulUpgradeEffect.DropRate: Gem(p, P); break;
                case SoulUpgradeEffect.UniqueDropRate: Crown(p, P); break;
                case SoulUpgradeEffect.BagSize: Bag(p, P, s); break;
                case SoulUpgradeEffect.CarryOver: Keepsake(p, P); break;
                case SoulUpgradeEffect.WeaponShop: Shop(p, P); break;
                case SoulUpgradeEffect.ShopStock: Stock(p, P); break;
                case SoulUpgradeEffect.SellPrice: Coin(p, P, s); break;
                case SoulUpgradeEffect.Bank: Bank(p, P); break;
                case SoulUpgradeEffect.WeaponCodex: Book(p, P); break;
                case SoulUpgradeEffect.EnemyCodex: Skull(p, P, s); break;
                case SoulUpgradeEffect.Shortcut: Ladder(p, P); break;
                case SoulUpgradeEffect.ExpBoost: Hourglass(p, P); break;
                case SoulUpgradeEffect.BoxWeaponRate: CrateSword(p, P); break;
                case SoulUpgradeEffect.BoxSpawnRate: Crates(p, P); break;
                case SoulUpgradeEffect.Gacha: Gacha(p, P, s); break;
                case SoulUpgradeEffect.BankSize: Bank(p, P); break;
                default: Flame(p, P, s); break;
            }
        }

        private delegate Vector2 Map(float x, float y);

        private static void Flame(Painter2D p, Map P, float s)
        {
            p.BeginPath();
            p.MoveTo(P(0f, -0.42f));
            p.BezierCurveTo(P(0.12f, -0.2f), P(0.34f, -0.06f), P(0.3f, 0.14f));
            p.BezierCurveTo(P(0.27f, 0.34f), P(0.12f, 0.42f), P(0f, 0.42f));
            p.BezierCurveTo(P(-0.12f, 0.42f), P(-0.27f, 0.34f), P(-0.3f, 0.14f));
            p.BezierCurveTo(P(-0.33f, -0.04f), P(-0.16f, -0.1f), P(-0.1f, -0.24f));
            p.BezierCurveTo(P(-0.04f, -0.14f), P(-0.02f, -0.3f), P(0f, -0.42f));
            p.ClosePath();
            p.Fill();

            // 芯の明るい所を抜く。
            Color core = p.fillColor;
            p.fillColor = new Color(1f, 1f, 1f, core.a * 0.85f);
            p.BeginPath();
            p.MoveTo(P(0f, 0.04f));
            p.BezierCurveTo(P(0.1f, 0.14f), P(0.12f, 0.3f), P(0f, 0.32f));
            p.BezierCurveTo(P(-0.12f, 0.3f), P(-0.1f, 0.14f), P(0f, 0.04f));
            p.ClosePath();
            p.Fill();
            p.fillColor = core;
        }

        private static void Sparkle(Painter2D p, Map P)
        {
            p.BeginPath();
            p.MoveTo(P(0f, -0.4f));
            p.QuadraticCurveTo(P(0.05f, -0.05f), P(0.4f, 0f));
            p.QuadraticCurveTo(P(0.05f, 0.05f), P(0f, 0.4f));
            p.QuadraticCurveTo(P(-0.05f, 0.05f), P(-0.4f, 0f));
            p.QuadraticCurveTo(P(-0.05f, -0.05f), P(0f, -0.4f));
            p.ClosePath();
            p.Fill();
            Dot(p, P(0.28f, -0.28f), 0.05f, P);
        }

        private static void Dot(Painter2D p, Vector2 at, float radius, Map P)
        {
            float r = (P(radius, 0f) - P(0f, 0f)).x;
            p.BeginPath();
            p.Arc(at, r, 0f, 360f);
            p.Fill();
        }

        private static void Heart(Painter2D p, Map P)
        {
            p.BeginPath();
            p.MoveTo(P(0f, 0.36f));
            p.BezierCurveTo(P(-0.42f, 0.08f), P(-0.42f, -0.34f), P(-0.16f, -0.32f));
            p.BezierCurveTo(P(-0.06f, -0.31f), P(0f, -0.22f), P(0f, -0.16f));
            p.BezierCurveTo(P(0f, -0.22f), P(0.06f, -0.31f), P(0.16f, -0.32f));
            p.BezierCurveTo(P(0.42f, -0.34f), P(0.42f, 0.08f), P(0f, 0.36f));
            p.ClosePath();
            p.Fill();
        }

        private static void Crosshair(Painter2D p, Map P, float s)
        {
            p.BeginPath();
            p.Arc(P(0f, 0f), s * 0.26f, 0f, 360f);
            p.Stroke();
            Line(p, P(0f, -0.42f), P(0f, -0.14f));
            Line(p, P(0f, 0.14f), P(0f, 0.42f));
            Line(p, P(-0.42f, 0f), P(-0.14f, 0f));
            Line(p, P(0.14f, 0f), P(0.42f, 0f));
            Dot(p, P(0f, 0f), 0.06f, P);
        }

        private static void Line(Painter2D p, Vector2 a, Vector2 b)
        {
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        private static void Sword(Painter2D p, Map P)
        {
            p.BeginPath();
            p.MoveTo(P(0.32f, -0.36f));
            p.LineTo(P(0.36f, -0.4f));
            p.LineTo(P(0.34f, -0.3f));
            p.LineTo(P(-0.08f, 0.12f));
            p.LineTo(P(-0.12f, 0.08f));
            p.ClosePath();
            p.Fill();
            Line(p, P(-0.2f, 0.0f), P(0.0f, 0.2f));
            Line(p, P(-0.1f, 0.1f), P(-0.3f, 0.3f));
            Dot(p, P(-0.33f, 0.33f), 0.05f, P);
        }

        private static void People(Painter2D p, Map P, float s)
        {
            Dot(p, P(-0.14f, -0.16f), 0.11f, P);
            Dot(p, P(0.16f, -0.12f), 0.09f, P);
            p.BeginPath();
            p.Arc(P(-0.14f, 0.26f), s * 0.22f, 180f, 360f);
            p.ClosePath();
            p.Fill();
            p.BeginPath();
            p.Arc(P(0.18f, 0.27f), s * 0.17f, 180f, 360f);
            p.ClosePath();
            p.Fill();
        }

        private static void Gem(Painter2D p, Map P)
        {
            p.BeginPath();
            p.MoveTo(P(-0.32f, -0.1f));
            p.LineTo(P(-0.16f, -0.3f));
            p.LineTo(P(0.16f, -0.3f));
            p.LineTo(P(0.32f, -0.1f));
            p.LineTo(P(0f, 0.36f));
            p.ClosePath();
            p.Stroke();
            Line(p, P(-0.32f, -0.1f), P(0.32f, -0.1f));
            Line(p, P(-0.08f, -0.1f), P(0f, 0.36f));
            Line(p, P(0.08f, -0.1f), P(0f, 0.36f));
        }

        private static void Crown(Painter2D p, Map P)
        {
            p.BeginPath();
            p.MoveTo(P(-0.34f, 0.22f));
            p.LineTo(P(-0.38f, -0.2f));
            p.LineTo(P(-0.16f, 0.0f));
            p.LineTo(P(0f, -0.3f));
            p.LineTo(P(0.16f, 0.0f));
            p.LineTo(P(0.38f, -0.2f));
            p.LineTo(P(0.34f, 0.22f));
            p.ClosePath();
            p.Fill();
            Line(p, P(-0.34f, 0.32f), P(0.34f, 0.32f));
        }

        private static void Bag(Painter2D p, Map P, float s)
        {
            p.BeginPath();
            p.MoveTo(P(-0.3f, -0.08f));
            p.LineTo(P(0.3f, -0.08f));
            p.LineTo(P(0.36f, 0.36f));
            p.LineTo(P(-0.36f, 0.36f));
            p.ClosePath();
            p.Stroke();
            p.BeginPath();
            p.Arc(P(0f, -0.08f), s * 0.17f, 180f, 360f);
            p.Stroke();
            Line(p, P(-0.3f, 0.08f), P(0.3f, 0.08f));
        }

        private static void Keepsake(Painter2D p, Map P)
        {
            // 両の手のひらに載った小さな宝石。
            p.BeginPath();
            p.MoveTo(P(-0.4f, 0.02f));
            p.QuadraticCurveTo(P(-0.3f, 0.34f), P(0f, 0.34f));
            p.QuadraticCurveTo(P(0.3f, 0.34f), P(0.4f, 0.02f));
            p.Stroke();
            p.BeginPath();
            p.MoveTo(P(0f, -0.36f));
            p.LineTo(P(0.16f, -0.12f));
            p.LineTo(P(0f, 0.12f));
            p.LineTo(P(-0.16f, -0.12f));
            p.ClosePath();
            p.Fill();
        }

        private static void Shop(Painter2D p, Map P)
        {
            p.BeginPath();
            p.MoveTo(P(-0.38f, -0.04f));
            p.LineTo(P(0f, -0.36f));
            p.LineTo(P(0.38f, -0.04f));
            p.Stroke();
            p.BeginPath();
            p.MoveTo(P(-0.28f, -0.08f));
            p.LineTo(P(-0.28f, 0.36f));
            p.LineTo(P(0.28f, 0.36f));
            p.LineTo(P(0.28f, -0.08f));
            p.Stroke();
            p.BeginPath();
            p.MoveTo(P(-0.09f, 0.36f));
            p.LineTo(P(-0.09f, 0.1f));
            p.LineTo(P(0.09f, 0.1f));
            p.LineTo(P(0.09f, 0.36f));
            p.Stroke();
        }

        private static void Stock(Painter2D p, Map P)
        {
            // 立てかけた 3 本の剣。
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 0.2f;
                Line(p, P(x, -0.36f), P(x, 0.2f));
                Line(p, P(x - 0.08f, 0.14f), P(x + 0.08f, 0.14f));
            }
            Line(p, P(-0.38f, 0.36f), P(0.38f, 0.36f));
        }

        private static void Coin(Painter2D p, Map P, float s)
        {
            p.BeginPath();
            p.Arc(P(0f, 0f), s * 0.36f, 0f, 360f);
            p.Stroke();
            p.BeginPath();
            p.Arc(P(0f, 0f), s * 0.22f, 0f, 360f);
            p.Stroke();
            Line(p, P(0f, -0.12f), P(0f, 0.12f));
        }

        private static void Bank(Painter2D p, Map P)
        {
            p.BeginPath();
            p.MoveTo(P(-0.4f, -0.12f));
            p.LineTo(P(0f, -0.38f));
            p.LineTo(P(0.4f, -0.12f));
            p.ClosePath();
            p.Fill();
            for (int i = 0; i < 4; i++)
            {
                float x = -0.27f + i * 0.18f;
                Line(p, P(x, -0.04f), P(x, 0.26f));
            }
            Line(p, P(-0.4f, 0.36f), P(0.4f, 0.36f));
        }

        /// <summary>板を打ち付けた木箱（左上が x0, y0、幅と高さ w）。</summary>
        private static void Crate(Painter2D p, Map P, float x0, float y0, float w)
        {
            p.BeginPath();
            p.MoveTo(P(x0, y0));
            p.LineTo(P(x0 + w, y0));
            p.LineTo(P(x0 + w, y0 + w));
            p.LineTo(P(x0, y0 + w));
            p.ClosePath();
            p.Stroke();
            Line(p, P(x0, y0), P(x0 + w, y0 + w));
        }

        private static void CrateSword(Painter2D p, Map P)
        {
            // 木箱から飛び出す剣。
            Crate(p, P, -0.32f, -0.02f, 0.4f);
            Line(p, P(0.04f, 0.04f), P(0.36f, -0.36f));
            Line(p, P(0.04f, -0.12f), P(0.2f, 0.0f));
        }

        private static void Crates(Painter2D p, Map P)
        {
            // 積んだ 3 つの木箱。
            Crate(p, P, -0.38f, 0.04f, 0.34f);
            Crate(p, P, 0.04f, 0.04f, 0.34f);
            Crate(p, P, -0.17f, -0.34f, 0.34f);
        }

        private static void Gacha(Painter2D p, Map P, float s)
        {
            // 丸い玉入れの下に台と取り出し口。
            p.BeginPath();
            p.Arc(P(0f, -0.12f), s * 0.26f, 0f, 360f);
            p.Stroke();
            p.BeginPath();
            p.Arc(P(-0.08f, -0.06f), s * 0.07f, 0f, 360f);
            p.Fill();
            p.BeginPath();
            p.Arc(P(0.09f, -0.18f), s * 0.07f, 0f, 360f);
            p.Fill();
            p.BeginPath();
            p.MoveTo(P(-0.26f, 0.14f));
            p.LineTo(P(0.26f, 0.14f));
            p.LineTo(P(0.3f, 0.38f));
            p.LineTo(P(-0.3f, 0.38f));
            p.ClosePath();
            p.Stroke();
            Line(p, P(-0.06f, 0.26f), P(0.06f, 0.26f));
        }

        private static void Book(Painter2D p, Map P)
        {
            p.BeginPath();
            p.MoveTo(P(0f, -0.24f));
            p.QuadraticCurveTo(P(-0.2f, -0.36f), P(-0.4f, -0.28f));
            p.LineTo(P(-0.4f, 0.28f));
            p.QuadraticCurveTo(P(-0.2f, 0.2f), P(0f, 0.32f));
            p.QuadraticCurveTo(P(0.2f, 0.2f), P(0.4f, 0.28f));
            p.LineTo(P(0.4f, -0.28f));
            p.QuadraticCurveTo(P(0.2f, -0.36f), P(0f, -0.24f));
            p.LineTo(P(0f, 0.32f));
            p.Stroke();
        }

        private static void Skull(Painter2D p, Map P, float s)
        {
            p.BeginPath();
            p.Arc(P(0f, -0.06f), s * 0.3f, 150f, 390f);
            p.LineTo(P(0.14f, 0.36f));
            p.LineTo(P(-0.14f, 0.36f));
            p.ClosePath();
            p.Fill();

            Color body = p.fillColor;
            p.fillColor = new Color(0f, 0f, 0f, 0.85f);
            Dot(p, P(-0.11f, -0.04f), 0.075f, P);
            Dot(p, P(0.11f, -0.04f), 0.075f, P);
            p.fillColor = body;
        }

        private static void Ladder(Painter2D p, Map P)
        {
            // 地上から垂れるロープと、下の層への矢印。
            p.BeginPath();
            p.MoveTo(P(-0.1f, -0.4f));
            p.BezierCurveTo(P(0.12f, -0.24f), P(-0.16f, -0.04f), P(0.04f, 0.12f));
            p.Stroke();
            p.BeginPath();
            p.MoveTo(P(-0.16f, 0.14f));
            p.LineTo(P(0.04f, 0.38f));
            p.LineTo(P(0.24f, 0.14f));
            p.Stroke();
            Line(p, P(0.04f, 0.12f), P(0.04f, 0.36f));
        }

        private static void Hourglass(Painter2D p, Map P)
        {
            Line(p, P(-0.28f, -0.38f), P(0.28f, -0.38f));
            Line(p, P(-0.28f, 0.38f), P(0.28f, 0.38f));
            p.BeginPath();
            p.MoveTo(P(-0.22f, -0.38f));
            p.QuadraticCurveTo(P(-0.22f, -0.08f), P(0f, 0f));
            p.QuadraticCurveTo(P(-0.22f, 0.08f), P(-0.22f, 0.38f));
            p.MoveTo(P(0.22f, -0.38f));
            p.QuadraticCurveTo(P(0.22f, -0.08f), P(0f, 0f));
            p.QuadraticCurveTo(P(0.22f, 0.08f), P(0.22f, 0.38f));
            p.Stroke();
            p.BeginPath();
            p.MoveTo(P(-0.14f, 0.32f));
            p.LineTo(P(0f, 0.16f));
            p.LineTo(P(0.14f, 0.32f));
            p.ClosePath();
            p.Fill();
        }
    }
}
