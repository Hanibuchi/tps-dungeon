using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 生きている敵の体（ルートのカプセル）を入れるレイヤー。物理の当たりは Default と同じ（床・壁・パーティー・敵どうし）。
    /// カメラの壁よけ（CinemachineThirdPersonFollow の AvoidObstacles）はこのレイヤーを見ない（敵が前を横切ってもカメラが寄らない）。
    /// 階の NavMesh もこのレイヤーを焼かない。攻撃・照準・見通しの判定は今までどおり敵に当たる。
    /// 敵のプレハブは Default のままで、EnemyHealth が起きたときにルートだけをここへ移す（壊せる置物は移さない）。
    /// レイヤーと衝突の設定はメニュー「Tools/TPS Dungeon/敵/敵のレイヤーを用意する」で作る。無ければ何も変えない。
    /// </summary>
    public static class EnemyLayer
    {
        public const string Name = "Enemy";

        /// <summary>レイヤーの番号。プロジェクトに無ければ -1。</summary>
        public static int Index => LayerMask.NameToLayer(Name);

        /// <summary>このレイヤーだけのマスク。無ければ 0。</summary>
        public static int Mask
        {
            get
            {
                int index = Index;
                return index >= 0 ? 1 << index : 0;
            }
        }

        /// <summary>mask から敵のレイヤーを外したもの。</summary>
        public static int Exclude(int mask) => mask & ~Mask;
    }
}
