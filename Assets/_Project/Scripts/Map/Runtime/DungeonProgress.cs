using UnityEngine;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// ダンジョンのどの階にいるか。シーンを読み直してもまたぐよう static に持つ。
    /// 階段で移る直前に <see cref="Pending"/> へ行き先の階を入れ、着いた階の FloorBootstrap が受け取って <see cref="Current"/> にする。
    /// Pending が無いとき（地上の門から来た・エディタでその階のシーンから再生した）は、そのシーンの最初の階から始まる。
    /// </summary>
    public static class DungeonProgress
    {
        /// <summary>次に着く階。着いた階が受け取ったら空にする。</summary>
        public static DungeonFloor? Pending { get; set; }

        /// <summary>今いる階。階の外（地上など）では最後にいた階のまま。</summary>
        public static DungeonFloor? Current { get; set; }

        /// <summary>Pending を受け取って空にする。</summary>
        public static DungeonFloor? TakePending()
        {
            var pending = Pending;
            Pending = null;
            return pending;
        }

        /// <summary>Enter Play Mode でドメインを読み直さない設定でも、前の再生の階を持ち越さない。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pending = null;
            Current = null;
        }
    }
}
