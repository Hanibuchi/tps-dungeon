using UnityEngine;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>
    /// 1 つの階の生成設定。階ごとに 1 つ作り、その階のシーンの FloorBootstrap から参照する。
    /// 上の層ほど広くする、といった層ごとの差はこのアセットの値で表現する。
    /// </summary>
    [CreateAssetMenu(fileName = "FloorConfig", menuName = "TPS Dungeon/Floor Config")]
    public sealed class FloorConfig : ScriptableObject
    {
        [Header("グリッド")]
        [Min(16), Tooltip("フロア全体の横幅（セル数）。部屋はこの範囲からはみ出さない。余裕があるほど広がった形になる。")]
        public int widthInCells = 64;
        [Min(16), Tooltip("フロア全体の奥行き（セル数）。")]
        public int heightInCells = 64;
        [Min(0.5f), Tooltip("1 セルの一辺（メートル）。部屋テンプレートの cellSize と揃えること。")]
        public float cellSize = 3f;

        [Header("部屋")]
        [Tooltip("この階で使う部屋テンプレートの一覧。層ごとに差し替えてテーマを変える。")]
        public RoomTemplateCatalog roomCatalog;
        [Min(3), Tooltip("1 フロアの部屋数の下限。ここに届かなかったら別の形で作り直す。")]
        public int minRoomCount = 10;
        [Min(3), Tooltip("1 フロアの部屋数の上限。実際の部屋数はこの範囲からシードごとに決まる。")]
        public int maxRoomCount = 20;
        [Min(1), Tooltip("部屋数が下限に届かないときに最初から作り直す回数の上限。")]
        public int maxPackAttempts = 6;
        [Min(0), Tooltip("最初の部屋をフロア中心からずらす最大セル数。0 なら必ず中心から生える。")]
        public int startJitter = 4;
        [Tooltip("この階にショップ部屋を 1 つ置くか。")]
        public bool includeShop = true;

        [Header("接続")]
        [Min(0), Tooltip("行き止まりを減らすために追加で開けるドアの本数の上限。ドア候補がたまたま向かい合った所にしか開かないので、必ずこの本数になるわけではない。")]
        public int extraDoorCount = 2;

        [Header("共通パーツ")]
        [Tooltip("セル 1 つ分の壁。ローカル原点が壁の中心で、+Z 側が部屋の外を向く。")]
        public GameObject wallPrefab;
        [Tooltip("セル 1 つ分のドア。向きは壁と同じ規約。")]
        public GameObject doorPrefab;
        [Tooltip("上り階段。上り階段の部屋の中央に 1 つ置かれる。")]
        public GameObject stairUpPrefab;
        [Tooltip("下り階段。下り階段の部屋の中央に 1 つ置かれる。")]
        public GameObject stairDownPrefab;
        [Tooltip("ショップの目印。ショップ部屋の中央に 1 つ置かれる。")]
        public GameObject shopMarkerPrefab;

        public FloorGenerationParams BuildParams()
        {
            var parameters = new FloorGenerationParams
            {
                Width = widthInCells,
                Height = heightInCells,
                MinRoomCount = minRoomCount,
                MaxRoomCount = maxRoomCount,
                MaxPackAttempts = maxPackAttempts,
                StartJitter = startJitter,
                ExtraDoorCount = extraDoorCount,
                IncludeShop = includeShop,
                Templates = roomCatalog != null
                    ? roomCatalog.BuildTemplateData()
                    : new System.Collections.Generic.List<Data.RoomTemplateData>(),
            };
            return parameters;
        }

#if UNITY_EDITOR
        /// <summary>古いアセットを開いたときや手入力で範囲が逆転したときに、生成器が例外を投げないよう均す。</summary>
        private void OnValidate()
        {
            minRoomCount = Mathf.Max(includeShop ? 4 : 3, minRoomCount);
            maxRoomCount = Mathf.Max(minRoomCount, maxRoomCount);
        }
#endif
    }
}
