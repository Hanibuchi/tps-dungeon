using UnityEngine;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>
    /// 階層ごとの壁・天井・ドア・階段の見た目（素材）。FloorConfig の theme に差し、
    /// 生成器（壁・床・天井のパーツを生成）がこの素材でその階のパーツ一式を作る。
    /// 同じ層の階は同じテーマを差せば揃う。層ごとに変えるときは、このアセットを複製して素材とパーツの置き場所を差し替え、その層の FloorConfig に差す。
    /// </summary>
    [CreateAssetMenu(fileName = "FloorTheme", menuName = "TPS Dungeon/Floor Theme")]
    public sealed class FloorTheme : ScriptableObject
    {
        [Tooltip("このテーマで作る共通パーツ（壁・ドア・天井・階段など）の置き場所。Assets からのパスで、層ごとに分ける（例: Assets/_Project/Prefabs/Map/L1/Parts）。")]
        public string partsFolder;

        [Header("壁・天井")]
        [Tooltip("壁・窓付きの壁・ドアのまわりの壁。")]
        public Material wall;
        [Tooltip("天井。")]
        public Material ceiling;
        [Tooltip("建物の下の土台（窓の外に見える）。")]
        public Material foundation;

        [Header("ドア")]
        [Tooltip("ドアの開口のまわり（両脇とアーチ）に並べるレンガ。")]
        public Material doorBrick;
        [Tooltip("扉板。")]
        public Material doorLeaf;
        [Tooltip("ドアノブ。")]
        public Material doorKnob;

        [Header("階段")]
        [Tooltip("階段の段と、階段のまわり（下りの穴の空いた床・竪穴の壁、上りの穴の空いた天井・竪穴の壁と蓋）。")]
        public Material stair;
        [Tooltip("下り階段の竪穴の奥と底、上り階段の竪穴の奥。暗くして先へ続いて見せる（黒の単色にしておくと暗がりに溶ける）。")]
        public Material stairVoid;
    }
}
