using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>BSP のリーフごとに部屋テンプレートを抽選して配置する。</summary>
    public static class RoomPlacer
    {
        private readonly struct PlacementOption
        {
            public readonly RoomTemplateData Template;
            public readonly int Rotation;
            public readonly float Weight;

            public PlacementOption(RoomTemplateData template, int rotation, float weight)
            {
                Template = template;
                Rotation = rotation;
                Weight = weight;
            }
        }

        /// <summary>リーフから余白を引いた、部屋を置ける範囲。</summary>
        public static GridRect UsableArea(GridRect leaf, FloorGenerationParams p) => leaf.Shrink(p.LeafPadding);

        /// <summary>そのリーフに、指定タグを持つテンプレートを 1 つでも置けるか。</summary>
        public static bool CanHost(GridRect leaf, RoomTag tag, FloorGenerationParams p)
        {
            var usable = UsableArea(leaf, p);
            foreach (var template in p.Templates)
            {
                if ((template.Tags & tag) == 0) continue;
                foreach (int rotation in Rotations(template))
                {
                    var (width, height) = template.SizeAfterRotation(rotation);
                    if (width <= usable.Width && height <= usable.Height) return true;
                }
            }
            return false;
        }

        public static List<RoomInstance> Place(
            IReadOnlyList<GridRect> leafRects,
            IReadOnlyList<RoomRole> roles,
            FloorGenerationParams p,
            Random rng)
        {
            var rooms = new List<RoomInstance>(leafRects.Count);

            for (int i = 0; i < leafRects.Count; i++)
            {
                var leaf = leafRects[i];
                var usable = UsableArea(leaf, p);
                var role = roles[i];

                var options = CollectOptions(usable, role.RequiredTag(), p);
                if (options.Count == 0 && role != RoomRole.Normal)
                {
                    // 役割用のテンプレートが収まらないときは通常部屋として置き、役割は下で解除する。
                    options = CollectOptions(usable, RoomTag.Normal, p);
                    role = RoomRole.Normal;
                }
                if (options.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"リーフ {i} {leaf} (配置可能領域 {usable}) に収まる部屋テンプレートがない。" +
                        "MinLeafSize を上げるか、小さいテンプレートを追加すること。");
                }

                var chosen = PickWeighted(options, rng);
                var (width, height) = chosen.Template.SizeAfterRotation(chosen.Rotation);
                int x = usable.X + rng.Next(usable.Width - width + 1);
                int y = usable.Y + rng.Next(usable.Height - height + 1);
                var bounds = new GridRect(x, y, width, height);

                var sockets = new List<PlacedSocket>();
                foreach (var local in chosen.Template.SocketsAfterRotation(chosen.Rotation))
                {
                    var cell = new GridPos(bounds.X + local.LocalCell.X, bounds.Y + local.LocalCell.Y);
                    sockets.Add(new PlacedSocket(cell, local.Facing));
                }

                var room = new RoomInstance(i, chosen.Template, chosen.Rotation, bounds, leaf, sockets)
                {
                    Role = role,
                };
                rooms.Add(room);
            }

            return rooms;
        }

        private static IEnumerable<int> Rotations(RoomTemplateData template)
        {
            if (!template.AllowRotation)
            {
                yield return 0;
                yield break;
            }
            for (int rotation = 0; rotation < 4; rotation++) yield return rotation;
        }

        private static List<PlacementOption> CollectOptions(GridRect usable, RoomTag tag, FloorGenerationParams p)
        {
            var options = new List<PlacementOption>();
            if (usable.Width <= 0 || usable.Height <= 0) return options;

            foreach (var template in p.Templates)
            {
                if ((template.Tags & tag) == 0) continue;
                if (template.Weight <= 0f) continue;

                var fitting = new List<int>();
                foreach (int rotation in Rotations(template))
                {
                    var (width, height) = template.SizeAfterRotation(rotation);
                    if (width <= usable.Width && height <= usable.Height) fitting.Add(rotation);
                }
                if (fitting.Count == 0) continue;

                // 回転の多いテンプレートが有利にならないよう、重みを回転数で割る。
                float weight = template.Weight / fitting.Count;
                foreach (int rotation in fitting) options.Add(new PlacementOption(template, rotation, weight));
            }
            return options;
        }

        private static PlacementOption PickWeighted(List<PlacementOption> options, Random rng)
        {
            float total = 0f;
            foreach (var option in options) total += option.Weight;

            double roll = rng.NextDouble() * total;
            foreach (var option in options)
            {
                roll -= option.Weight;
                if (roll <= 0d) return option;
            }
            return options[options.Count - 1];
        }
    }
}
