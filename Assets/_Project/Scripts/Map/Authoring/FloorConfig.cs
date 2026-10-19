using UnityEngine;
using UnityEngine.Rendering;
using TpsDungeon.Map.Generation;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>階の間取りの作り方。</summary>
    public enum FloorLayoutShape
    {
        /// <summary>部屋を成長させて毎回違う形に作る（普通の階）。</summary>
        Random = 0,
        /// <summary>決まった間取り（入口 → 横長の部屋 → 大扉 → ボス部屋 → 閉ざされた扉 → 出口）。BossFloorLayout を見る。</summary>
        Boss = 1,
    }

    /// <summary>
    /// 1 つの階の生成設定。階ごとに 1 つ作り、その階のシーンの FloorBootstrap から参照する。
    /// 上の層ほど広くする、といった層ごとの差はこのアセットの値で表現する。
    /// </summary>
    [CreateAssetMenu(fileName = "FloorConfig", menuName = "TPS Dungeon/Floor Config")]
    public sealed class FloorConfig : ScriptableObject
    {
        /// <summary>ボス階のカタログで、各部屋に使う Prefab の名前（テンプレート ID）。</summary>
        public const string BossEntranceId = "Room_Stair";
        public const string BossHallId = "Room_BossHall";
        public const string BossArenaId = "Room_BossArena";

        [Header("間取り")]
        [Tooltip("Random は毎回違う形、Boss は決まった間取り（カタログに Room_Stair・Room_BossHall・Room_BossArena を入れる。部屋数・接続の値は使わない）。")]
        public FloorLayoutShape layoutShape = FloorLayoutShape.Random;

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

        [Header("高さ")]
        [Min(2f), Tooltip("壁の高さ（メートル）。天井はこの高さに張り、ドアの枠はこの高さに伸びる。壁・窓は実寸で作るので、変えたら「壁・床・天井のパーツを生成」を回すこと。")]
        public float wallHeight = 5f;
        [Min(0f), Tooltip("この階の床が地面からどれだけ高いか（メートル）。高い建物の上の階なら大きくする。窓の外の景色はこの分だけ下に置き、部屋の下には地面まで土台を置く。Settings/Tables/Floors.csv の elevation 列で層ごとに設定する（取り込むとここと土台が書き換わる）。変えたら「階の外の景色を置く」と「地上のダンジョンの塔を建て直す」を回すこと。")]
        public float elevation = 40f;

        [Header("共通パーツ")]
        [Tooltip("セル 1 つ分の壁。ローカル原点が壁の下端の中心で、+Z 側が部屋の外を向く。幅 cellSize・高さ wallHeight の実寸で作る（FloorBuilder はスケールしない）。")]
        public GameObject wallPrefab;
        [Tooltip("セル 1 つ分の窓付きの壁。外に面した壁にだけ使う。向きと大きさは壁と同じ。未設定なら窓を開けない。")]
        public GameObject windowWallPrefab;
        [Range(0f, 1f), Tooltip("外に面した壁 1 枚ごとに、窓付きの壁にする確率。")]
        public float windowChance = 0.35f;
        [Tooltip("セル 1 つ分の天井。ローカル原点が下面の中心で、cellSize 四方の実寸で作る。未設定なら天井を張らない。")]
        public GameObject ceilingPrefab;
        [Tooltip("セル 1 つ分の土台（建物の下の部分）。ローカル原点が上面の中心で、cellSize 四方・深さ elevation（＋少し）の実寸で作る。未設定か elevation が 0 なら置かない。")]
        public GameObject foundationPrefab;
        [Tooltip("セル 1 つ分のドア。向きは壁と同じ規約。")]
        public GameObject doorPrefab;
        [Tooltip("上の階へ続く上り階段。出口の部屋の FeatureCell の中心に 1 つ置かれる。FloorFeature を持てば、そのセルの天井などを置き換えられる。")]
        public GameObject stairUpPrefab;
        [Tooltip("下の階へ続く下り階段。入口の部屋（パーティーが着く部屋）の FeatureCell の中心に 1 つ置かれる。子の SceneGate に FloorBootstrap が行き先を入れる。")]
        public GameObject stairDownPrefab;
        [Tooltip("ショップの目印。ショップ部屋の中央に 1 つ置かれる。")]
        public GameObject shopMarkerPrefab;
        [Tooltip("大扉（両開き）。ボス部屋の入口など、レイアウトが大扉にした辺に普通のドアの代わりに置く。向きと大きさは壁と同じ。未設定なら普通のドア。")]
        public GameObject grandDoorPrefab;
        [Tooltip("ショートカット解放用の場所の目印。レイアウトが決めたセルの中央に置く。")]
        public GameObject shortcutMarkerPrefab;

        [Header("置き物")]
        [Tooltip("部屋に置く物（松明・柱・机・小物・木箱・宝箱）のプレハブの一覧。未設定なら何も置かない。")]
        public RoomPropSet propSet;
        [Min(0f), Tooltip("普通の部屋のセル 1 つあたりの木箱の数の平均。Settings/Tables/Floors.csv の crates_per_cell 列で層ごとに設定する。")]
        public float cratesPerCell = 0.5f;
        [Range(0f, 1f), Tooltip("普通の部屋 1 つに宝箱を 1 つ置く確率。Settings/Tables/Floors.csv の chest_chance 列で層ごとに設定する。")]
        public float chestChance = 0.2f;
        [Tooltip("木箱・宝箱から武器が出たときのランク（E・D・C・B・A・S の順）の重み。Settings/Tables/BreakableWeaponRanks.csv で層ごとに設定する。")]
        public float[] boxWeaponRanks = { 1f, 0f, 0f, 0f, 0f, 0f };

        [Header("見た目")]
        [Tooltip("壁・天井・ドア・階段の素材。層ごとに差し替えて見た目を変える。共通パーツはこれで作るので、変えたら「壁・床・天井のパーツを生成」を回すこと。")]
        public FloorTheme theme;
        [Tooltip("階の明るさ・色味（天井で日の光が遮られる室内を持ち上げるなど）。生成のたびに Global Volume として置く。未設定なら置かない。")]
        public VolumeProfile volumeProfile;

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

        /// <summary>ボス階の 3 つの部屋のテンプレートをカタログから ID で引く。無いものは null のまま（BossFloorLayout が知らせる）。</summary>
        public BossFloorTemplates BuildBossTemplates()
        {
            var templates = new BossFloorTemplates();
            if (roomCatalog == null) return templates;

            foreach (var data in roomCatalog.BuildTemplateData())
            {
                if (data.Id == BossEntranceId) templates.Entrance = data;
                else if (data.Id == BossHallId) templates.Hall = data;
                else if (data.Id == BossArenaId) templates.Arena = data;
            }
            return templates;
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
