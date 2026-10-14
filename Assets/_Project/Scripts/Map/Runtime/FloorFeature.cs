using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 部屋の FeatureCell に置く物（階段など）が、そのセルの床・天井・土台の組み立て方を FloorBuilder に頼むための印。
    /// 無ければ FloorBuilder はいつもどおりに組む。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Floor Feature")]
    public sealed class FloorFeature : MonoBehaviour
    {
        [Tooltip("部屋の床をこちらの床（穴の空いた床など）で置き換えるか。1 セルの部屋のときだけ、部屋の Prefab の子 Floor を消す。")]
        public bool replacesRoomFloor;

        [Tooltip("このセルの天井をこちらで持つか。持つなら FloorBuilder はこのセルに天井を張らない。")]
        public bool replacesCeiling;

        [Min(0f), Tooltip("床から下へ掘っている深さ（メートル）。このセルの土台はこの分だけ下げて置く（竪穴とぶつからないように）。")]
        public float depthBelowFloor;
    }
}
