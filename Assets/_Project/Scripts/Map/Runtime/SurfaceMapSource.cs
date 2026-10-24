using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 地上の地図の元。真上から撮った地上の絵（城を除く）と、その絵が写す範囲・城の土台の範囲（どちらもワールドの x と z、メートル）を持つ。
    /// 地上シーンの [Surface] に付け、生成器（地上の地図を撮る）が中身を入れる。HUD がこれを見つけて地図に描く。
    /// 死後の場所（[Afterlife]）も同じ作りの地図を持つ（城の土台は無い。左上の札の地名だけ変える）。
    /// 地図に重ねる印（ダンジョンの入口・武器屋・銀行・ガチャ屋・解放したショートカット・強化の台・転生の渦）は、描くたびにシーンから拾う（<see cref="Markers"/>）。
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
            /// <summary>死後の場所の強化の台。</summary>
            Upgrade = 4,
            /// <summary>死後の場所の転生の渦。</summary>
            Rebirth = 5,
            Gacha = 6,
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

        [SerializeField, Tooltip("城の土台の範囲（x・z、メートル）。地図には四角で描く。大きさが 0 なら描かない。")]
        private Rect castle;

        [SerializeField, Tooltip("左上の札に出す地名。空なら「地上」。")]
        private string placeName;

        public Texture2D Image => image;
        public Rect Area => area;
        public Rect Castle => castle;

        /// <summary>左上の札に出す地名。</summary>
        public string PlaceName => string.IsNullOrEmpty(placeName) ? "地上" : placeName;

        /// <summary>有効な地図の元（最後に有効になったもの）。無ければ null。</summary>
        public static SurfaceMapSource Current => Active.Count > 0 ? Active[Active.Count - 1] : null;

        public void Configure(Texture2D newImage, Rect newArea, Rect newCastle)
        {
            image = newImage;
            area = newArea;
            castle = newCastle;
        }

        public void Configure(Texture2D newImage, Rect newArea, Rect newCastle, string newPlaceName)
        {
            Configure(newImage, newArea, newCastle);
            placeName = newPlaceName;
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        /// <summary>
        /// 地図に重ねる印。地図の印を決めてある門（SceneGate.Icon。ダンジョンの入口・武器屋・銀行・転生の渦）と、門ではない印（<see cref="MapMarker"/>。強化の台）、
        /// 開いている（解放した）地上のショートカット（登る方）。解放していないショートカットは入れない。
        /// </summary>
        public List<Marker> Markers()
        {
            var markers = new List<Marker>();
            foreach (var gate in GetComponentsInChildren<SceneGate>())
            {
                if (gate.Icon == MapIcon.None) continue;
                var p = gate.transform.position;
                markers.Add(new Marker(KindOf(gate.Icon), new Vector2(p.x, p.z), -1, gate.MapLabel));
            }
            foreach (var marker in GetComponentsInChildren<MapMarker>())
            {
                if (marker.Icon == MapIcon.None) continue;
                var p = marker.transform.position;
                markers.Add(new Marker(KindOf(marker.Icon), new Vector2(p.x, p.z), -1, marker.Label));
            }
            foreach (var point in GetComponentsInChildren<ShortcutPoint>())
            {
                if (point.Direction != ShortcutDirection.ToBossFloor || !point.IsOpen) continue;
                var p = point.transform.position;
                markers.Add(new Marker(MarkerKind.Shortcut, new Vector2(p.x, p.z), point.Layer));
            }
            return markers;
        }

        private static MarkerKind KindOf(MapIcon icon) => icon switch
        {
            MapIcon.DungeonEntrance => MarkerKind.DungeonEntrance,
            MapIcon.WeaponShop => MarkerKind.WeaponShop,
            MapIcon.Upgrade => MarkerKind.Upgrade,
            MapIcon.Rebirth => MarkerKind.Rebirth,
            MapIcon.Gacha => MarkerKind.Gacha,
            _ => MarkerKind.Bank,
        };
    }
}
