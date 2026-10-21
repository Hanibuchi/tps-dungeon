using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 門（SceneGate）ではない物を地図に印で出す（死後の場所の強化の台など）。地図の元（SurfaceMapSource）の子に置けば拾われる。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Map Marker")]
    public sealed class MapMarker : MonoBehaviour
    {
        [SerializeField, Tooltip("地図に出す印。None なら出さない。")]
        private MapIcon icon;

        [SerializeField, Tooltip("大きな地図で印の下に出す名前。")]
        private string label;

        public MapIcon Icon
        {
            get => icon;
            set => icon = value;
        }

        public string Label
        {
            get => label;
            set => label = value;
        }
    }
}
