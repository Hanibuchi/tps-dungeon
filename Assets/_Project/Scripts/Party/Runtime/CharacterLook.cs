using System;

namespace TpsDungeon.Party
{
    /// <summary>
    /// キャラの見た目の個性。体（モデル）は全員同じで、角・耳・しっぽ（GanzSe の小物）の有無と色で見分ける。
    /// 0 は付けない、1 以上は色の番号（角・耳は 1〜<see cref="HornColors"/>、しっぽは 1〜<see cref="TailColors"/>）。
    /// 角と耳は左右を同じ色で一組にして付ける。
    /// </summary>
    [Serializable]
    public struct CharacterLook : IEquatable<CharacterLook>
    {
        public const int HornColors = 3;
        public const int EarColors = 3;
        public const int TailColors = 5;

        /// <summary>組み合わせの数（何も付けないものを含む）。</summary>
        public const int Combinations = (HornColors + 1) * (EarColors + 1) * (TailColors + 1);

        // 並び順で見た目を配るときに、似た組み合わせが続かないよう飛ばし読みする歩幅。Combinations - 1 と互いに素。
        private const int Stride = 37;

        public int horn;
        public int ear;
        public int tail;

        public CharacterLook(int horn, int ear, int tail)
        {
            this.horn = horn;
            this.ear = ear;
            this.tail = tail;
        }

        /// <summary>何も付けない（今の主人公の見た目のまま）。</summary>
        public static CharacterLook None => default;

        public bool IsNone => horn <= 0 && ear <= 0 && tail <= 0;

        /// <summary>
        /// index 番目の仲間に配る見た目。0 は何も付けない。1 以上は、何か付ける組み合わせを重ならないように順に配る
        /// （<see cref="Combinations"/> - 1 人まで重ならない。パーティーは 12 人なので足りる）。
        /// </summary>
        public static CharacterLook ForIndex(int index)
        {
            if (index <= 0) return None;

            int withSomething = Combinations - 1;
            int code = 1 + (int)((long)(index - 1) * Stride % withSomething);
            return FromCode(code);
        }

        /// <summary>組み合わせの番号（0 で何も付けない）から見た目にする。</summary>
        public static CharacterLook FromCode(int code)
        {
            code = ((code % Combinations) + Combinations) % Combinations;
            int tail = code % (TailColors + 1);
            code /= TailColors + 1;
            int ear = code % (EarColors + 1);
            int horn = code / (EarColors + 1);
            return new CharacterLook(horn, ear, tail);
        }

        /// <summary>範囲外の色の番号を付けない（0）に直したもの。</summary>
        public CharacterLook Clamped => new CharacterLook(
            horn > HornColors ? 0 : Math.Max(0, horn),
            ear > EarColors ? 0 : Math.Max(0, ear),
            tail > TailColors ? 0 : Math.Max(0, tail));

        public bool Equals(CharacterLook other) => horn == other.horn && ear == other.ear && tail == other.tail;

        public override bool Equals(object obj) => obj is CharacterLook other && Equals(other);

        public override int GetHashCode() => (horn * 31 + ear) * 31 + tail;

        public override string ToString() => $"角{horn} 耳{ear} しっぽ{tail}";
    }
}
