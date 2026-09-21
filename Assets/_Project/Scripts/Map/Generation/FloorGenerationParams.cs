using System;
using System.Collections.Generic;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Generation
{
    /// <summary>1 フロアを生成するための入力パラメータ。FloorConfig(ScriptableObject) から組み立てる。</summary>
    public sealed class FloorGenerationParams
    {
        /// <summary>フロアのセル数（X 方向）。部屋はこの矩形からはみ出さない。</summary>
        public int Width = 64;

        /// <summary>フロアのセル数（Y 方向 = ワールド Z）。</summary>
        public int Height = 64;

        /// <summary>部屋数の下限。成長がここに届かなかったら作り直す。</summary>
        public int MinRoomCount = 10;

        /// <summary>部屋数の上限。実際の目標値はこの範囲からシードごとに引く。</summary>
        public int MaxRoomCount = 20;

        /// <summary>MinRoomCount に届かないときに最初から作り直す回数の上限。</summary>
        public int MaxPackAttempts = 6;

        /// <summary>最初の部屋をフロア中心からずらす最大セル数。0 なら必ず中心。</summary>
        public int StartJitter = 4;

        /// <summary>
        /// 木の辺に加えて、たまたま向かい合った未使用ソケット対に開ける追加ドアの本数の上限。
        /// 袋小路を減らして返り道を作るためのもので、必ずこの本数だけ開くわけではない。
        /// </summary>
        public int ExtraDoorCount = 2;

        /// <summary>ショップ部屋を配置するか。</summary>
        public bool IncludeShop = true;

        /// <summary>抽選対象の部屋テンプレート。</summary>
        public IReadOnlyList<RoomTemplateData> Templates = Array.Empty<RoomTemplateData>();

        public void Validate()
        {
            if (Width < 16 || Height < 16)
                throw new ArgumentException($"フロアサイズが小さすぎる: {Width}x{Height}");

            // 上り階段・下り階段・ショップは別々の部屋に置きたいので、その分だけ下限が要る。
            int floor = IncludeShop ? 4 : 3;
            if (MinRoomCount < floor)
                throw new ArgumentException($"MinRoomCount は {floor} 以上でなければならない: {MinRoomCount}");
            if (MaxRoomCount < MinRoomCount)
                throw new ArgumentException($"部屋数の範囲が逆転している: {MinRoomCount}..{MaxRoomCount}");
            if (MaxPackAttempts < 1)
                throw new ArgumentException($"MaxPackAttempts が小さすぎる: {MaxPackAttempts}");
            if (StartJitter < 0)
                throw new ArgumentException($"StartJitter が負: {StartJitter}");
            if (ExtraDoorCount < 0)
                throw new ArgumentException($"ExtraDoorCount が負: {ExtraDoorCount}");
            if (Templates == null || Templates.Count == 0)
                throw new ArgumentException("部屋テンプレートが 1 つも渡されていない");

            bool anyUsable = false;
            foreach (var template in Templates)
            {
                if (template.Width > Width || template.Height > Height)
                    throw new ArgumentException(
                        $"テンプレート '{template.Id}' {template.Width}x{template.Height} がフロア {Width}x{Height} に収まらない");
                if (template.Sockets.Count == 0)
                    throw new ArgumentException($"テンプレート '{template.Id}' にドア候補が 1 つも無いので、どこにも繋げられない");

                foreach (var socket in template.Sockets)
                {
                    if (IsOnMatchingBorder(template, socket)) continue;
                    throw new ArgumentException(
                        $"テンプレート '{template.Id}' のドア候補 {socket} が {socket.Facing} 側の外周セルに乗っていない。" +
                        "隣の部屋はこのセルの真横に吸着するので、外周セルでないと部屋同士が食い込む");
                }

                if (template.Weight > 0f) anyUsable = true;
            }
            if (!anyUsable)
                throw new ArgumentException("Weight が 0 より大きい部屋テンプレートが 1 つも無い");
        }

        /// <summary>ドア候補が、自分の向きに対応する外周セルの上にあるか。</summary>
        private static bool IsOnMatchingBorder(RoomTemplateData template, DoorSocketData socket)
        {
            var cell = socket.LocalCell;
            if (cell.X < 0 || cell.Y < 0 || cell.X >= template.Width || cell.Y >= template.Height) return false;

            switch (socket.Facing)
            {
                case Direction.North: return cell.Y == template.Height - 1;
                case Direction.East: return cell.X == template.Width - 1;
                case Direction.South: return cell.Y == 0;
                default: return cell.X == 0;
            }
        }

        public FloorGenerationParams Clone() => (FloorGenerationParams)MemberwiseClone();
    }
}
