using System;
using UnityEngine;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>
    /// 階の光と色（層ごと）。FloorConfig に持たせ、Settings/Tables/FloorMoods.csv から取り込む（FloorMoodCsvImporter）。
    /// FloorBuilder が、共通の Global Volume の上に重ねる Volume（色・彩度・周りの暗さ）、Play 中の環境光、松明の光に使う。
    /// </summary>
    [Serializable]
    public struct FloorMood
    {
        [Tooltip("環境光の色（空の側）。地平線の側はこの 0.85 倍、床の側は 0.55 倍。天井で日の光が届かない室内の明るさの元になる。")]
        public Color ambient;

        [Tooltip("画面全体に掛ける色（ColorAdjustments の colorFilter）。白なら変えない。暗くしすぎると Volume だけで見えなくなるので、明るさ 0.7 くらいまでに。")]
        public Color filter;

        [Range(-100f, 100f), Tooltip("彩度。負で色が抜ける。")]
        public float saturation;

        [Range(0f, 1f), Tooltip("画面の周りの暗さ（Vignette）。")]
        public float vignette;

        [Tooltip("松明の光の色。")]
        public Color torchColor;

        [Min(0f), Tooltip("松明の光の強さ。机の上のろうそくはこの何割か（プレハブの FlickerLight の倍率）。")]
        public float torchIntensity;

        [Min(0.1f), Tooltip("松明の光の届く距離（メートル）。ろうそくはこの何割か。")]
        public float torchRange;

        [Range(0f, 1f), Tooltip("松明のゆらめきの幅（明るさの割合）。")]
        public float flicker;

        /// <summary>CSV を取り込む前の既定（今までの見た目に近い、明るく暖かい階）。</summary>
        public static FloorMood Default => new FloorMood
        {
            ambient = new Color(0.62f, 0.72f, 0.82f),
            filter = Color.white,
            saturation = 0f,
            vignette = 0f,
            torchColor = new Color(1f, 0.6f, 0.3f),
            torchIntensity = 2f,
            torchRange = 8f,
            flicker = 0.12f,
        };

        /// <summary>取り込んでいない（全部 0 の）値か。</summary>
        public bool IsUnset => torchRange <= 0f && filter.maxColorComponent <= 0f;
    }
}
