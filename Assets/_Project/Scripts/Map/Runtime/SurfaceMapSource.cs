using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 地上の地図の元。真上から撮った地上の絵（城を除く）と、その絵が写す範囲・城の土台の範囲（どちらもワールドの x と z、メートル）を持つ。
    /// 地上シーンの [Surface] に付け、生成器（地上の地図を撮る）が中身を入れる。HUD がこれを見つけて地図に描く。
    /// 地図に重ねる印（ダンジョンの入口・武器屋・銀行・解放したショートカット）は、描くたびにシーンから拾う（<see cref="Markers"/>）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Surface Map Source")]
    public sealed class SurfaceMapSource : MonoBehaviour
    {
        /// <summary>地図の印の種類。</summary>
        public enum MarkerKind
        {
            /// <summary>ダンジョンへ入る城の門。</summary>
            DungeonEntrance = 0,
            WeaponShop = 1,
            Bank = 2,
            /// <summary>解放したショートカット（Layer にその層、0 始まり）。</summary>
            Shortcut = 3,
        }

        public readonly struct Marker
        {
            public readonly MarkerKind Kind;
            /// <summary>ワールドの x と z。</summary>
            public readonly Vector2 Position;
            public readonly int Layer;
            /// <summary>印の下に出す名前（建物・入口）。ショートカットは空。</summary>
            public readonly string Label;

            public Marker(MarkerKind kind, Vector2 position, int layer, string label = null)
            {
                Kind = kind;
                Position = position;
                Layer = layer;
                Label = label;
            }
        }

        private static readonly List<SurfaceMapSource> Active = new List<SurfaceMapSource>();

        [SerializeField, Tooltip("真上から撮った地上の絵（城・ショートカット・パーティーは写さない）。")]
        private Texture2D image;

        [SerializeField, Tooltip("絵が写す範囲（x・z の最小と大きさ、メートル）。")]
        private Rect area;

        [SerializeField, Tooltip("城の土台の範囲（x・z、メートル）。地図には四角で描く。")]
        private Rect castle;

        public Texture2D Image => image;
        public Rect Area => area;
        public Rect Castle => castle;

        /// <summary>有効な地図の元（最後に有効になったもの）。無ければ null。</summary>
        public static SurfaceMapSource Current => Active.Count > 0 ? Active[Active.Count - 1] : null;

        public void Configure(Texture2D newImage, Rect newArea, Rect newCastle)
        {
            image = newImage;
            area = newArea;
            castle = newCastle;
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        /// <summary>
        /// 地図に重ねる印。地図の印を決めてある門（SceneGate.Icon。ダンジョンの入口・武器屋・銀行）と、
        /// 開いている（解放した）地上のショートカット（登る方）。解放していないショートカットは入れない。
        /// </summary>
        public List<Marker> Markers()
        {
            var markers = new List<Marker>();
            foreach (var gate in GetComponentsInChildren<SceneGate>())
            {
                if (gate.Icon == MapIcon.None) continue;
                var kind = gate.Icon == MapIcon.DungeonEntrance ? MarkerKind.DungeonEntrance
                    : gate.Icon == MapIcon.WeaponShop ? MarkerKind.WeaponShop
                    : MarkerKind.Bank;
                var p = gate.transform.position;
                markers.Add(new Marker(kind, new Vector2(p.x, p.z), -1, gate.MapLabel));
            }
            foreach (var point in GetComponentsInChildren<ShortcutPoint>())
            {
                if (point.Direction != ShortcutDirection.ToBossFloor || !point.IsOpen) continue;
                var p = point.transform.position;
                markers.Add(new Marker(MarkerKind.Shortcut, new Vector2(p.x, p.z), point.Layer));
            }
            return markers;
        }
    }
}
