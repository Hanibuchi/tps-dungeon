using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Gen = TpsDungeon.Items.Editor.PlaceholderItemAssetGenerator;

namespace TpsDungeon.Items.Editor
{
    /// <summary>
    /// 武器まわりのデータ一式をコードから作る。数値は仮で、ここを直して作り直すか、できたアセットを直接いじって調整する。
    /// - エンチャントの付き方・エンチャント 22 種・武器種 01（片手近距離）・02（ダッシュ突き）・04（両手近距離）・05（叩きつけ）・09（弓）・11（持続弓）・
    ///   17（治癒持続）・19（電撃）・20（範囲連置）・21（火炎放射器）・26（お守り）・27（盾）・28（投擲）・30（召喚）: Assets/_Project/Items/Weapons/
    /// - ランクの色と、ユニークの光・落ちた音: Assets/_Project/Resources/Weapons/（ゲーム中に WeaponRankTable.Default で引くため Resources に置く）
    /// - 武器 41 本（Notion の武器一覧 DB で武器種＝01 の 3 本、02・04・05 の 1 本ずつ、09 の 3 本、11・19・20・21 の 4 本ずつ、26 の 3 本、27 の 4 本、
    ///   28 の 3 本と、一覧に無い 28 の石ころ、17 の 1 本、30 の 4 本）と、拾える物・手に持つ見た目のプレハブ
    /// - 召喚で呼び出す置物（Prefabs/Items/Summons/。木の人形はプリミティブ、石像・古代兵・英霊は Blink の人型を Kevin の構えの姿勢で焼いたメッシュ）と、
    ///   治癒持続で投げる種（Projectile_HealSeed）
    /// - 弓の飛ぶ矢の見た目（Projectile_Arrow）と、持続弓の範囲の円・雷の線の材質（RangeRing）
    /// - 範囲連置の 1 列の結晶（Effect_CrystalLine。Hovl の Crystals front attack を 1 本にしたバリアント）と、火炎放射器の炎（Effect_Flame）
    /// - 素手の武器種 00 と、素手のときに振る武器（Weapon_Fists。インベントリには入れない）
    /// - 振り・命中のエフェクトは ThirdParty/VFX のプレハブを、効果音は ThirdParty/Sound の効果音ラボの音を武器種に入れる
    /// - 枠と情報欄の絵は、手に持つ見た目のモデルを斜めから撮って作る（背景は透明）
    /// 見た目は ThirdParty の Blink の武器（FreeSwords の剣・Stylized のハンマーと杖と盾・LowPoly の弓と杖と盾）があればそれを、無ければプリミティブの剣を使う。矢は Pandazole の矢。
    /// お守りは GanzSe の装身具（耳飾り・ペンダント・首飾り）。召喚は Daniel Riches の Books Essentials の本。
    /// 杖の手に持つ見た目には、杖の先に "Tip"（WeaponTypeDefinition.StaffTipName）を置く。雷・棘・炎はそこから出る。
    /// 何度実行しても同じ結果になる（既存アセットは上書き、GUID は保つ）。
    /// </summary>
    public static class PlaceholderWeaponAssetGenerator
    {
        public const string WeaponsFolder = Gen.ItemsFolder + "/Weapons";
        private const string EnchantmentsFolder = WeaponsFolder + "/Enchantments";
        public const string RankTablePath = "Assets/_Project/Resources/" + WeaponRankTable.ResourcePath + ".asset";
        private const string OldRankTablePath = WeaponsFolder + "/WeaponRankTable.asset";
        public const string RollSettingsPath = WeaponsFolder + "/EnchantmentRollSettings.asset";
        public const string OneHandedTypePath = WeaponsFolder + "/WeaponType_01_OneHanded.asset";
        public const string DashThrustTypePath = WeaponsFolder + "/WeaponType_02_DashThrust.asset";
        public const string TwoHandedTypePath = WeaponsFolder + "/WeaponType_04_TwoHanded.asset";
        public const string HammerTypePath = WeaponsFolder + "/WeaponType_05_Hammer.asset";
        public const string BowTypePath = WeaponsFolder + "/WeaponType_09_Bow.asset";
        public const string RainBowTypePath = WeaponsFolder + "/WeaponType_11_RainBow.asset";
        public const string LightningTypePath = WeaponsFolder + "/WeaponType_19_Lightning.asset";
        public const string SpikeLineTypePath = WeaponsFolder + "/WeaponType_20_SpikeLine.asset";
        public const string FlamethrowerTypePath = WeaponsFolder + "/WeaponType_21_Flamethrower.asset";
        public const string CharmTypePath = WeaponsFolder + "/WeaponType_26_Charm.asset";
        public const string ShieldTypePath = WeaponsFolder + "/WeaponType_27_Shield.asset";
        public const string ThrowTypePath = WeaponsFolder + "/WeaponType_28_Throw.asset";
        public const string HealFieldTypePath = WeaponsFolder + "/WeaponType_17_HealField.asset";
        public const string SummonTypePath = WeaponsFolder + "/WeaponType_30_Summon.asset";
        public const string SummonsFolder = Gen.PrefabsFolder + "/Summons";
        public const string HealSeedPrefabPath = Gen.PrefabsFolder + "/Projectile_HealSeed.prefab";
        private const string SummonHumanMeshPath = SummonsFolder + "/Mesh_SummonHuman.asset";
        public const string CrystalLinePrefabPath = Gen.PrefabsFolder + "/Effect_CrystalLine.prefab";
        public const string FlamePrefabPath = Gen.PrefabsFolder + "/Effect_Flame.prefab";
        public const string RangeRingMaterialPath = WeaponsFolder + "/RangeRing.mat";
        public const string AuraMaterialPath = WeaponsFolder + "/RankAura.mat";
        public const string ArrowPrefabPath = Gen.PrefabsFolder + "/Projectile_Arrow.prefab";
        public const string UnarmedTypePath = WeaponsFolder + "/WeaponType_00_Unarmed.asset";
        public const string FistsPath = WeaponsFolder + "/Weapon_Fists.asset";

        private const string HovlPrefabs = "Assets/ThirdParty/VFX/Hovl Studio/Magic effects pack/Prefabs/";
        private const string LanaPrefabs = "Assets/ThirdParty/VFX/Lana Studio/Hyper Casual FX/Prefabs/";
        public const string SlashEffectPath = HovlPrefabs + "Slash effects/Stone slash.prefab";
        // 0.5 秒溜めて（黄色い輪）から、三日月の斬撃を素材の前（+Z）へ秒速 30 m で放つ。
        public const string ChargeSlashEffectPath = HovlPrefabs + "Slash effects/Charge slash blue.prefab";
        private const float ChargeSlashReleaseTime = 0.5f;
        public const string SwordHitEffectPath = HovlPrefabs + "Sparks/Sparks explode white.prefab";
        public const string PunchHitEffectPath = LanaPrefabs + "Flash/Flash_round_ellow.prefab";
        public const string CriticalHitEffectPath = HovlPrefabs + "Hits and explosions/Star hit.prefab";
        public const string ExplosionEffectPath = HovlPrefabs + "Hits and explosions/Explosion.prefab";
        // 叩きつけの着弾は、土煙と地割れ（Ground AOE explosion、素材は 15 m ほどに広がる）に、
        // 土煙の塊・石の破片・火花・閃光を重ねる。着弾の 0.05 秒で光と破片、0.3 秒で土煙の輪が広がる（Play で撮って決めた）。
        public const string SlamDustEffectPath = HovlPrefabs + "AoE effects/Ground AOE explosion.prefab";
        public const string SlamFlashEffectPath = LanaPrefabs + "Flash/Flash_ellow.prefab";
        public const string StonesEffectPath = HovlPrefabs + "Hits and explosions/Stones hit.prefab";
        public const string SparksEffectPath = HovlPrefabs + "Sparks/Sparks explode white.prefab";
        // 着弾点に重ねる土煙（素材は 12 m ほど）。
        public const string DustPuffEffectPath = HovlPrefabs + "Smoke effects/Dust puff.prefab";
        // 持続弓の雨。高さ 8 m・半径 4 m の円錐から 0.15 秒ごとに 20 回隕石を落とし、着弾で石・火花・閃光が散る。
        public const string MeteorsEffectPath = HovlPrefabs + "AoE effects/Meteors AOE.prefab";
        // 雷が当たった所の閃光と火花（素材は 3 m ほど）。
        public const string ElectroHitEffectPath = HovlPrefabs + "Hits and explosions/Electro hit.prefab";
        // 地面を這う結晶。根の粒 7 つが +X を中心に 45° の扇へ秒速 30 m で 0.32 秒走り（約 7.8 m）、通り道に結晶・煙・石を残す。
        public const string CrystalsFrontAttackPath = HovlPrefabs + "AoE effects/Crystals front attack.prefab";
        private const float CrystalsFrontAttackLength = 7.8f;
        private const float CrystalsFrontAttackTravelTime = 0.32f;
        // 炎が当たった所の火花。
        public const string FireHitEffectPath = HovlPrefabs + "Sparks/Sparks explode red.prefab";
        /// <summary>召喚の置物が出るときの魔法陣と、消えるときの煙。治癒の場の見た目。</summary>
        public const string SummonCircleEffectPath = HovlPrefabs + "Magic circles/Magic circle.prefab";
        public const string SmokePuffEffectPath = HovlPrefabs + "Smoke effects/Smoke puff.prefab";
        public const string HealingCircleEffectPath = HovlPrefabs + "Magic circles/Healing circle.prefab";
        // 炎の粒の絵（煙の柔らかい塊を、色を時間で変えて炎に見せる）と、火の粉の点。
        private const string FlameMaterialPath = "Assets/ThirdParty/VFX/Hovl Studio/Magic effects pack/Materials/Smoke26.mat";
        private const string EmberMaterialPath = "Assets/ThirdParty/VFX/Hovl Studio/Magic effects pack/Materials/Point.mat";

        // 効果音ラボの音はどれも頭の無音が 0.07 秒以下なので、判定の瞬間に鳴らしてもずれない。
        private const string ArmsSounds = "Assets/ThirdParty/Sound/SoundEffect-Lab/Arms/";
        public const string SwordSwingSoundPath = ArmsSounds + "剣の素振り2.mp3";
        // 両手剣の振りは片手剣と別の風切り。
        public const string GreatswordSwingSoundPath = ArmsSounds + "剣の素振り1.mp3";
        public const string SwordHitSoundPath = ArmsSounds + "剣で斬る2.mp3";
        public const string SwordFinisherSoundPath = ArmsSounds + "剣で斬る1.mp3";
        public const string PunchSwingSoundPath = ArmsSounds + "パンチ素振り.mp3";
        public const string PunchHitSoundPath = ArmsSounds + "打撃1.mp3";
        public const string ExplosionSoundPath = ArmsSounds + "爆発2.mp3";
        // クリティカルは命中の音に重ねるので、命中（剣で斬る）とは別の鋭い斬撃。
        public const string CriticalHitSoundPath = ArmsSounds + "刀で斬る5.mp3";
        public const string DashThrustSoundPath = ArmsSounds + "居合抜き1.mp3";
        public const string BowReleaseSoundPath = ArmsSounds + "弓矢を放つ.mp3";
        public const string ArrowHitSoundPath = ArmsSounds + "弓矢が刺さる.mp3";
        public const string BowDrawSoundPath = ArmsSounds + "弓を引き絞る1.mp3";
        // 持続弓の雨は隕石の火の玉が降る見た目なので、炎の音を降り始めと刻みごとに鳴らす。
        public const string RainSoundPath = ArmsSounds + "火炎魔法1.mp3";
        public const string LightningSoundPath = ArmsSounds + "雷魔法1.mp3";
        public const string IceSoundPath = ArmsSounds + "氷魔法2.mp3";
        public const string FlameSoundPath = ArmsSounds + "火炎魔法1.mp3";
        public const string ThrowSoundPath = ArmsSounds + "ナイフを投げる.mp3";
        /// <summary>治癒持続で種を投げる音・場を張る音、召喚で置物を呼ぶ音。</summary>
        public const string SeedThrowSoundPath = ArmsSounds + "手裏剣を投げる.mp3";
        public const string HealSoundPath = ArmsSounds + "回復魔法1.mp3";
        public const string SummonSoundPath = ArmsSounds + "パワーアップ.mp3";
        // 投げた物が壁や床に当たった音。
        public const string ThrownStickSoundPath = ArmsSounds + "雪玉をぶつける.mp3";
        // 「ハンマーを叩きつける音」は調達済みだが未取り込み。届いたらここだけ差し替える。振りと命中（着弾）の両方に使う。
        public const string HammerSlamSoundPath = ArmsSounds + "打撃3.mp3";
        // ユニークが床に落ちた音（捨てたときも）。
        public const string UniqueDropSoundPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/きらーん2.mp3";
        // ユニークがまとう光の粒の絵（Hovl の丸い点。灰色を透明度として読む）。
        private const string AuraPointTexturePath = "Assets/ThirdParty/VFX/Hovl Studio/Magic effects pack/Textures/Point1.png";

        private const string FreeSwords = "Assets/ThirdParty/3D Model/Blink/Weapons/FreeSwords/Prefabs/";
        private const string StylizedHammers = "Assets/ThirdParty/3D Model/Blink/Weapons/Stylized/Hammers/_PrefabsHammers/";
        private const string LowPolyWeapons = "Assets/ThirdParty/3D Model/Blink/Weapons/LowPoly/FreeRPGWeapons/_PREFABS/";
        private const string StylizedStaves = "Assets/ThirdParty/3D Model/Blink/Weapons/Stylized/Staves/_PrefabsStaves/";
        private const string StylizedShields = "Assets/ThirdParty/3D Model/Blink/Weapons/Stylized/Shields/_PrefabsShields/";
        private const string Accessories = "Assets/ThirdParty/3D Model/URP GanzSe Free Character Accessories/Prefabs/";
        private const string StylizedDaggers = "Assets/ThirdParty/3D Model/Blink/Weapons/Stylized/Daggers/_PrefabsDaggers/";
        private const string PandazolePrefabs = "Assets/ThirdParty/3D Model/Pandazole_Ultimate_Pack/Pandazole Survival Crafting Pack/Prefabs/";
        private const string BooksPrefabs = "Assets/ThirdParty/3D Model/DanielRiches/BooksEssentials/Prefabs/";
        private const string StylizedHumanPath = "Assets/ThirdParty/3D Model/Blink/Character/Stylized/Humans/Prefabs_Humans/HumanMale_Character_Free.prefab";
        /// <summary>石像・古代兵・英霊の置物の姿勢（Kevin の片手武器の構えの 1 フレーム）。</summary>
        private const string SummonPoseClipPath = "Assets/ThirdParty/Animation/Kevin Iglesias/Human Animations/Animations/Male/Combat/1H/HumanM@CombatIdle1H01.fbx";
        // 矢は長さ 1.08 m で、素材の -Z に矢じり、+Z に矢羽根がある。
        private const string ArrowModelPath = PandazolePrefabs + "Arrow_01.prefab";

        /// <summary>飛ぶ矢の見た目の長さ（m）。</summary>
        private const float ArrowLength = 0.8f;

        /// <summary>弓の見た目の長さ（m、上下の端から端）。</summary>
        private const float BowLength = 1.2f;

        /// <summary>片手剣の見た目の長さ（m）。素材の大きさはまちまちなので、武器ごとの長さに合わせて縮める。</summary>
        private const float SwordLength = 0.9f;

        /// <summary>杖の見た目の長さ（m）と、下端から握る所までの長さ（m）。</summary>
        private const float StaffLength = 1.5f;
        private const float StaffGrip = 0.55f;

        /// <summary>盾の見た目の大きさ（m、いちばん長い辺）。城壁の大盾だけ大きくする。</summary>
        private const float ShieldLength = 0.65f;

        /// <summary>お守りの見た目の大きさ（m、いちばん長い辺）。</summary>
        private const float CharmLength = 0.2f;

        /// <summary>召喚の本の見た目の大きさ（m、いちばん長い辺）と、片手杖の長さ・握る所（m）。</summary>
        private const float SummonerLength = 0.3f;
        private const float WandLength = 0.75f;
        private const float WandGrip = 0.12f;

        /// <summary>人型の置物の背の高さ（m）。主人公（約 1.8 m）と並べて見劣りしないよう、少し大きくする。</summary>
        private const float SummonHumanHeight = 1.95f;

        /// <summary>拾える物の判定の幅と高さ（m）。奥行きは武器の長さに合わせる。</summary>
        private static readonly Vector2 PickupVolume = new Vector2(0.35f, 0.3f);

        /// <summary>モデルを撮った絵の大きさ（px）。枠と情報欄は 64px なので、高解像度の画面でも粗くならないよう倍で撮る。</summary>
        private const int IconSize = 128;

        /// <summary>
        /// 絵の周りに付ける縁取りの色。枠の地は暗い石で、鉄や黒い刃は溶けてしまうので、古い紙色の明るい線で輪郭を立てる。
        /// </summary>
        private static readonly Color OutlineColor = new Color32(236, 226, 206, 255);

        /// <summary>縁取りの太さ（撮った絵の px）。この内側は濃く、外へ <see cref="OutlineSoftness"/> px かけて消える。枠では半分の大きさで出る。</summary>
        private const float OutlineWidth = 2.5f;

        private const float OutlineSoftness = 1.5f;

        /// <summary>背景の透明が残らないときに撮り直す、抜き取り用の背景色。</summary>
        private static readonly Color KeyColor = new Color(1f, 0f, 1f, 1f);

        private sealed class WeaponSpec
        {
            public string Id;
            public string Name;
            public WeaponRank Rank;
            public float Strength;
            public string Description;
            /// <summary>武器種（WeaponTypeDefinition のアセットのパス）。</summary>
            public string TypePath;
            /// <summary>見た目の素材のプレハブ（フルパス）。無ければプリミティブの剣で代える。</summary>
            public string ModelPath;
            /// <summary>見た目の長さ（m）。</summary>
            public float Length = SwordLength;
            public Color Blade;
            public Color Hilt;
            /// <summary>真ん中を原点に置く（弓。真ん中の握りを手に持つ）。偽なら下端（剣の柄の根元）から Grip 上がった所を原点に。</summary>
            public bool CenterPivot;
            /// <summary>CenterPivot でないとき、下端から握る所までの長さ（m）。剣は 0（柄の根元を握る）、杖は少し上を握る。</summary>
            public float Grip;
            /// <summary>素材の上下を入れ替える（いちばん長い軸を +Y にしたとき、先が下を向く素材）。</summary>
            public bool Flip;
            /// <summary>手に持つ見た目の先（Length − Grip の高さ）に "Tip" を置く（杖）。</summary>
            public bool StaffTip;
            /// <summary>絵を撮るとき裏返す（盾は素材の -Z が裏の持ち手なので、表を撮るために回す）。</summary>
            public bool IconBackside;
            /// <summary>ユニークに必ず付くエンチャント。同じ種類を並べると重ねがけ。</summary>
            public EnchantmentKind[] FixedEnchantments = Array.Empty<EnchantmentKind>();
            /// <summary>投擲で投げたとき縦に回る速さ（度/秒）。0 なら回らない。</summary>
            public float ThrownSpinRate;
            /// <summary>投擲で投げたとき、壁や床に刺さらず跳ね返る（石）。</summary>
            public bool ThrownBounces;
            /// <summary>素材の色を使わず、この色の無地で塗る（Pandazole の石はパレットの白いところを使っていて雪玉に見えるため）。</summary>
            public Color? Tint;
            /// <summary>召喚で呼び出す置物のプレハブ。召喚でなければ null。</summary>
            public GameObject SummonModel;
        }

        [MenuItem("Tools/TPS Dungeon/プレースホルダの武器を生成")]
        public static void Generate()
        {
            Gen.EnsureFolder(WeaponsFolder);
            Gen.EnsureFolder(EnchantmentsFolder);
            Gen.EnsureFolder(Gen.IconsFolder);
            Gen.EnsureFolder(Gen.PrefabsFolder);

            WriteRankTable(WriteAuraMaterial());
            EnchantmentRollSettings roll = WriteRollSettings();
            Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments = WriteEnchantments();
            Material ring = WriteRangeRingMaterial();
            GameObject arrow = WriteArrowPrefab();
            var types = new Dictionary<string, WeaponTypeDefinition>
            {
                [OneHandedTypePath] = WriteOneHandedType(enchantments, roll),
                [DashThrustTypePath] = WriteDashThrustType(enchantments, roll),
                [TwoHandedTypePath] = WriteTwoHandedType(enchantments, roll),
                [HammerTypePath] = WriteHammerType(enchantments, roll),
                [BowTypePath] = WriteBowType(enchantments, roll, arrow, ring),
                [RainBowTypePath] = WriteRainBowType(enchantments, roll, arrow, ring),
                [LightningTypePath] = WriteLightningType(enchantments, roll, ring),
                [SpikeLineTypePath] = WriteSpikeLineType(enchantments, roll, WriteCrystalLinePrefab()),
                [FlamethrowerTypePath] = WriteFlamethrowerType(enchantments, roll, WriteFlamePrefab()),
                [CharmTypePath] = WriteCharmType(enchantments, roll),
                [ShieldTypePath] = WriteShieldType(enchantments, roll),
                [ThrowTypePath] = WriteThrowType(enchantments, roll, ring),
                [HealFieldTypePath] = WriteHealFieldType(enchantments, roll, ring, WriteHealSeedPrefab(ring)),
                [SummonTypePath] = WriteSummonType(enchantments, roll, ring),
            };

            summonModels = WriteSummonModels();
            foreach (WeaponSpec spec in Weapons()) WriteWeapon(spec, types[spec.TypePath], enchantments);

            WriteFists(WriteUnarmedType());

            AssetDatabase.SaveAssets();
            Debug.Log($"仮の武器データを作った: {WeaponsFolder}");
        }

        /// <summary>生成器が作った武器を全部読む。確認用シーンに置くときに使う。</summary>
        public static List<WeaponDefinition> LoadAll()
        {
            var result = new List<WeaponDefinition>();
            foreach (WeaponSpec spec in Weapons())
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>($"{WeaponsFolder}/{spec.Id}.asset");
                if (weapon != null) result.Add(weapon);
            }

            return result;
        }

        private static IEnumerable<WeaponSpec> Weapons()
        {
            yield return new WeaponSpec
            {
                Id = "Weapon_RustySword", Name = "錆びた片手剣", Rank = WeaponRank.E, Strength = 5f,
                Description = "刃こぼれだらけだが、振れば斬れる。",
                TypePath = OneHandedTypePath, ModelPath = FreeSwords + "Sword1_Bronze.prefab",
                Blade = new Color32(150, 110, 80, 255), Hilt = new Color32(90, 60, 40, 255),
            };
            yield return new WeaponSpec
            {
                Id = "Weapon_IronSword", Name = "鉄の片手剣", Rank = WeaponRank.C, Strength = 21f,
                Description = "兵士が腰に下げていた、ありふれた造りの剣。",
                TypePath = OneHandedTypePath, ModelPath = FreeSwords + "Sword15_Iron.prefab",
                Blade = new Color32(190, 196, 204, 255), Hilt = new Color32(80, 60, 50, 255),
            };
            yield return new WeaponSpec
            {
                Id = "Weapon_BlueSteelRapier", Name = "蒼鋼の細剣", Rank = WeaponRank.A, Strength = 37f,
                Description = "薄く鍛えられた刃が、風を裂いて敵を刻む。",
                TypePath = OneHandedTypePath, ModelPath = FreeSwords + "Sword13_Blue.prefab",
                Blade = new Color32(110, 170, 230, 255), Hilt = new Color32(50, 60, 110, 255),
            };
            yield return new WeaponSpec
            {
                Id = "Weapon_WoodenRapier", Name = "木柄の刺突剣", Rank = WeaponRank.E, Strength = 8f,
                Description = "踏み込みの勢いを、そのまま切っ先に乗せる。",
                TypePath = DashThrustTypePath, ModelPath = FreeSwords + "Sword2_Red.prefab", Length = 1.0f,
                Blade = new Color32(180, 180, 175, 255), Hilt = new Color32(120, 80, 45, 255),
            };
            yield return new WeaponSpec
            {
                Id = "Weapon_ChippedGreatsword", Name = "欠けた両手剣", Rank = WeaponRank.D, Strength = 11f,
                Description = "重さだけは一人前。振り回せば道が開く。",
                TypePath = TwoHandedTypePath, ModelPath = FreeSwords + "Sword4_Red.prefab", Length = 1.35f,
                Blade = new Color32(120, 120, 125, 255), Hilt = new Color32(60, 45, 35, 255),
            };
            yield return new WeaponSpec
            {
                Id = "Weapon_StoneHammer", Name = "石のハンマー", Rank = WeaponRank.D, Strength = 14f,
                Description = "岩を削り出しただけの武骨な槌。当たれば潰れる。",
                TypePath = HammerTypePath, ModelPath = StylizedHammers + "Hammer1_1_3.prefab", Length = 1.1f,
                Blade = new Color32(130, 125, 120, 255), Hilt = new Color32(95, 70, 45, 255),
            };

            // 09 弓
            yield return Bow("Weapon_OldBow", "古びた弓", WeaponRank.E, 3f, "弦が緩み、矢はまっすぐ飛ばない。",
                BowTypePath, "Bow_Basic.prefab");
            yield return Bow("Weapon_HunterBow", "狩人の弓", WeaponRank.C, 19f, "森で獣を追うために作られた、扱いやすい弓。",
                BowTypePath, "Bow_Medium.prefab");
            yield return Bow("Weapon_SoldierLongbow", "兵士の長弓", WeaponRank.A, 41f, "放たれた矢は、落ちた場所を炎の輪に変える。",
                BowTypePath, "Bow_Epic.prefab", 1.35f);

            // 11 持続弓
            yield return Bow("Weapon_PoisonArrowBow", "毒矢の弓", WeaponRank.E, 6f, "降り注いだ矢が、地面にじわりと毒を残す。",
                RainBowTypePath, "Bow_Basic.prefab");
            yield return Bow("Weapon_ArrowRainBow", "矢雨の弓", WeaponRank.C, 23f, "一射で空を埋め、狙った地点に矢が降り続ける。",
                RainBowTypePath, "Bow_Medium.prefab");
            yield return Bow("Weapon_SkyPiercerLongbow", "天穿の長弓", WeaponRank.A, 40f, "天へ放たれた矢は、幾重にも分かれて落ちてくる。",
                RainBowTypePath, "Bow_Epic.prefab", 1.35f);
            // ユニークなので固定のエンチャントが付くが、何を付けるかはまだ決めていない（今は何も付かない）。
            yield return Bow("Weapon_EndlessDownpour", "終わらぬ驟雨", WeaponRank.Unique, 23f, "一度降り始めた矢の雨は、いつまでも止むことがない。",
                RainBowTypePath, "Bow_Epic.prefab", 1.35f);

            // 19 電撃。ユニークは飛び移りを大きく増やす（数 ×3 で 3 ＋ 6 回）。固定のエンチャントは仮。
            yield return Staff("Weapon_ChargedStaff", "帯電した杖", WeaponRank.E, 10f, "触れた敵から敵へ、火花が短く飛び移る。",
                LightningTypePath, LowPolyWeapons + "Staff_Basic.prefab");
            yield return Staff("Weapon_ThunderStaff", "雷撃の杖", WeaponRank.B, 29f, "走った稲光が、群れをまとめて焼き払う。",
                LightningTypePath, LowPolyWeapons + "Staff_Medium.prefab");
            yield return Staff("Weapon_StormEmperorStaff", "嵐帝の杖", WeaponRank.S, 48f, "一度放てば、雷は獲物を残らず数え上げる。",
                LightningTypePath, LowPolyWeapons + "Staff_Epic.prefab");
            yield return Staff("Weapon_ThunderGodPike", "雷神の鉾杖", WeaponRank.Unique, 48f, "放たれた雷は尽きることなく、敵から敵へと渡り続ける。",
                LightningTypePath, StylizedStaves + "Staff5_1_1.prefab",
                EnchantmentKind.ProjectileCount, EnchantmentKind.ProjectileCount, EnchantmentKind.ProjectileCount);

            // 20 範囲連置。ユニークは列を増やして延ばす（数 ×2 で 3 列、持続時間 ×2 で 1.4 倍の長さ）。固定のエンチャントは仮。
            yield return Staff("Weapon_StoneSpikeStaff", "石棘の杖", WeaponRank.D, 13f, "地面から石の棘が、まっすぐ連なって突き出す。",
                SpikeLineTypePath, LowPolyWeapons + "Staff_Basic.prefab");
            yield return Staff("Weapon_IceFangStaff", "氷牙の杖", WeaponRank.B, 32f, "足元から氷の牙が次々と生え、敵を追い立てる。",
                SpikeLineTypePath, LowPolyWeapons + "Staff_Medium.prefab");
            yield return Staff("Weapon_EarthSplitterStaff", "大地裂の杖", WeaponRank.S, 51f, "地面が波打ち、裂け目が敵の列を飲み込む。",
                SpikeLineTypePath, LowPolyWeapons + "Staff_Epic.prefab");
            yield return Staff("Weapon_IcePrisonScepter", "氷獄の王笏", WeaponRank.Unique, 32f, "地を這う氷の列が幾筋も伸び、逃げ場を塞いでいく。",
                SpikeLineTypePath, StylizedStaves + "Staff4_1_1.prefab",
                EnchantmentKind.ProjectileCount, EnchantmentKind.ProjectileCount, EnchantmentKind.Duration, EnchantmentKind.Duration);

            // 21 火炎放射器。ユニークは炎を壁のように広げる（サイズ ×3・数 ×2）。固定のエンチャントは仮。
            yield return Staff("Weapon_EmberStaff", "種火の杖", WeaponRank.D, 15f, "先端から、細く頼りない炎が伸びる。",
                FlamethrowerTypePath, LowPolyWeapons + "Staff_Basic.prefab");
            yield return Staff("Weapon_HellfireStaff", "業火の杖", WeaponRank.B, 35f, "押さえている間、炎は途切れず前方を舐める。",
                FlamethrowerTypePath, LowPolyWeapons + "Staff_Medium.prefab");
            yield return Staff("Weapon_DragonBreathStaff", "竜息の杖", WeaponRank.S, 55f, "杖の口から、竜の吐息と同じ熱がほとばしる。",
                FlamethrowerTypePath, LowPolyWeapons + "Staff_Epic.prefab");
            yield return Staff("Weapon_PurgatoryRoar", "煉獄の咆哮", WeaponRank.Unique, 15f, "吐き出す炎は壁となり、前にあるものすべてを飲み込む。",
                FlamethrowerTypePath, StylizedStaves + "Staff2_2_6.prefab",
                EnchantmentKind.Size, EnchantmentKind.Size, EnchantmentKind.Size, EnchantmentKind.ProjectileCount, EnchantmentKind.ProjectileCount);

            // お守り（26）。強さは今は使わない（ランクの目安）。
            yield return Gear("Weapon_RustyBell", "錆びた鈴", WeaponRank.E, 4f, "かすかに鳴り続け、持ち主の歩みを軽くする。",
                CharmTypePath, Accessories + "FCA_Earring_Type1_Color1.prefab", CharmLength);
            yield return Gear("Weapon_OldTalisman", "古びた護符", WeaponRank.D, 14f, "文字はもう読めないが、効き目だけは残っている。",
                CharmTypePath, Accessories + "FCA_Pendant_Type1_Color1.prefab", CharmLength);
            yield return Gear("Weapon_EmeraldNecklace", "翠玉の首飾り", WeaponRank.C, 24f, "石が濁るたび、災いをひとつ肩代わりしている。",
                CharmTypePath, Accessories + "FCA_Necklace_Type1_Color3.prefab", CharmLength * 1.5f);

            // 盾（27）。強さは防御力。
            yield return Gear("Weapon_WoodenShield", "木の盾", WeaponRank.E, 6f, "板を打ちつけただけの盾。無いよりはましだ。",
                ShieldTypePath, StylizedShields + "Shield3_1_1.prefab", ShieldLength);
            yield return Gear("Weapon_IronRoundShield", "鉄の円盾", WeaponRank.D, 18f, "打ち込まれた刃を、鈍い音とともに受け流す。",
                ShieldTypePath, StylizedShields + "Shield2_1_2.prefab", ShieldLength);
            yield return Gear("Weapon_RampartShield", "城壁の大盾", WeaponRank.B, 30f, "構えれば、そこが一人分の城壁になる。",
                ShieldTypePath, LowPolyWeapons + "Shield_Medium.prefab", ShieldLength * 1.3f);
            // ユニークの固定エンチャントは仮。
            yield return Gear("Weapon_UnfallingHolyShield", "不落の聖盾", WeaponRank.Unique, 18f, "傷を受けるそばから、盾の輝きが持ち主を癒していく。",
                ShieldTypePath, LowPolyWeapons + "Shield_Epic.prefab", ShieldLength,
                EnchantmentKind.Regen, EnchantmentKind.Regen, EnchantmentKind.Regen, EnchantmentKind.Defense);
            // 28 投擲。手に持った見た目をそのまま投げる。ナイフと手斧は縦に回り、投槍は先を前へ向けてまっすぐ飛ぶ。
            // 石は Notion の武器一覧に無い（調達した素材の「投擲（石…）」に合わせて足した）。刺さらずに跳ね返って転がる。強さは仮。
            var stone = Thrown("Weapon_ThrowingStone", "石ころ", WeaponRank.E, 6f, "道端で拾った手ごろな石。当たれば痛い。",
                PandazolePrefabs + "Rock_01.prefab", 0.14f, 0f, 720f);
            stone.CenterPivot = true;
            stone.ThrownBounces = true;
            stone.Tint = new Color32(120, 114, 106, 255);
            yield return stone;
            yield return Thrown("Weapon_ThrowingKnife", "投げナイフ", WeaponRank.D, 18f, "軽く、速く、数を投げるためだけに研がれている。",
                StylizedDaggers + "Dagger1_3_5.prefab", 0.35f, 0.05f, 1080f, flip: true);
            yield return Thrown("Weapon_HandAxe", "手斧", WeaponRank.A, 38f, "回転しながら飛び、重さのまま食い込む。",
                LowPolyWeapons + "Axe1H_Medium.prefab", 0.55f, 0.08f, 900f);
            yield return Thrown("Weapon_ThunderJavelin", "雷鳴の投槍", WeaponRank.S, 58f, "投げ放つと同時に、空気が裂けて鳴る。",
                LowPolyWeapons + "Spear1H_Epic.prefab", 1.4f, 0.6f, 0f);

            // 17 治癒持続（片手杖）。強さは毎秒の回復量。
            yield return new WeaponSpec
            {
                Id = "Weapon_WorldTreeBranchStaff", Name = "世界樹の枝杖", Rank = WeaponRank.B, Strength = 30f,
                Description = "落ちた場所に根を張り、周囲を緑の加護で満たす。",
                TypePath = HealFieldTypePath, ModelPath = LowPolyWeapons + "Wand_Medium.prefab", Length = WandLength, Grip = WandGrip, StaffTip = true,
                Blade = new Color32(120, 160, 80, 255), Hilt = new Color32(100, 70, 40, 255),
            };

            // 30 召喚。強さはおとりの体力の元（× 武器種の係数）。持つ見た目と置物は仮。
            // ユニークの固定エンチャント（数 ×2・持続時間 ×1）は仮。「軍勢が」に合わせて 3 体呼ぶ。
            SummonModels summons = summonModels;
            // 見た目はどれも Books Essentials の閉じた本。ランクが上がるほど角金具や宝石の付いた装丁にする（木偶は飾りの無い茶、石像は青、古代兵は緑、英霊は赤）。
            yield return Summoner("Weapon_WoodenDollWhistle", "木偶の呼び笛", WeaponRank.C, 22f, "吹くと、木でできた人形が地面から立ち上がる。",
                "Book.prefab", summons.WoodenDoll);
            yield return Summoner("Weapon_StoneStatueTalisman", "石像の護符", WeaponRank.A, 41f, "指定した場所に石の守り手が現れ、敵を引きつける。",
                "Book3_2.prefab", summons.StoneStatue);
            yield return Summoner("Weapon_AncientSoldierHorn", "古代兵の角笛", WeaponRank.S, 60f, "音に応え、朽ちぬ鎧をまとった兵が地から起き上がる。",
                "Book4_3.prefab", summons.AncientSoldier);
            yield return Summoner("Weapon_HeroicSpiritHorn", "英霊の号笛", WeaponRank.Unique, 60f, "呼び声に応え、いにしえの軍勢が列をなして現れる。",
                "Book1_4.prefab", summons.HeroicSpirit,
                EnchantmentKind.ProjectileCount, EnchantmentKind.ProjectileCount, EnchantmentKind.Duration);
        }

        /// <summary>召喚の武器（本）。model は Books Essentials のプレハブ名。真ん中を原点に置く（手のひらに本の真ん中が来る）。</summary>
        private static WeaponSpec Summoner(string id, string name, WeaponRank rank, float strength, string description, string model,
            GameObject summonModel, params EnchantmentKind[] fixedEnchantments)
        {
            return new WeaponSpec
            {
                Id = id, Name = name, Rank = rank, Strength = strength, Description = description,
                TypePath = SummonTypePath, ModelPath = BooksPrefabs + model, Length = SummonerLength, CenterPivot = true,
                Blade = new Color32(150, 120, 80, 255), Hilt = new Color32(90, 70, 50, 255),
                SummonModel = summonModel, FixedEnchantments = fixedEnchantments,
            };
        }

        private static WeaponSpec Thrown(string id, string name, WeaponRank rank, float strength, string description, string modelPath,
            float length, float grip, float spinRate, bool flip = false)
        {
            return new WeaponSpec
            {
                Id = id, Name = name, Rank = rank, Strength = strength, Description = description,
                TypePath = ThrowTypePath, ModelPath = modelPath, Length = length, Grip = grip, Flip = flip, ThrownSpinRate = spinRate,
                Blade = new Color32(190, 196, 204, 255), Hilt = new Color32(90, 60, 40, 255),
            };
        }

        private static WeaponSpec Staff(string id, string name, WeaponRank rank, float strength, string description, string typePath,
            string modelPath, params EnchantmentKind[] fixedEnchantments)
        {
            return new WeaponSpec
            {
                Id = id, Name = name, Rank = rank, Strength = strength, Description = description,
                TypePath = typePath, ModelPath = modelPath, Length = StaffLength, Grip = StaffGrip, StaffTip = true,
                Blade = new Color32(120, 90, 60, 255), Hilt = new Color32(80, 55, 35, 255),
                FixedEnchantments = fixedEnchantments,
            };
        }

        /// <summary>お守り・盾。真ん中を原点に置く（手のひらに真ん中が来る）。</summary>
        private static WeaponSpec Gear(string id, string name, WeaponRank rank, float strength, string description, string typePath,
            string modelPath, float length, params EnchantmentKind[] fixedEnchantments)
        {
            return new WeaponSpec
            {
                Id = id, Name = name, Rank = rank, Strength = strength, Description = description,
                TypePath = typePath, ModelPath = modelPath, Length = length, CenterPivot = true, IconBackside = typePath == ShieldTypePath,
                Blade = new Color32(150, 120, 80, 255), Hilt = new Color32(90, 70, 50, 255),
                FixedEnchantments = fixedEnchantments,
            };
        }

        private static WeaponSpec Bow(string id, string name, WeaponRank rank, float strength, string description, string typePath,
            string model, float length = BowLength)
        {
            return new WeaponSpec
            {
                Id = id, Name = name, Rank = rank, Strength = strength, Description = description,
                TypePath = typePath, ModelPath = LowPolyWeapons + model, Length = length, CenterPivot = true,
                Blade = new Color32(140, 100, 60, 255), Hilt = new Color32(90, 60, 40, 255),
            };
        }

        // ---- 共通のデータ ----------------------------------------------

        private static WeaponRankTable WriteRankTable(Material aura)
        {
            // 前は Items/Weapons に置いていた。GUID を保ったまま Resources へ移す。
            Gen.EnsureFolder(RankTablePath.Substring(0, RankTablePath.LastIndexOf('/')));
            if (AssetDatabase.LoadAssetAtPath<WeaponRankTable>(OldRankTablePath) != null
                && AssetDatabase.LoadAssetAtPath<WeaponRankTable>(RankTablePath) == null)
            {
                string error = AssetDatabase.MoveAsset(OldRankTablePath, RankTablePath);
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"ランク表を移せなかった: {error}");
            }

            var table = Gen.LoadOrCreate<WeaponRankTable>(RankTablePath);
            // Notion の色設定（ユニーク=orange、S=yellow、A=purple、B=blue、C=green、D=brown、E=gray）を仮採用。
            var entries = new (WeaponRank rank, string hex)[]
            {
                (WeaponRank.Unique, "#D9730D"),
                (WeaponRank.S, "#CB912F"),
                (WeaponRank.A, "#9065B0"),
                (WeaponRank.B, "#337EA9"),
                (WeaponRank.C, "#448361"),
                (WeaponRank.D, "#9F6B53"),
                (WeaponRank.E, "#787774"),
            };

            var serialized = new SerializedObject(table);
            SerializedProperty list = serialized.FindProperty("entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                ColorUtility.TryParseHtmlString(entries[i].hex, out Color color);
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("rank").enumValueIndex = RankEnumIndex(entries[i].rank);
                entry.FindPropertyRelative("color").colorValue = color;

                // 光と落ちた音はユニークだけ。光の色はランクの色を少し明るくする。
                bool unique = entries[i].rank == WeaponRank.Unique;
                entry.FindPropertyRelative("auraParticles").objectReferenceValue = unique ? aura : null;
                entry.FindPropertyRelative("auraColor").colorValue = unique ? Color.Lerp(color, new Color(1f, 0.75f, 0.3f), 0.5f) : Color.clear;
                entry.FindPropertyRelative("auraLightIntensity").floatValue = unique ? 2f : 0f;
                entry.FindPropertyRelative("dropSound").objectReferenceValue = unique ? LoadSound(UniqueDropSoundPath) : null;
                entry.FindPropertyRelative("dropSoundVolume").floatValue = unique ? 0.9f : 0f;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return table;
        }

        private static int RankEnumIndex(WeaponRank rank) => Array.IndexOf(Enum.GetValues(typeof(WeaponRank)), rank);

        private static EnchantmentRollSettings WriteRollSettings()
        {
            var settings = Gen.LoadOrCreate<EnchantmentRollSettings>(RollSettingsPath);
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("continueChance").floatValue = 0.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }

        private static Dictionary<EnchantmentKind, EnchantmentDefinition> WriteEnchantments()
        {
            // 効果量は仮。割合は 0.15 で +15%。効果を実装しているのは近接の武器種に付く 12 種だけ
            // （片手近距離の 9 種と、持続時間＝ダッシュの時間・数＝叩きつけの数・多重＝追撃とダッシュの回数）。
            var specs = new (EnchantmentKind kind, string name, float amount, float secondary, string description)[]
            {
                (EnchantmentKind.DamageUp, "ダメージ増加", 0.15f, 0f, "与えるダメージが 15% 上がる。"),
                (EnchantmentKind.CritChance, "クリティカル率", 0.05f, 0f, "クリティカルの出る確率が 5% 上がる。"),
                (EnchantmentKind.DropUp, "ドロップ増加", 0.10f, 0f, "倒した敵が武器を落とす確率が 10% 上がる。"),
                (EnchantmentKind.RapidFire, "速射", 0.10f, 0f, "攻撃の速さが 10% 上がる。"),
                (EnchantmentKind.ProjectileCount, "数", 1f, 0f, "放つ矢や叩きつけ・連ねる列・炎の筋が 1 つ増える。矢は照準の右左へ交互に開き、叩きつけと列と炎は前を中心に扇状に、矢の雨は狙った所の付近に降る。雷は飛び移る回数が増える。"),
                (EnchantmentKind.Size, "サイズ", 0.15f, 0f, "攻撃の届く範囲（矢の雨の範囲と炎の太さも）と、振りや爆発の大きさが 15% 広がる。"),
                (EnchantmentKind.Duration, "持続時間", 0.20f, 0f, "矢の雨やダッシュ、炎を吐ける時間が 20% 延び、地面から連ねる列が 20% 長くなる。"),
                (EnchantmentKind.Pierce, "貫通", 1f, 0f, "矢が敵を 1 体多く貫く。"),
                (EnchantmentKind.Multishot, "多重", 1f, 0f, "少し遅れてもう一度放つ一斉射（矢の雨は同じ所にもう一度、雷と地面から連ねる列も）や、叩きつけの追撃（叩きつけごとに、本撃と同じダメージ）、ダッシュ突きの走る回数が 1 つ増える。"),
                (EnchantmentKind.HealUp, "回復量増加", 0.20f, 0f, "回復する量が 20% 増える。"),
                (EnchantmentKind.Homing, "ホーミング", 1f, 0f, "矢や炎が前にいる敵を追って曲がる。"),
                (EnchantmentKind.ChargeTimeDown, "チャージ時間減少", 0.15f, 0f, "溜めにかかる時間が 15% 縮む。"),
                (EnchantmentKind.Stun, "スタン", 0.25f, 0f, "敵をスタン・気絶させやすくなる（一撃の重さ 25% 増しで判定）。"),
                (EnchantmentKind.ProjectileSpeed, "弾速", 0.20f, 0f, "矢が 20% 速く飛ぶ。矢の雨は 20% 早く降り始め、炎は 20% 遠くまで届く。"),
                (EnchantmentKind.Knockback, "ノックバック", 2f, 0f, "当てた敵を押し出す勢いが増す。"),
                (EnchantmentKind.Explosion, "爆発", 0.40f, 2.5f, "当てた所で爆発し、周りの敵にダメージの 40% を与える。"),
                (EnchantmentKind.ComboBonus, "コンボボーナス", 0.10f, 0f, "当てるたびにダメージが 10% ずつ上がり、周をまたいでも続く。空振りか手を止めると途切れる。"),
                (EnchantmentKind.MoveSpeed, "移動速度", 0.10f, 0f, "歩く速さが 10% 上がる。"),
                (EnchantmentKind.Exp, "経験値", 0.10f, 0f, "得られる経験値が 10% 増える。"),
                (EnchantmentKind.MaxHp, "体力増加", 0.10f, 0f, "最大 HP が 10% 増える。"),
                (EnchantmentKind.Defense, "防御力", 0.10f, 0f, "受けるダメージが 10% 減る。"),
                (EnchantmentKind.Regen, "自然回復", 1f, 0f, "HP が毎秒 1 ずつ回復する。"),
            };

            var result = new Dictionary<EnchantmentKind, EnchantmentDefinition>();
            foreach (var spec in specs)
            {
                var definition = Gen.LoadOrCreate<EnchantmentDefinition>($"{EnchantmentsFolder}/Enchant_{spec.kind}.asset");
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("kind").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(EnchantmentKind)), spec.kind);
                serialized.FindProperty("displayName").stringValue = spec.name;
                serialized.FindProperty("description").stringValue = spec.description;
                serialized.FindProperty("amount").floatValue = spec.amount;
                serialized.FindProperty("secondaryAmount").floatValue = spec.secondary;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                result[spec.kind] = definition;
            }

            return result;
        }

        private static WeaponTypeDefinition WriteOneHandedType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(OneHandedTypePath);
            var serialized = new SerializedObject(type);
            serialized.FindProperty("id").stringValue = "01";
            serialized.FindProperty("displayName").stringValue = "片手近距離";
            serialized.FindProperty("animatorWeaponType").intValue = 1; // CharacterAnimatorBuilder.Weapon.OneHanded
            serialized.FindProperty("canUseShield").boolValue = true;

            var allowed = new[]
            {
                EnchantmentKind.DamageUp, EnchantmentKind.CritChance, EnchantmentKind.DropUp, EnchantmentKind.Size,
                EnchantmentKind.RapidFire, EnchantmentKind.Stun, EnchantmentKind.Knockback, EnchantmentKind.Explosion,
                EnchantmentKind.ComboBonus,
            };
            SerializedProperty list = serialized.FindProperty("allowedEnchantments");
            list.arraySize = allowed.Length;
            for (int i = 0; i < allowed.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = enchantments[allowed[i]];

            serialized.FindProperty("enchantmentRoll").objectReferenceValue = roll;
            serialized.FindProperty("characterAttackWeight").floatValue = 1f;
            serialized.FindProperty("baseCritChance").floatValue = 0.05f;
            serialized.FindProperty("baseCritMultiplier").floatValue = 1.5f;

            // 素早い 3 振りと、溜めの長い重い 4 段目。値は仮。
            // 判定はモーションの刃が正面を通る瞬間に合わせる（MeleeAttack_OneHanded は 0.32 秒、2 段目の振り上げは 0.28 秒、
            // 4 段目は CharacterAnimatorBuilder で 0.5 秒）。
            // 斬撃（Stone slash は水平の三日月で、左から正面を回って右へ振る。半径は 0.6 倍で約 0.8 m）は、
            // 刃先の通り道に三日月の頂点が来る位置に出し、振りの向きに Z で傾ける:
            // 1・3 段目は右上から左下へ振り下ろして腰の前へ、2 段目は右下から左上へ振り上げて顔の前（y≈1.75, z≈1.2）を通る、
            // 4 段目は真上からの縦振り。
            var low = new Vector3(0f, 1.1f, 0.3f);
            WriteComboSteps(serialized, new[]
            {
                new ComboStepSpec(1f, 0.45f, 0.32f, new Vector3(1.6f, 1.2f, 1.4f), new Vector3(0f, 1f, 1.0f), 0.5f, low, new Vector3(0f, 0f, -125f)),
                new ComboStepSpec(1f, 0.45f, 0.28f, new Vector3(1.6f, 1.2f, 1.4f), new Vector3(0f, 1f, 1.0f), 0.5f,
                    new Vector3(-0.2f, 1.75f, 0.45f), new Vector3(0f, 0f, 157f)),
                new ComboStepSpec(1f, 0.50f, 0.32f, new Vector3(1.6f, 1.2f, 1.4f), new Vector3(0f, 1f, 1.0f), 0.5f, low, new Vector3(0f, 0f, -125f)),
                new ComboStepSpec(2f, 0.80f, 0.50f, new Vector3(2.0f, 1.2f, 1.8f), new Vector3(0f, 1f, 1.2f), 3f, low, new Vector3(0f, 0f, -90f)),
            });

            // 4 段目（締めの重い振り）だけ振りと命中の音を替える。
            SetStepSounds(serialized, 3, SwordFinisherSoundPath, SwordFinisherSoundPath);

            serialized.FindProperty("comboChainGrace").floatValue = 0.25f;
            serialized.FindProperty("comboCooldown").floatValue = 0.3f;
            serialized.FindProperty("swingEffect").objectReferenceValue = LoadEffect(SlashEffectPath);
            serialized.FindProperty("swingEffectScale").floatValue = 0.6f;
            serialized.FindProperty("hitEffect").objectReferenceValue = LoadEffect(SwordHitEffectPath);
            serialized.FindProperty("hitEffectScale").floatValue = 0.6f;
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(SwordSwingSoundPath);
            serialized.FindProperty("hitSound").objectReferenceValue = LoadSound(SwordHitSoundPath);
            serialized.FindProperty("soundVolume").floatValue = 0.8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>素手の武器種。右・左のパンチの 2 段。エンチャントは付かない。振りのエフェクトは出さず、命中だけ。</summary>
        private static WeaponTypeDefinition WriteUnarmedType()
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(UnarmedTypePath);
            var serialized = new SerializedObject(type);
            serialized.FindProperty("id").stringValue = "00";
            serialized.FindProperty("displayName").stringValue = "素手";
            serialized.FindProperty("animatorWeaponType").intValue = 0; // CharacterAnimatorBuilder.Weapon.Unarmed
            serialized.FindProperty("canUseShield").boolValue = false;
            serialized.FindProperty("allowedEnchantments").arraySize = 0;
            serialized.FindProperty("enchantmentRoll").objectReferenceValue = null;
            serialized.FindProperty("characterAttackWeight").floatValue = 1f;
            serialized.FindProperty("baseCritChance").floatValue = 0.05f;
            serialized.FindProperty("baseCritMultiplier").floatValue = 1.5f;

            // 判定は腕が伸び切る瞬間（CharacterAnimatorBuilder でパンチを 1.4 倍にして 0.3 秒）。
            WriteComboSteps(serialized, new[]
            {
                new ComboStepSpec(1f, 0.45f, 0.30f, new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0f, 1.1f, 0.8f), 0.3f, Vector3.zero, Vector3.zero),
                new ComboStepSpec(1f, 0.45f, 0.30f, new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0f, 1.1f, 0.8f), 0.3f, Vector3.zero, Vector3.zero),
            });

            serialized.FindProperty("comboChainGrace").floatValue = 0.25f;
            serialized.FindProperty("comboCooldown").floatValue = 0.3f;
            serialized.FindProperty("swingEffect").objectReferenceValue = null;
            serialized.FindProperty("hitEffect").objectReferenceValue = LoadEffect(PunchHitEffectPath);
            serialized.FindProperty("hitEffectScale").floatValue = 0.12f; // 素材は 14 m ほどに広がる
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(PunchSwingSoundPath);
            serialized.FindProperty("hitSound").objectReferenceValue = LoadSound(PunchHitSoundPath);
            serialized.FindProperty("soundVolume").floatValue = 0.6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        // ---- 武器種 02 / 04 / 05 ----------------------------------------------

        /// <summary>武器種の見出しと、付けられるエンチャント・ダメージの基礎を入れる。</summary>
        private static SerializedObject BeginType(WeaponTypeDefinition type, string id, string displayName, int animatorWeaponType,
            bool canUseShield, EnchantmentKind[] allowed, Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll)
        {
            var serialized = new SerializedObject(type);
            serialized.FindProperty("id").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("animatorWeaponType").intValue = animatorWeaponType;
            serialized.FindProperty("canUseShield").boolValue = canUseShield;

            SerializedProperty list = serialized.FindProperty("allowedEnchantments");
            list.arraySize = allowed.Length;
            for (int i = 0; i < allowed.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = enchantments[allowed[i]];

            serialized.FindProperty("enchantmentRoll").objectReferenceValue = roll;
            serialized.FindProperty("characterAttackWeight").floatValue = 1f;
            serialized.FindProperty("baseCritChance").floatValue = 0.05f;
            serialized.FindProperty("baseCritMultiplier").floatValue = 1.5f;
            return serialized;
        }

        /// <summary>
        /// お守り（26）。振らず、ホットバーに入れておくだけで効く（PlayerGear）。選んでいる間は素手で殴り、手には持たせる。
        /// </summary>
        private static WeaponTypeDefinition WriteCharmType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(CharmTypePath);
            SerializedObject serialized = BeginType(type, "26", "お守り", 0, false, new[]
            {
                EnchantmentKind.MoveSpeed, EnchantmentKind.CritChance, EnchantmentKind.DropUp, EnchantmentKind.ProjectileCount,
                EnchantmentKind.Multishot, EnchantmentKind.Exp, EnchantmentKind.MaxHp, EnchantmentKind.Regen,
            }, enchantments, roll); // 0 = CharacterAnimatorBuilder.Weapon.Unarmed
            WritePassiveGear(serialized, PassiveGear.Charm, false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>
        /// 盾（27）。ホットバーに入れて、片手武器（canUseShield）を持っている間だけ効く。強さが防御力で、
        /// 受けるダメージ × 100 / (100 + 防御力)。効いている間は左手に持たせる（HeldShieldView）。
        /// </summary>
        private static WeaponTypeDefinition WriteShieldType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(ShieldTypePath);
            SerializedObject serialized = BeginType(type, "27", "盾", 0, false, new[]
            {
                EnchantmentKind.CritChance, EnchantmentKind.DropUp, EnchantmentKind.ProjectileCount, EnchantmentKind.Multishot,
                EnchantmentKind.Exp, EnchantmentKind.MoveSpeed, EnchantmentKind.MaxHp, EnchantmentKind.Defense,
                EnchantmentKind.Regen,
            }, enchantments, roll);
            WritePassiveGear(serialized, PassiveGear.Shield, true);
            // 左の前腕の外側に、前腕に沿って立て、面を体の外へ向ける（片手剣の構えで合わせた）。Play 中に Scene ビューで動かすと書き戻る。
            SeedHeldGrip(serialized, new Vector3(-0.12f, -0.052f, -0.086f), new Vector3(16f, 181f, 258f));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>振らない装備の武器種にする。攻撃欄（コンボ・遠距離）は空にする。</summary>
        private static void WritePassiveGear(SerializedObject serialized, PassiveGear gear, bool leftHand)
        {
            serialized.FindProperty("passiveGear").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(PassiveGear)), gear);
            serialized.FindProperty("comboSteps").arraySize = 0;
            serialized.FindProperty("rangedKind").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(RangedAttackKind)), RangedAttackKind.None);
            serialized.FindProperty("heldInLeftHand").boolValue = leftHand;
        }

        /// <summary>手に持つ位置が未調整（両方ゼロ）なら、片手剣と同じ握りを仮に入れる。調整済みなら触らない。</summary>
        private static void SeedHeldGrip(SerializedObject serialized, Vector3 position, Vector3 euler)
        {
            SerializedProperty p = serialized.FindProperty("heldLocalPosition");
            SerializedProperty e = serialized.FindProperty("heldLocalEuler");
            if (p.vector3Value != Vector3.zero || e.vector3Value != Vector3.zero) return;

            p.vector3Value = position;
            e.vector3Value = euler;
        }

        /// <summary>
        /// ダッシュ突き（02）。1 段で、判定の瞬間から前へ走り、走っている間ずっと前方の箱で当てる。敵はすり抜け、壁で止まる。
        /// 持続時間のエンチャントで走る時間（＝距離）が延び、多重のエンチャントで走る回数が増える。コンボボーナスは付かない。値は仮。
        /// </summary>
        private static WeaponTypeDefinition WriteDashThrustType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(DashThrustTypePath);
            SerializedObject serialized = BeginType(type, "02", "ダッシュ突き", 5, true, new[]
            {
                EnchantmentKind.DamageUp, EnchantmentKind.CritChance, EnchantmentKind.DropUp, EnchantmentKind.RapidFire,
                EnchantmentKind.Duration, EnchantmentKind.Stun, EnchantmentKind.Knockback, EnchantmentKind.Multishot,
            }, enchantments, roll); // 5 = CharacterAnimatorBuilder.Weapon.DashThrust

            // 0.2 秒で突き出してから 0.18 秒で 5 m 走る（約 28 m/s）。走り終えて 0.37 秒で構えに戻る。
            WriteComboSteps(serialized, new[]
            {
                new ComboStepSpec(1f, 0.75f, 0.2f, new Vector3(1.2f, 1.4f, 1.4f), new Vector3(0f, 1f, 0.8f), 2f,
                    new Vector3(0f, 1.3f, 0.6f), new Vector3(0f, 0f, 0f))
                {
                    Motion = MeleeStepMotion.Lunge, LungeDistance = 5f, LungeDuration = 0.18f,
                },
            });

            serialized.FindProperty("comboChainGrace").floatValue = 0f;
            serialized.FindProperty("comboCooldown").floatValue = 0.3f;
            serialized.FindProperty("lungePassesThroughEnemies").boolValue = true;
            serialized.FindProperty("invulnerableDuringLunge").boolValue = true;
            SeedHeldGrip(serialized, new Vector3(-0.1f, -0.1f, -0.05f), Vector3.zero);
            // 振り始めに体の胸の前へ付けて溜め、走り出す瞬間（hitTime）に斬撃を前へ放つ（MeleeAttacker が再生の速さを合わせる）。
            serialized.FindProperty("swingEffect").objectReferenceValue = LoadEffect(ChargeSlashEffectPath);
            serialized.FindProperty("swingEffectScale").floatValue = 0.8f;
            serialized.FindProperty("swingEffectLeadTime").floatValue = ChargeSlashReleaseTime;
            serialized.FindProperty("hitEffect").objectReferenceValue = LoadEffect(SwordHitEffectPath);
            serialized.FindProperty("hitEffectScale").floatValue = 0.6f;
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(DashThrustSoundPath);
            serialized.FindProperty("hitSound").objectReferenceValue = LoadSound(SwordHitSoundPath);
            serialized.FindProperty("soundVolume").floatValue = 0.8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>
        /// 両手近距離（04）。大ぶりの横薙ぎ 3 回と、重い振り下ろしの 4 段目。4 段目は箱が大きく、ノックバックとスタンが強い。
        /// 音は片手剣の振り・命中を流用し、4 段目の命中だけ替える。値は仮。
        /// </summary>
        private static WeaponTypeDefinition WriteTwoHandedType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(TwoHandedTypePath);
            SerializedObject serialized = BeginType(type, "04", "両手近距離", 2, false, new[]
            {
                EnchantmentKind.DamageUp, EnchantmentKind.CritChance, EnchantmentKind.DropUp, EnchantmentKind.Size,
                EnchantmentKind.RapidFire, EnchantmentKind.Stun, EnchantmentKind.Knockback, EnchantmentKind.Explosion,
                EnchantmentKind.ComboBonus,
            }, enchantments, roll); // 2 = CharacterAnimatorBuilder.Weapon.TwoHanded

            // 判定は CharacterAnimatorBuilder の再生速度に合わせる（横薙ぎ 0.42 秒、締めの振り下ろし 0.67 秒）。
            var sweep = new Vector3(2.6f, 1.4f, 2.0f);
            var sweepCenter = new Vector3(0f, 1f, 1.2f);
            var waist = new Vector3(0f, 1.1f, 0.5f);
            WriteComboSteps(serialized, new[]
            {
                new ComboStepSpec(1f, 0.70f, 0.42f, sweep, sweepCenter, 1.5f, waist, new Vector3(0f, 0f, -160f)),
                new ComboStepSpec(1f, 0.70f, 0.42f, sweep, sweepCenter, 1.5f, waist, new Vector3(0f, 0f, 20f)),
                new ComboStepSpec(1f, 0.75f, 0.42f, sweep, sweepCenter, 1.5f, waist, new Vector3(0f, 0f, -160f)),
                new ComboStepSpec(2.5f, 1.10f, 0.67f, new Vector3(2.4f, 1.6f, 2.8f), new Vector3(0f, 1f, 1.5f), 5f, waist,
                    new Vector3(0f, 0f, -90f)) { ReactionBonus = 1f },
            });
            SetStepSounds(serialized, 3, SwordFinisherSoundPath, SwordFinisherSoundPath);

            serialized.FindProperty("comboChainGrace").floatValue = 0.35f;
            serialized.FindProperty("comboCooldown").floatValue = 0.5f;
            SeedHeldGrip(serialized, new Vector3(-0.1f, -0.2f, -0.05f), Vector3.zero);
            serialized.FindProperty("swingEffect").objectReferenceValue = LoadEffect(SlashEffectPath);
            serialized.FindProperty("swingEffectScale").floatValue = 0.9f;
            serialized.FindProperty("hitEffect").objectReferenceValue = LoadEffect(SwordHitEffectPath);
            serialized.FindProperty("hitEffectScale").floatValue = 0.8f;
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(GreatswordSwingSoundPath);
            serialized.FindProperty("hitSound").objectReferenceValue = LoadSound(SwordHitSoundPath);
            serialized.FindProperty("soundVolume").floatValue = 0.8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>
        /// 叩きつけ（05）。1 段で、前方の着弾点を中心とした円に当てる。
        /// 数のエンチャントで叩きつけが増えて持ち主を中心に 30° ずつ扇状に並び、多重のエンチャントで叩きつけごとにその向きへずらした追撃が遅れて落ちる。値は仮。
        /// </summary>
        private static WeaponTypeDefinition WriteHammerType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(HammerTypePath);
            SerializedObject serialized = BeginType(type, "05", "叩きつけ", 6, false, new[]
            {
                EnchantmentKind.DamageUp, EnchantmentKind.CritChance, EnchantmentKind.DropUp, EnchantmentKind.ProjectileCount,
                EnchantmentKind.Size, EnchantmentKind.Multishot, EnchantmentKind.RapidFire, EnchantmentKind.Stun,
                EnchantmentKind.Knockback, EnchantmentKind.Explosion, EnchantmentKind.ComboBonus,
            }, enchantments, roll); // 6 = CharacterAnimatorBuilder.Weapon.Hammer

            // 振り下ろしが地面に着く 0.76 秒（CharacterAnimatorBuilder で 1.1 倍）に、1.5 m 前の地面を中心に半径 1.8 m。
            WriteComboSteps(serialized, new[]
            {
                new ComboStepSpec(1f, 1.0f, 0.76f, Vector3.zero, new Vector3(0f, 0f, 1.5f), 4f,
                    new Vector3(0f, 1.2f, 0.8f), new Vector3(0f, 0f, -90f))
                {
                    Motion = MeleeStepMotion.Slam, SlamRadius = 1.8f, SlamHeight = 1.5f, ReactionBonus = 0.5f,
                },
            });

            serialized.FindProperty("comboChainGrace").floatValue = 0f;
            serialized.FindProperty("comboCooldown").floatValue = 0.4f;
            serialized.FindProperty("slamSpreadAngle").floatValue = 30f;
            serialized.FindProperty("followUpSpacing").floatValue = 1.5f;
            serialized.FindProperty("followUpInterval").floatValue = 0.18f;
            serialized.FindProperty("followUpEffectScale").floatValue = 0.7f;
            serialized.FindProperty("slamShake").floatValue = 0.35f;
            serialized.FindProperty("followUpShakeRatio").floatValue = 0.4f;
            SeedHeldGrip(serialized, new Vector3(-0.1f, -0.21f, -0.05f), Vector3.zero);
            // 振りの斬撃は出さない（槌なので）。着弾点に土煙・石の破片・火花・閃光を重ね、当たった敵にも閃光。
            serialized.FindProperty("swingEffect").objectReferenceValue = null;
            WriteEffectLayers(serialized.FindProperty("slamEffects"), new[]
            {
                new EffectLayer(LoadEffect(SlamDustEffectPath), 0.5f),
                new EffectLayer(LoadEffect(DustPuffEffectPath), 0.3f),
                new EffectLayer(LoadEffect(StonesEffectPath), 1.2f),
                new EffectLayer(LoadEffect(SparksEffectPath), 0.9f, new Vector3(0f, 0.2f, 0f)),
                new EffectLayer(LoadEffect(SlamFlashEffectPath), 0.1f, new Vector3(0f, 0.3f, 0f)),
            });
            serialized.FindProperty("hitEffect").objectReferenceValue = LoadEffect(PunchHitEffectPath);
            serialized.FindProperty("hitEffectScale").floatValue = 0.22f;
            // 叩きつけの音は外れても着弾の瞬間に鳴る（MeleeAttacker が命中の音として鳴らす）。振りの音も同じ瞬間に重ねて厚くする。
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(HammerSlamSoundPath);
            serialized.FindProperty("hitSound").objectReferenceValue = LoadSound(HammerSlamSoundPath);
            serialized.FindProperty("soundVolume").floatValue = 0.9f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>
        /// 弓（09）。照準の先へ矢をまっすぐ放つ。数で扇状に増え、多重で遅れてもう一斉射、貫通・ホーミング・爆発が付く。値は仮。
        /// 撃つ間隔 0.8 秒なので、1 発は 強さ × 0.8。
        /// </summary>
        private static WeaponTypeDefinition WriteBowType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll, GameObject arrow, Material ring)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(BowTypePath);
            SerializedObject serialized = BeginType(type, "09", "弓", 3, false, new[]
            {
                EnchantmentKind.CritChance, EnchantmentKind.Stun, EnchantmentKind.DamageUp, EnchantmentKind.DropUp,
                EnchantmentKind.Knockback, EnchantmentKind.Homing, EnchantmentKind.Multishot, EnchantmentKind.ProjectileSpeed,
                EnchantmentKind.ProjectileCount, EnchantmentKind.Explosion, EnchantmentKind.Pierce, EnchantmentKind.RapidFire,
            }, enchantments, roll); // 3 = CharacterAnimatorBuilder.Weapon.Bow

            WriteRangedCommon(serialized, RangedAttackKind.Bow, 0.8f, arrow, ring);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>
        /// 持続弓（11）。照準の地面へ上から雨（見た目は Meteors AOE）を降らせ、半径 1.25 m に 0.5 秒ごと 3 秒間（6 刻み）ダメージ。値は仮。
        /// 撃つ間隔 1.2 秒なので、範囲にずっと居た 1 体が受ける合計は 強さ × 1.2。数で付近のランダムな所に雨が増え、多重で同じ所にもう一度降る。
        /// </summary>
        private static WeaponTypeDefinition WriteRainBowType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll, GameObject arrow, Material ring)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(RainBowTypePath);
            SerializedObject serialized = BeginType(type, "11", "持続弓", 3, false, new[]
            {
                EnchantmentKind.CritChance, EnchantmentKind.Size, EnchantmentKind.Stun, EnchantmentKind.DamageUp,
                EnchantmentKind.DropUp, EnchantmentKind.Knockback, EnchantmentKind.Multishot, EnchantmentKind.ProjectileSpeed,
                EnchantmentKind.Duration, EnchantmentKind.ProjectileCount, EnchantmentKind.RapidFire,
            }, enchantments, roll); // 3 = CharacterAnimatorBuilder.Weapon.Bow

            WriteRangedCommon(serialized, RangedAttackKind.Rain, 1.2f, arrow, ring);
            serialized.FindProperty("aimMaxDistance").floatValue = 25f;
            serialized.FindProperty("rainRadius").floatValue = 1.25f;
            serialized.FindProperty("rainHeight").floatValue = 2f;
            serialized.FindProperty("rainDuration").floatValue = 3f;
            serialized.FindProperty("rainTickInterval").floatValue = 0.5f;
            serialized.FindProperty("rainDelay").floatValue = 0.7f;
            serialized.FindProperty("rainScatterMin").floatValue = 1.25f;
            serialized.FindProperty("rainScatterMax").floatValue = 3f;
            serialized.FindProperty("rainRepeatInterval").floatValue = 1f;
            // 隕石が半径 4 m の円錐から降る素材を、雨の半径に合わせて水平に縮めて使う。
            serialized.FindProperty("rainEffect").objectReferenceValue = LoadEffect(MeteorsEffectPath);
            serialized.FindProperty("rainEffectRadius").floatValue = 4f;
            serialized.FindProperty("rainAimPitch").floatValue = 35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        // ---- 武器種 28（投擲） ----------------------------------------------

        /// <summary>
        /// 投擲（28）。右手に持った武器を、腕を振り切る瞬間（押して 0.22 秒、Kevin の右手の突きを 1.5 倍）に照準の先へまっすぐ投げる。
        /// 飛ぶのは武器の手に持つ見た目そのもので、投げてから次が投げられるまで手は空になる。数で扇状に増え、多重で遅れてもう一投、
        /// 貫通・ホーミングは弓と同じ。爆発は付かない。値は仮。撃つ間隔 0.7 秒なので、1 投は 強さ × 0.7。
        /// </summary>
        private static WeaponTypeDefinition WriteThrowType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll, Material ring)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(ThrowTypePath);
            SerializedObject serialized = BeginType(type, "28", "投擲", 7, false, new[]
            {
                EnchantmentKind.CritChance, EnchantmentKind.Stun, EnchantmentKind.DamageUp, EnchantmentKind.DropUp,
                EnchantmentKind.Knockback, EnchantmentKind.Homing, EnchantmentKind.Multishot, EnchantmentKind.ProjectileSpeed,
                EnchantmentKind.ProjectileCount, EnchantmentKind.Pierce, EnchantmentKind.RapidFire,
            }, enchantments, roll); // 7 = CharacterAnimatorBuilder.Weapon.Throw

            // 飛ぶ見た目は武器ごとの手に持つ見た目を使うので、武器種の矢は持たない。
            WriteRangedCommon(serialized, RangedAttackKind.Throw, 0.7f, null, ring);
            serialized.FindProperty("heldInLeftHand").boolValue = false;
            serialized.FindProperty("castDelay").floatValue = 0.22f;
            serialized.FindProperty("projectileSpeed").floatValue = 28f;
            serialized.FindProperty("projectileRange").floatValue = 35f;
            serialized.FindProperty("projectileRadius").floatValue = 0.2f;
            serialized.FindProperty("projectileKnockback").floatValue = 2.5f;
            // 右手の骨から見た握る所（杖と同じく、指は骨の −X へ伸び、拳を通る軸が +Y）。調整済みなら触らない。
            SeedHeldGrip(serialized, new Vector3(-0.09f, 0f, -0.02f), Vector3.zero);
            serialized.FindProperty("hitEffect").objectReferenceValue = LoadEffect(SwordHitEffectPath);
            serialized.FindProperty("hitEffectScale").floatValue = 0.4f;
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(ThrowSoundPath);
            serialized.FindProperty("stickSound").objectReferenceValue = LoadSound(ThrownStickSoundPath);
            serialized.FindProperty("drawSound").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        // ---- 武器種 17（治癒持続）・30（召喚） ----------------------------------------------

        /// <summary>
        /// 治癒持続（17、片手杖）。右手に杖を持ち、腕を振り切る瞬間（押して 0.22 秒）に狙った地面へ種を放物線で投げ、
        /// 落ちた所に半径 2 m の治癒の場を 5 秒張る（0.5 秒ごと 10 刻み、最初の刻みは張った瞬間）。値は仮。
        /// 強さは毎秒の回復量で、撃つ間隔 4 秒なので、場にずっと居れば 1 つの場から 強さ × 4 回復する。
        /// 数で付近に場が増え、多重で 1.5 秒遅れて同じ所にもう一度張り、サイズで広がる。片手杖なので盾が効く。
        /// </summary>
        private static WeaponTypeDefinition WriteHealFieldType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll, Material ring, GameObject seed)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(HealFieldTypePath);
            SerializedObject serialized = BeginType(type, "17", "治癒持続", 7, true, new[]
            {
                EnchantmentKind.HealUp, EnchantmentKind.Multishot, EnchantmentKind.Duration, EnchantmentKind.Size,
                EnchantmentKind.ProjectileCount, EnchantmentKind.RapidFire,
            }, enchantments, roll); // 7 = CharacterAnimatorBuilder.Weapon.Throw（腕を前へ振る）

            WriteSupportCommon(serialized, RangedAttackKind.HealField, 4f, ring);
            serialized.FindProperty("aimMaxDistance").floatValue = 15f;
            serialized.FindProperty("healRadius").floatValue = 2f;
            serialized.FindProperty("healHeight").floatValue = 2f;
            serialized.FindProperty("healDuration").floatValue = 5f;
            serialized.FindProperty("healTickInterval").floatValue = 0.5f;
            serialized.FindProperty("healFlightTime").floatValue = 0.55f;
            serialized.FindProperty("healArcHeight").floatValue = 2f;
            serialized.FindProperty("healScatterMin").floatValue = 2f;
            serialized.FindProperty("healScatterMax").floatValue = 4f;
            serialized.FindProperty("healRepeatInterval").floatValue = 1.5f;
            serialized.FindProperty("healSeedPrefab").objectReferenceValue = seed;
            // Healing circle は半径 4 m の魔法陣（大きさ 8 の板）と、同じ半径から立ちのぼる光の粒。場の半径に合わせて水平に縮める。
            serialized.FindProperty("healEffect").objectReferenceValue = LoadEffect(HealingCircleEffectPath);
            serialized.FindProperty("healEffectRadius").floatValue = 4f;
            serialized.FindProperty("healRingColor").colorValue = new Color(0.45f, 1f, 0.5f, 0.85f);
            serialized.FindProperty("aimRingColor").colorValue = new Color(0.75f, 1f, 0.75f, 0.6f);
            serialized.FindProperty("healSound").objectReferenceValue = LoadSound(HealSoundPath);
            serialized.FindProperty("healSoundVolume").floatValue = 0.8f;
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(SeedThrowSoundPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>
        /// 召喚（30）。腕を振り切る瞬間（押して 0.22 秒）に、狙った地面へ置物を呼び出す。置物は攻撃せず、敵を引きつける的（DecoyTarget）として
        /// 12 秒立っている（おとりの体力 ＝ 強さ × 10。敵の攻撃はまだ無い）。数で狙った所から 1.2〜2.5 m のランダムな所に増え（互いに 1 m 以上離す）、持続時間で長く居る。
        /// 呼び直すと前の分は消える。撃つ間隔（呼び直しの待ち）8 秒。値は仮。手に持つ本は右手。
        /// 出るときの魔法陣（Magic circle）は自分では消えない素材なので、SummonedDecoy が少し見せてから薄くして消す。消えるときの煙は小さめ。
        /// </summary>
        private static WeaponTypeDefinition WriteSummonType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll, Material ring)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(SummonTypePath);
            SerializedObject serialized = BeginType(type, "30", "召喚", 7, false, new[]
            {
                EnchantmentKind.Duration, EnchantmentKind.ProjectileCount, EnchantmentKind.RapidFire,
            }, enchantments, roll); // 7 = CharacterAnimatorBuilder.Weapon.Throw（腕を前へ振って呼ぶ所を指す）

            WriteSupportCommon(serialized, RangedAttackKind.Summon, 8f, ring);
            serialized.FindProperty("aimMaxDistance").floatValue = 15f;
            serialized.FindProperty("summonDuration").floatValue = 12f;
            serialized.FindProperty("summonScatterMin").floatValue = 1.2f;
            serialized.FindProperty("summonScatterMax").floatValue = 2.5f;
            serialized.FindProperty("summonMinGap").floatValue = 1f;
            serialized.FindProperty("summonHealthPerStrength").floatValue = 10f;
            serialized.FindProperty("summonEffect").objectReferenceValue = LoadEffect(SummonCircleEffectPath);
            serialized.FindProperty("summonEffectScale").floatValue = 0.6f;
            serialized.FindProperty("dismissEffect").objectReferenceValue = LoadEffect(SmokePuffEffectPath);
            serialized.FindProperty("dismissEffectScale").floatValue = 0.4f;
            serialized.FindProperty("aimRingColor").colorValue = new Color(0.75f, 0.85f, 1f, 0.6f);
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(SummonSoundPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>
        /// 治癒持続・召喚に共通の値。遠距離の欄を使うが敵には当てないので、矢・命中・弓の音は外し、基礎攻撃力も足さない。
        /// 右手で持ち、投擲と同じく腕を振り切る瞬間に放つ。照準へは体の前を向ける。
        /// </summary>
        private static void WriteSupportCommon(SerializedObject serialized, RangedAttackKind kind, float fireInterval, Material ring)
        {
            WriteRangedCommon(serialized, kind, fireInterval, null, ring);
            serialized.FindProperty("characterAttackWeight").floatValue = 0f;
            serialized.FindProperty("baseCritChance").floatValue = 0f;
            serialized.FindProperty("heldInLeftHand").boolValue = false;
            serialized.FindProperty("castDelay").floatValue = 0.22f;
            // 右手の骨から見た握る所（投擲・杖と同じ）。調整済みなら触らない。
            SeedHeldGrip(serialized, new Vector3(-0.09f, 0f, -0.02f), Vector3.zero);
            serialized.FindProperty("hitEffect").objectReferenceValue = null;
            serialized.FindProperty("hitSound").objectReferenceValue = null;
            serialized.FindProperty("drawSound").objectReferenceValue = null;
            serialized.FindProperty("stickSound").objectReferenceValue = null;
            serialized.FindProperty("rainSound").objectReferenceValue = null;
            serialized.FindProperty("soundVolume").floatValue = 0.8f;
        }

        /// <summary>
        /// 治癒持続で投げる種。光る緑の小さな球に、薄い緑の尾を引かせる。当たり判定は持たない（LobbedSeed が見た目として運ぶ）。
        /// </summary>
        private static GameObject WriteHealSeedPrefab(Material ring)
        {
            var root = new GameObject("Projectile_HealSeed");
            Material glow = Gen.Material("Placeholder_HealSeed", new Color(0.55f, 1f, 0.45f, 1f));
            if (glow.HasProperty("_EmissionColor"))
            {
                glow.EnableKeyword("_EMISSION");
                glow.SetColor("_EmissionColor", new Color(0.4f, 1.2f, 0.35f, 1f));
                EditorUtility.SetDirty(glow);
            }

            Gen.Part(root, PrimitiveType.Sphere, glow, Vector3.zero, Vector3.one * 0.14f);
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = ring;
            trail.time = 0.25f;
            trail.widthMultiplier = 0.09f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.startColor = new Color(0.6f, 1f, 0.5f, 0.8f);
            trail.endColor = new Color(0.6f, 1f, 0.5f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HealSeedPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---- 召喚の置物 ----------------------------------------------

        // Generate が作って Weapons() に渡す。LoadAll では作らない（読むだけなので空のまま）。
        private static SummonModels summonModels;

        private struct SummonModels
        {
            public GameObject WoodenDoll;
            public GameObject StoneStatue;
            public GameObject AncientSoldier;
            public GameObject HeroicSpirit;
        }

        /// <summary>
        /// 召喚で呼び出す置物を作る。どれも足元が原点、+Z が前で、当たり判定も Animator も持たない。
        /// 木偶はプリミティブの木の人形。石像・古代兵・英霊は Blink の人型を Kevin の構えの姿勢で焼いた 1 つのメッシュを、灰の石・くすんだ青銅・半透明の青い光（URP の Unlit）で塗り分ける。
        /// 人型が無い（ThirdParty が無い）ときは木の人形の形で代える。
        /// </summary>
        private static SummonModels WriteSummonModels()
        {
            Gen.EnsureFolder(SummonsFolder);
            Mesh human = WriteSummonHumanMesh();

            Material stone = Gen.Material("Summon_Stone", new Color32(150, 147, 140, 255));
            SetSurface(stone, 0f, 0.15f);
            Material bronze = Gen.Material("Summon_Bronze", new Color32(150, 112, 62, 255));
            SetSurface(bronze, 0.85f, 0.45f);
            Material ghost = WriteGhostMaterial();

            return new SummonModels
            {
                WoodenDoll = SavePrefab(BuildWoodenDoll(), "Summon_WoodenDoll"),
                StoneStatue = SavePrefab(BuildHumanStatue("Summon_StoneStatue", human, stone), "Summon_StoneStatue"),
                AncientSoldier = SavePrefab(BuildHumanStatue("Summon_AncientSoldier", human, bronze), "Summon_AncientSoldier"),
                HeroicSpirit = SavePrefab(AddSpiritLight(BuildHumanStatue("Summon_HeroicSpirit", human, ghost)), "Summon_HeroicSpirit"),
            };
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{SummonsFolder}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetSurface(Material material, float metallic, float smoothness)
        {
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
        }

        /// <summary>英霊の半透明の青い光。照明を受けない URP の Unlit を、透明（アルファで重ねる）にして使う。</summary>
        private static Material WriteGhostMaterial()
        {
            string path = SummonsFolder + "/Summon_Ghost.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "Summon_Ghost" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null) material.shader = shader;

            var color = new Color(0.55f, 0.8f, 1f, 0.45f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            // Inspector で「Surface Type: Transparent / Blend: Alpha」を選んだときと同じ値とキーワード。
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>木の人形（訓練用の木偶）。台・柱・胴・腕木・頭をプリミティブで組む。高さ約 1.8 m。</summary>
        private static GameObject BuildWoodenDoll()
        {
            var root = new GameObject("Summon_WoodenDoll");
            Material wood = Gen.Material("Summon_Wood", new Color32(156, 108, 62, 255));
            Material dark = Gen.Material("Summon_WoodDark", new Color32(108, 72, 40, 255));
            Gen.Part(root, PrimitiveType.Cylinder, dark, new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 0.04f, 0.6f));
            Gen.Part(root, PrimitiveType.Cylinder, dark, new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 0.5f, 0.12f));
            Gen.Part(root, PrimitiveType.Cylinder, wood, new Vector3(0f, 1.15f, 0f), new Vector3(0.42f, 0.32f, 0.36f));
            Gen.Part(root, PrimitiveType.Cube, dark, new Vector3(0f, 1.32f, 0f), new Vector3(1.0f, 0.08f, 0.08f));
            Gen.Part(root, PrimitiveType.Sphere, wood, new Vector3(0f, 1.66f, 0f), new Vector3(0.3f, 0.32f, 0.3f));
            return root;
        }

        /// <summary>焼いた人型のメッシュに material を塗った置物。メッシュが無ければ木の人形の形を material で塗る。</summary>
        private static GameObject BuildHumanStatue(string name, Mesh mesh, Material material)
        {
            if (mesh == null)
            {
                GameObject doll = BuildWoodenDoll();
                doll.name = name;
                foreach (Renderer r in doll.GetComponentsInChildren<Renderer>()) r.sharedMaterial = material;
                return doll;
            }

            var root = new GameObject(name);
            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = body.AddComponent<MeshRenderer>();
            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            return root;
        }

        /// <summary>英霊に、胸の高さの青い明かりを足す。</summary>
        private static GameObject AddSpiritLight(GameObject root)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, 0.3f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.5f, 0.75f, 1f);
            light.range = 3f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return root;
        }

        /// <summary>
        /// Blink の人型を Kevin の片手武器の構え（CombatIdle1H01 の頭のフレーム）の姿勢にして、肌・服・髪などの皮付きメッシュを全部焼いて 1 つに合わせ、
        /// 足元が原点・背の高さが <see cref="SummonHumanHeight"/> のメッシュとして保存する。人型か姿勢が無ければ null（姿勢だけ無ければ元の姿勢で焼く）。
        /// </summary>
        private static Mesh WriteSummonHumanMesh()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(StylizedHumanPath);
            if (source == null)
            {
                Debug.LogWarning($"人型が無いので、置物は木の人形の形で代える: {StylizedHumanPath}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AnimationClip pose = LoadClip(SummonPoseClipPath);
                var animator = instance.GetComponentInChildren<Animator>();
                if (pose == null || animator == null || animator.avatar == null)
                {
                    Debug.LogWarning("置物の姿勢を付けられなかったので、元の姿勢のまま焼く。");
                    return BakeSkinnedMeshes(instance);
                }

                AnimationMode.StartAnimationMode();
                try
                {
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(animator.gameObject, pose, 0f);
                    AnimationMode.EndSampling();
                    return BakeSkinnedMeshes(instance);
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static AnimationClip LoadClip(string path)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal)) return clip;
            Debug.LogWarning($"置物の姿勢のモーションが無い: {path}");
            return null;
        }

        /// <summary>root の下の皮付きメッシュと普通のメッシュを、今の姿勢のまま root の座標系で 1 つに合わせて保存する。</summary>
        private static Mesh BakeSkinnedMeshes(GameObject root)
        {
            var parts = new List<CombineInstance>();
            var temporary = new List<Mesh>();
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!skinned.enabled || skinned.sharedMesh == null) continue;

                var baked = new Mesh();
                skinned.BakeMesh(baked, true);
                temporary.Add(baked);
                // BakeMesh(useScale) は大きさまで焼くので、位置と向きだけを掛ける。
                Matrix4x4 placement = toRoot * Matrix4x4.TRS(skinned.transform.position, skinned.transform.rotation, Vector3.one);
                for (int i = 0; i < baked.subMeshCount; i++) parts.Add(new CombineInstance { mesh = baked, subMeshIndex = i, transform = placement });
            }

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Matrix4x4 placement = toRoot * filter.transform.localToWorldMatrix;
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                    parts.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = i, transform = placement });
            }

            if (parts.Count == 0)
            {
                foreach (Mesh m in temporary) Object.DestroyImmediate(m);
                return null;
            }

            var combined = new Mesh { name = "Mesh_SummonHuman", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            combined.CombineMeshes(parts.ToArray(), true, true);
            foreach (Mesh m in temporary) Object.DestroyImmediate(m);

            // 足元を原点に、背の高さを揃える。
            combined.RecalculateBounds();
            Bounds bounds = combined.bounds;
            float scale = bounds.size.y > 1e-4f ? SummonHumanHeight / bounds.size.y : 1f;
            Vector3[] vertices = combined.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                vertices[i] = new Vector3((v.x - bounds.center.x) * scale, (v.y - bounds.min.y) * scale, (v.z - bounds.center.z) * scale);
            }

            combined.vertices = vertices;
            // 頂点色は塗り分けでは使わないので捨てる。
            combined.colors = null;
            combined.RecalculateBounds();

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(SummonHumanMeshPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(combined, existing);
                existing.name = "Mesh_SummonHuman";
                Object.DestroyImmediate(combined);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(combined, SummonHumanMeshPath);
            return combined;
        }

        // ---- 武器種 19 / 20 / 21（両手杖） ----------------------------------------------

        /// <summary>
        /// 杖（雷・連置・炎）に共通の値。弓と同じ遠距離の欄を使い、Animator は魔法（撃ち出しは SpellCast、炎は CastingLoop）。
        /// 右手で握り、照準へは体の前を向ける。矢は飛ばさないので矢の見た目と弓の音は外す。
        /// </summary>
        private static void WriteStaffCommon(SerializedObject serialized, RangedAttackKind kind, float fireInterval, Material ring)
        {
            WriteRangedCommon(serialized, kind, fireInterval, null, ring);
            serialized.FindProperty("heldInLeftHand").boolValue = false;
            // 撃ち出し（SpellCast の溜めの終わりから再生）で杖を突き出すのは押してから 0.1 秒あたり。
            serialized.FindProperty("castDelay").floatValue = 0.12f;
            // 右手の骨から見た杖の握る所（指は骨の −X へ伸び、拳を通る軸が +Y。手のひらの真ん中は −X に 0.08 m ほど）。調整済みなら触らない。
            SeedHeldGrip(serialized, new Vector3(-0.09f, 0f, -0.02f), Vector3.zero);
            serialized.FindProperty("drawSound").objectReferenceValue = null;
            serialized.FindProperty("stickSound").objectReferenceValue = null;
            serialized.FindProperty("rainSound").objectReferenceValue = null;
        }

        /// <summary>
        /// 電撃（19）。照準から 10° 以内の、いちばん照準に近い敵へ雷を放ち、6 m 以内の次の敵へ 3 回飛び移る（4 体まで）。値は仮。
        /// 撃つ間隔 0.9 秒なので、1 体あたり 強さ × 0.9。数 1 つで 2 回多く飛び移り、多重で 0.25 秒遅れてもう一度放つ。
        /// </summary>
        private static WeaponTypeDefinition WriteLightningType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll, Material ring)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(LightningTypePath);
            SerializedObject serialized = BeginType(type, "19", "電撃", 4, false, new[]
            {
                EnchantmentKind.CritChance, EnchantmentKind.Stun, EnchantmentKind.DamageUp, EnchantmentKind.DropUp,
                EnchantmentKind.Knockback, EnchantmentKind.Multishot, EnchantmentKind.ProjectileCount, EnchantmentKind.RapidFire,
            }, enchantments, roll); // 4 = CharacterAnimatorBuilder.Weapon.Magic

            WriteStaffCommon(serialized, RangedAttackKind.Chain, 0.9f, ring);
            serialized.FindProperty("aimMaxDistance").floatValue = 20f;
            serialized.FindProperty("projectileKnockback").floatValue = 1f;
            serialized.FindProperty("multishotInterval").floatValue = 0.25f;
            serialized.FindProperty("chainAimAngle").floatValue = 10f;
            serialized.FindProperty("chainJumps").intValue = 3;
            serialized.FindProperty("chainJumpsPerCount").intValue = 2;
            serialized.FindProperty("chainRange").floatValue = 6f;
            serialized.FindProperty("chainJumpDelay").floatValue = 0.06f;
            serialized.FindProperty("boltMaterial").objectReferenceValue = ring;
            serialized.FindProperty("boltColor").colorValue = new Color(0.55f, 0.75f, 1f, 1f);
            serialized.FindProperty("boltWidth").floatValue = 0.06f;
            serialized.FindProperty("boltDuration").floatValue = 0.2f;
            serialized.FindProperty("hitEffect").objectReferenceValue = LoadEffect(ElectroHitEffectPath);
            serialized.FindProperty("hitEffectScale").floatValue = 0.5f;
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(LightningSoundPath);
            serialized.FindProperty("hitSound").objectReferenceValue = null; // 放つ雷の音が当たった音を兼ねる
            serialized.FindProperty("soundVolume").floatValue = 0.7f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>
        /// 範囲連置（20）。照準の向きへ、1.2 m 先から 1.1 m おきに 8 か所（約 9 m）、結晶の列（Crystals front attack を 1 本にしたもの）を 0.32 秒で走らせ、
        /// 届いた所から順に当てる。1 か所は半径 0.9 m に当たり、
        /// 1 列は同じ敵に 1 回だけ。撃つ間隔 1.0 秒なので、1 体あたり 強さ × 1.0。数で 20° ずつ扇状に列が増え、持続時間で列が伸び、
        /// 多重で 0.45 秒遅れてもう一列。値は仮。
        /// </summary>
        private static WeaponTypeDefinition WriteSpikeLineType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll, GameObject line)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(SpikeLineTypePath);
            SerializedObject serialized = BeginType(type, "20", "範囲連置", 4, false, new[]
            {
                EnchantmentKind.CritChance, EnchantmentKind.Stun, EnchantmentKind.DamageUp, EnchantmentKind.DropUp,
                EnchantmentKind.Knockback, EnchantmentKind.Multishot, EnchantmentKind.Duration, EnchantmentKind.ProjectileCount,
                EnchantmentKind.RapidFire,
            }, enchantments, roll); // 4 = CharacterAnimatorBuilder.Weapon.Magic

            WriteStaffCommon(serialized, RangedAttackKind.Line, 1.0f, null);
            serialized.FindProperty("projectileKnockback").floatValue = 3f;
            serialized.FindProperty("lineStartDistance").floatValue = 1.2f;
            serialized.FindProperty("lineSpacing").floatValue = 1.1f;
            serialized.FindProperty("linePillarCount").intValue = 8;
            serialized.FindProperty("lineInterval").floatValue = 0.05f;
            serialized.FindProperty("lineRadius").floatValue = 0.9f;
            serialized.FindProperty("lineHeight").floatValue = 2f;
            serialized.FindProperty("lineSpreadAngle").floatValue = 20f;
            serialized.FindProperty("lineRepeatInterval").floatValue = 0.45f;
            serialized.FindProperty("lineEffect").objectReferenceValue = line;
            serialized.FindProperty("lineEffectLength").floatValue = CrystalsFrontAttackLength;
            serialized.FindProperty("lineEffectTravelTime").floatValue = CrystalsFrontAttackTravelTime;
            serialized.FindProperty("hitEffect").objectReferenceValue = LoadEffect(SwordHitEffectPath);
            serialized.FindProperty("hitEffectScale").floatValue = 0.4f;
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(IceSoundPath);
            serialized.FindProperty("hitSound").objectReferenceValue = LoadSound(SwordHitSoundPath);
            serialized.FindProperty("soundVolume").floatValue = 0.7f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>
        /// 火炎放射器（21）。押している間、杖の先から照準へ 6 m・広がり 14° の炎を最長 2.4 秒吐き、0.2 秒ごとにダメージ。吐き切ったら 1.0 秒待つ（途中で離せば吐いた割合だけ）。
        /// 吐き続けて待つ 1 周（3.4 秒）で 強さ × 3.4 を 12 刻みに分けて与える（炎の中にずっと居ればおよそ 強さ の DPS）。値は仮。
        /// </summary>
        private static WeaponTypeDefinition WriteFlamethrowerType(Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments,
            EnchantmentRollSettings roll, GameObject flame)
        {
            var type = Gen.LoadOrCreate<WeaponTypeDefinition>(FlamethrowerTypePath);
            SerializedObject serialized = BeginType(type, "21", "火炎放射器", 4, false, new[]
            {
                EnchantmentKind.CritChance, EnchantmentKind.Size, EnchantmentKind.Stun, EnchantmentKind.DamageUp,
                EnchantmentKind.DropUp, EnchantmentKind.Knockback, EnchantmentKind.Homing, EnchantmentKind.ProjectileSpeed,
                EnchantmentKind.Duration, EnchantmentKind.ProjectileCount, EnchantmentKind.RapidFire,
            }, enchantments, roll); // 4 = CharacterAnimatorBuilder.Weapon.Magic

            WriteStaffCommon(serialized, RangedAttackKind.Flame, 1.0f, null);
            serialized.FindProperty("aimMaxDistance").floatValue = 20f;
            serialized.FindProperty("flameDuration").floatValue = 2.4f;
            serialized.FindProperty("flameTickInterval").floatValue = 0.2f;
            serialized.FindProperty("flameRange").floatValue = 6f;
            serialized.FindProperty("flameAngle").floatValue = 14f;
            serialized.FindProperty("flameBaseRadius").floatValue = 0.25f;
            serialized.FindProperty("flameSpreadAngle").floatValue = 25f;
            serialized.FindProperty("flameHomingAngle").floatValue = 40f;
            serialized.FindProperty("flameHomingTurnRate").floatValue = 120f;
            serialized.FindProperty("flameEffect").objectReferenceValue = flame;
            serialized.FindProperty("flameEffectLength").floatValue = FlameLength;
            serialized.FindProperty("flameSoundInterval").floatValue = 0.6f;
            serialized.FindProperty("hitEffect").objectReferenceValue = LoadEffect(FireHitEffectPath);
            serialized.FindProperty("hitEffectScale").floatValue = 0.25f;
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(FlameSoundPath);
            serialized.FindProperty("hitSound").objectReferenceValue = null; // 吐いている音が当たった音を兼ねる
            serialized.FindProperty("soundVolume").floatValue = 0.6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return type;
        }

        /// <summary>
        /// 範囲連置の 1 列の見た目。Hovl の Crystals front attack のバリアントで、根の粒を 7 つの扇から +X へまっすぐ 1 つにする
        /// （根の粒が通り道に結晶・煙・石を残す作りはそのまま）。列の長さへの伸び縮みは RangedAttacker が出すときにやる。
        /// </summary>
        private static GameObject WriteCrystalLinePrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalsFrontAttackPath);
            if (source == null)
            {
                Debug.LogWarning($"結晶の列の素材が無い: {CrystalsFrontAttackPath}（範囲連置は見た目なしで当たる）");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name = "Effect_CrystalLine";
                var root = instance.GetComponent<ParticleSystem>();
                ParticleSystem.EmissionModule emission = root.emission;
                ParticleSystem.Burst burst = emission.GetBurst(0);
                burst.count = 1;
                emission.SetBurst(0, burst);
                // 素材は扇の幅 45° を -22.5° 回して +X を真ん中にしている。幅を 1° にして +X へそろえる。
                ParticleSystem.ShapeModule shape = root.shape;
                shape.arc = 1f;
                shape.rotation = new Vector3(-90f, -0.5f, 0f);
                return PrefabUtility.SaveAsPrefabAsset(instance, CrystalLinePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>炎の素材そのままの届く距離（m）。粒は秒速 15 m で 0.4 秒飛ぶ。</summary>
        private const float FlameLength = 6f;

        /// <summary>
        /// 吐いている炎。+Z へ円錐（広がり 14°）に柔らかい塊を吹き出し、白黄 → 橙 → 赤 → 暗い煙に色を変えながら膨らませる。
        /// 粒は世界に置いて流す（杖を振ると炎がしなる）。壁や床に当たったら止まる。火の粉と、杖の先の橙の明かりを添える。
        /// </summary>
        private static GameObject WriteFlamePrefab()
        {
            var root = new GameObject("Effect_Flame");
            try
            {
                var flameMaterial = AssetDatabase.LoadAssetAtPath<Material>(FlameMaterialPath);
                var emberMaterial = AssetDatabase.LoadAssetAtPath<Material>(EmberMaterialPath);
                if (flameMaterial == null) Debug.LogWarning($"炎の材質が無い: {FlameMaterialPath}");

                const float lifetime = 0.4f;
                ParticleSystem flame = AddParticles(root, "Flame", flameMaterial);
                ParticleSystem.MainModule main = flame.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.8f, lifetime);
                main.startSpeed = new ParticleSystem.MinMaxCurve(FlameLength / lifetime * 0.9f, FlameLength / lifetime);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.maxParticles = 400;
                ParticleSystem.EmissionModule emission = flame.emission;
                emission.rateOverTime = 140f;
                ParticleSystem.ShapeModule shape = flame.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 14f;
                shape.radius = 0.05f;
                ParticleSystem.SizeOverLifetimeModule size = flame.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.5f, 3.2f), new Keyframe(1f, 5f)));
                ParticleSystem.RotationOverLifetimeModule spin = flame.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
                ParticleSystem.ColorOverLifetimeModule color = flame.colorOverLifetime;
                color.enabled = true;
                color.color = new Gradient
                {
                    colorKeys = new[]
                    {
                        new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                        new GradientColorKey(new Color(1f, 0.65f, 0.15f), 0.25f),
                        new GradientColorKey(new Color(0.95f, 0.25f, 0.05f), 0.6f),
                        new GradientColorKey(new Color(0.25f, 0.08f, 0.04f), 1f),
                    },
                    alphaKeys = new[]
                    {
                        new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f),
                    },
                };
                AddWallCollision(flame);

                ParticleSystem embers = AddParticles(root, "Embers", emberMaterial);
                ParticleSystem.MainModule emberMain = embers.main;
                emberMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                emberMain.startSpeed = new ParticleSystem.MinMaxCurve(8f, 14f);
                emberMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
                emberMain.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.4f, 0.1f));
                emberMain.gravityModifier = -0.3f;
                emberMain.maxParticles = 200;
                ParticleSystem.EmissionModule emberEmission = embers.emission;
                emberEmission.rateOverTime = 40f;
                ParticleSystem.ShapeModule emberShape = embers.shape;
                emberShape.shapeType = ParticleSystemShapeType.Cone;
                emberShape.angle = 20f;
                emberShape.radius = 0.05f;
                embers.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Stretch;
                embers.GetComponent<ParticleSystemRenderer>().lengthScale = 3f;
                AddWallCollision(embers);

                var lightObject = new GameObject("Light");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.localPosition = new Vector3(0f, 0f, 1f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.55f, 0.2f);
                light.range = 6f;
                light.intensity = 3f;
                light.shadows = LightShadows.None;

                return PrefabUtility.SaveAsPrefabAsset(root, FlamePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>parent の下に、+Z へ吹く、世界に置いて流すループの粒を作る。</summary>
        private static ParticleSystem AddParticles(GameObject parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var particles = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        /// <summary>壁や床に当たった粒は勢いを失い、早く消える（炎が壁を抜けない）。</summary>
        private static void AddWallCollision(ParticleSystem particles)
        {
            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.dampen = 0.8f;
            collision.bounce = 0.1f;
            collision.lifetimeLoss = 0.5f;
            collision.radiusScale = 0.3f;
        }

        /// <summary>遠距離（弓・持続弓・杖・投擲）に共通の値。コンボの段は持たない（近接では振らない）。</summary>
        private static void WriteRangedCommon(SerializedObject serialized, RangedAttackKind kind, float fireInterval, GameObject arrow,
            Material ring)
        {
            serialized.FindProperty("comboSteps").arraySize = 0;
            serialized.FindProperty("rangedKind").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(RangedAttackKind)), kind);
            serialized.FindProperty("fireInterval").floatValue = fireInterval;
            serialized.FindProperty("muzzleOffset").vector3Value = new Vector3(0f, 1.4f, 0.5f);
            serialized.FindProperty("aimMaxDistance").floatValue = 40f;
            serialized.FindProperty("projectilePrefab").objectReferenceValue = arrow;
            serialized.FindProperty("projectileSpeed").floatValue = 35f;
            serialized.FindProperty("projectileRange").floatValue = 45f;
            serialized.FindProperty("projectileRadius").floatValue = 0.15f;
            serialized.FindProperty("projectileKnockback").floatValue = 2f;
            serialized.FindProperty("volleySpreadAngle").floatValue = 8f;
            serialized.FindProperty("multishotInterval").floatValue = 0.12f;
            serialized.FindProperty("homingTurnRate").floatValue = 540f;
            serialized.FindProperty("homingRange").floatValue = 12f;
            serialized.FindProperty("rangeRingMaterial").objectReferenceValue = ring;
            serialized.FindProperty("heldInLeftHand").boolValue = true;
            // 左手の骨から見た弓の握り（Play 中に手の武器を動かして合わせた値）。調整済みなら触らない。杖は WriteStaffCommon が入れる。
            if (kind == RangedAttackKind.Bow || kind == RangedAttackKind.Rain) SeedHeldGrip(serialized, new Vector3(0.018f, 0f, 0.072f), Vector3.zero);

            serialized.FindProperty("swingEffect").objectReferenceValue = null;
            serialized.FindProperty("hitEffect").objectReferenceValue = LoadEffect(SwordHitEffectPath);
            serialized.FindProperty("hitEffectScale").floatValue = 0.3f;
            WriteEffectLayers(serialized.FindProperty("slamEffects"), Array.Empty<EffectLayer>());
            serialized.FindProperty("swingSound").objectReferenceValue = LoadSound(BowReleaseSoundPath);
            serialized.FindProperty("hitSound").objectReferenceValue = LoadSound(ArrowHitSoundPath);
            serialized.FindProperty("soundVolume").floatValue = 0.8f;
            serialized.FindProperty("drawSound").objectReferenceValue = LoadSound(BowDrawSoundPath);
            // 壁や床に刺さった音は敵に当たった音と同じ素材を、少し小さく鳴らす。
            serialized.FindProperty("stickSound").objectReferenceValue = LoadSound(ArrowHitSoundPath);
            serialized.FindProperty("stickSoundVolume").floatValue = 0.6f;
            serialized.FindProperty("rainSound").objectReferenceValue = kind == RangedAttackKind.Rain ? LoadSound(RainSoundPath) : null;
            serialized.FindProperty("rainSoundVolume").floatValue = 0.8f;
        }

        /// <summary>
        /// 飛ぶ矢の見た目。原点が矢じりの先で、矢羽根は -Z（ArrowProjectile の決まり）。当たり判定は持たず、細い白の尾を引く。
        /// </summary>
        private static GameObject WriteArrowPrefab()
        {
            var root = new GameObject("Projectile_Arrow");
            try
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowModelPath);
                if (source != null)
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                    foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                    model.transform.SetParent(root.transform, false);
                    // 素材は -Z が矢じりなので、裏返して +Z に向ける。
                    model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    if (TryMeasure(model, out Bounds raw, root.transform) && raw.size.z > 1e-4f)
                        model.transform.localScale = Vector3.one * (ArrowLength / raw.size.z);
                    TryMeasure(model, out Bounds placed, root.transform);
                    model.transform.localPosition = new Vector3(-placed.center.x, -placed.center.y, -placed.max.z);
                }
                else
                {
                    Debug.LogWarning($"矢の素材が無い: {ArrowModelPath}（プリミティブで代える）");
                    Material shaft = Gen.Material("Placeholder_ArrowShaft", new Color32(150, 90, 50, 255));
                    Gen.Part(root, PrimitiveType.Cube, shaft, new Vector3(0f, 0f, -ArrowLength * 0.5f), new Vector3(0.02f, 0.02f, ArrowLength));
                }

                var trail = root.AddComponent<TrailRenderer>();
                trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(RangeRingMaterialPath);
                trail.time = 0.12f;
                trail.minVertexDistance = 0.1f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.03f), new Keyframe(1f, 0f));
                trail.colorGradient = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) },
                };
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                trail.receiveShadows = false;

                return PrefabUtility.SaveAsPrefabAsset(root, ArrowPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>範囲の円と矢の尾の線の材質。頂点色と透明をそのまま出す（Sprites/Default）。</summary>
        private static Material WriteRangeRingMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RangeRingMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default")) { name = "RangeRing" };
                AssetDatabase.CreateAsset(material, RangeRingMaterialPath);
            }

            return material;
        }

        /// <summary>
        /// ランクの光の粒の材質。加算だと明るい床の上で白く飛んでランクの色が出ないので、半透明で重ねる。色は粒の頂点色で付く。
        /// </summary>
        private static Material WriteAuraMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AuraMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default")) { name = "RankAura" };
                AssetDatabase.CreateAsset(material, AuraMaterialPath);
            }

            var point = AssetDatabase.LoadAssetAtPath<Texture2D>(AuraPointTexturePath);
            if (point == null) Debug.LogWarning($"光の粒の絵が無い: {AuraPointTexturePath}");
            material.mainTexture = point;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>素手のときに振る武器。拾えず、インベントリにも入らない（MeleeAttacker が直接持つ）。</summary>
        private static void WriteFists(WeaponTypeDefinition type)
        {
            var fists = Gen.LoadOrCreate<WeaponDefinition>(FistsPath);
            var serialized = new SerializedObject(fists);
            serialized.FindProperty("id").stringValue = "Weapon_Fists";
            serialized.FindProperty("displayName").stringValue = "素手";
            serialized.FindProperty("description").stringValue = "何も持たずに殴る。";
            serialized.FindProperty("rank").enumValueIndex = RankEnumIndex(WeaponRank.E);
            serialized.FindProperty("strength").floatValue = 3f; // 錆びた片手剣（5）より弱い。値は仮。
            serialized.FindProperty("weaponType").objectReferenceValue = type;
            serialized.FindProperty("heldModel").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class ComboStepSpec
        {
            public readonly float Weight, Duration, HitTime, Knockback;
            public readonly Vector3 Size, Center, SwingEffectOffset, SwingEffectEuler;

            public MeleeStepMotion Motion = MeleeStepMotion.Swing;
            public float ReactionBonus;
            public float LungeDistance, LungeDuration;
            public float SlamRadius, SlamHeight;

            public ComboStepSpec(float weight, float duration, float hitTime, Vector3 size, Vector3 center, float knockback,
                Vector3 swingEffectOffset, Vector3 swingEffectEuler)
            {
                Weight = weight;
                Duration = duration;
                HitTime = hitTime;
                Size = size;
                Center = center;
                Knockback = knockback;
                SwingEffectOffset = swingEffectOffset;
                SwingEffectEuler = swingEffectEuler;
            }
        }

        private static void WriteComboSteps(SerializedObject serialized, ComboStepSpec[] steps)
        {
            SerializedProperty combo = serialized.FindProperty("comboSteps");
            combo.arraySize = steps.Length;
            for (int i = 0; i < steps.Length; i++)
            {
                SerializedProperty step = combo.GetArrayElementAtIndex(i);
                step.FindPropertyRelative("damageWeight").floatValue = steps[i].Weight;
                step.FindPropertyRelative("duration").floatValue = steps[i].Duration;
                step.FindPropertyRelative("hitTime").floatValue = steps[i].HitTime;
                step.FindPropertyRelative("hitboxSize").vector3Value = steps[i].Size;
                step.FindPropertyRelative("hitboxCenter").vector3Value = steps[i].Center;
                step.FindPropertyRelative("knockback").floatValue = steps[i].Knockback;
                step.FindPropertyRelative("swingEffectOffset").vector3Value = steps[i].SwingEffectOffset;
                step.FindPropertyRelative("swingEffectEuler").vector3Value = steps[i].SwingEffectEuler;
                step.FindPropertyRelative("motion").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(MeleeStepMotion)), steps[i].Motion);
                step.FindPropertyRelative("reactionBonus").floatValue = steps[i].ReactionBonus;
                step.FindPropertyRelative("lungeDistance").floatValue = steps[i].LungeDistance;
                step.FindPropertyRelative("lungeDuration").floatValue = steps[i].LungeDuration;
                step.FindPropertyRelative("slamRadius").floatValue = steps[i].SlamRadius;
                step.FindPropertyRelative("slamHeight").floatValue = steps[i].SlamHeight;
                // 段ごとの音の上書きは、要る段だけ呼び出し側で入れ直す。
                step.FindPropertyRelative("swingSound").objectReferenceValue = null;
                step.FindPropertyRelative("hitSound").objectReferenceValue = null;
            }
        }

        private static void WriteEffectLayers(SerializedProperty list, EffectLayer[] layers)
        {
            list.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++)
            {
                SerializedProperty layer = list.GetArrayElementAtIndex(i);
                layer.FindPropertyRelative("prefab").objectReferenceValue = layers[i].prefab;
                layer.FindPropertyRelative("scale").floatValue = layers[i].scale;
                layer.FindPropertyRelative("offset").vector3Value = layers[i].offset;
            }
        }

        /// <summary>段 index だけ振りと命中の音を替える（武器種の音より優先される）。</summary>
        private static void SetStepSounds(SerializedObject serialized, int index, string swingPath, string hitPath)
        {
            SerializedProperty step = serialized.FindProperty("comboSteps").GetArrayElementAtIndex(index);
            step.FindPropertyRelative("swingSound").objectReferenceValue = LoadSound(swingPath);
            step.FindPropertyRelative("hitSound").objectReferenceValue = LoadSound(hitPath);
        }

        public static AudioClip LoadSound(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) Debug.LogWarning($"効果音が無い: {path}");
            return clip;
        }

        public static GameObject LoadEffect(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) Debug.LogWarning($"エフェクトが無い: {path}");
            return prefab;
        }

        // ---- 武器 --------------------------------------------------------

        private static void WriteWeapon(WeaponSpec spec, WeaponTypeDefinition type,
            Dictionary<EnchantmentKind, EnchantmentDefinition> enchantments)
        {
            var weapon = Gen.LoadOrCreate<WeaponDefinition>($"{WeaponsFolder}/{spec.Id}.asset");

            var iconSpec = new Gen.Spec
            {
                Id = spec.Id,
                Name = spec.Name,
                Description = spec.Description,
            };
            Texture2D icon = WriteModelIcon(spec);

            // 床に落ちているときは寝かせる。
            iconSpec.BuildModel = (root, mat) =>
            {
                root.transform.localPosition = new Vector3(0f, 0.05f, spec.CenterPivot ? 0f : spec.Grip - spec.Length * 0.5f);
                root.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                BuildSword(root, spec, mat);
            };
            ItemPickup pickup = Gen.BuildPickupPrefab(iconSpec, weapon);
            FitPickupVolume(pickup, spec.Length);
            GameObject held = BuildHeldPrefab(spec);

            var serialized = new SerializedObject(weapon);
            serialized.FindProperty("id").stringValue = spec.Id;
            serialized.FindProperty("displayName").stringValue = spec.Name;
            serialized.FindProperty("description").stringValue = spec.Description;
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("worldPrefab").objectReferenceValue = pickup;
            serialized.FindProperty("rank").enumValueIndex = RankEnumIndex(spec.Rank);
            serialized.FindProperty("strength").floatValue = spec.Strength;
            serialized.FindProperty("weaponType").objectReferenceValue = type;
            serialized.FindProperty("heldModel").objectReferenceValue = held;
            serialized.FindProperty("thrownSpinRate").floatValue = spec.ThrownSpinRate;
            serialized.FindProperty("thrownBounces").boolValue = spec.ThrownBounces;
            serialized.FindProperty("summonModel").objectReferenceValue = spec.SummonModel;
            SerializedProperty fixedList = serialized.FindProperty("fixedEnchantments");
            fixedList.arraySize = spec.FixedEnchantments.Length;
            for (int i = 0; i < spec.FixedEnchantments.Length; i++)
                fixedList.GetArrayElementAtIndex(i).objectReferenceValue = enchantments[spec.FixedEnchantments[i]];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>拾える物の判定を剣の形に合わせる。</summary>
        private static void FitPickupVolume(ItemPickup pickup, float length)
        {
            string path = AssetDatabase.GetAssetPath(pickup);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var volume = root.GetComponent<BoxCollider>();
                volume.size = new Vector3(PickupVolume.x, PickupVolume.y, length + 0.1f);
                volume.center = new Vector3(0f, PickupVolume.y * 0.5f, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>手に持つ見た目。柄の根元が原点で、刃は +Y に伸びる。当たり判定は持たない。</summary>
        private static GameObject BuildHeldPrefab(WeaponSpec spec)
        {
            var root = new GameObject($"Held_{spec.Id}");
            BuildSword(root, spec, Gen.Material);
            if (spec.StaffTip)
            {
                var tip = new GameObject(WeaponTypeDefinition.StaffTipName);
                tip.transform.SetParent(root.transform, false);
                tip.transform.localPosition = Vector3.up * (spec.CenterPivot ? spec.Length * 0.5f : spec.Length - spec.Grip);
            }

            string path = $"{Gen.PrefabsFolder}/{root.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>
        /// 剣を parent の下に作る。柄の根元が原点、刃は +Y。
        /// FreeSwords の素材があれば、いちばん長い向きを +Y に向けて長さを揃えて入れる。無ければプリミティブで組む。
        /// </summary>
        private static void BuildSword(GameObject parent, WeaponSpec spec, Func<string, Color, Material> material)
        {
            var source = string.IsNullOrEmpty(spec.ModelPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
            if (source != null && TryPlaceModel(parent, source, spec.Length, spec.CenterPivot, spec.Grip, spec.Flip))
            {
                if (spec.Tint.HasValue)
                {
                    Material tint = material($"Placeholder_{spec.Id}_Tint", spec.Tint.Value);
                    foreach (Renderer r in parent.GetComponentsInChildren<Renderer>())
                    {
                        var shared = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < shared.Length; i++) shared[i] = tint;
                        r.sharedMaterials = shared;
                    }
                }

                return;
            }

            Material blade = material($"Placeholder_{spec.Id}_Blade", spec.Blade);
            Material hilt = material($"Placeholder_{spec.Id}_Hilt", spec.Hilt);
            Gen.Part(parent, PrimitiveType.Cylinder, hilt, new Vector3(0f, 0.08f, 0f), new Vector3(0.035f, 0.08f, 0.035f));
            Gen.Part(parent, PrimitiveType.Cube, hilt, new Vector3(0f, 0.17f, 0f), new Vector3(0.2f, 0.03f, 0.04f));
            Gen.Part(parent, PrimitiveType.Cube, blade, new Vector3(0f, 0.17f + (spec.Length - 0.17f) * 0.5f, 0f),
                new Vector3(0.06f, spec.Length - 0.17f, 0.012f));
        }

        private static bool TryPlaceModel(GameObject parent, GameObject source, float targetLength, bool centered = false, float grip = 0f,
            bool flip = false)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            if (!TryMeasure(model, out Bounds bounds))
            {
                Object.DestroyImmediate(model);
                return false;
            }

            // いちばん長い軸を +Y に。どちらの端が柄かは素材によるので、向きは確かめて武器種の持ち方で合わせる。
            Vector3 size = bounds.size;
            Quaternion toUp = size.y >= size.x && size.y >= size.z ? Quaternion.identity
                : size.x >= size.z ? Quaternion.Euler(0f, 0f, 90f)
                : Quaternion.Euler(-90f, 0f, 0f);
            if (flip) toUp = Quaternion.Euler(0f, 0f, 180f) * toUp;
            float length = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            float scale = length > 1e-4f ? targetLength / length : 1f;

            var pivot = new GameObject("Model");
            pivot.transform.SetParent(parent.transform, false);
            model.transform.SetParent(pivot.transform, false);
            model.transform.localRotation = toUp;
            model.transform.localScale = Vector3.one * scale;

            // 回したあとの下端から grip 上がった所（centered なら真ん中）を原点へ。
            TryMeasure(model, out Bounds rotated, pivot.transform);
            model.transform.localPosition = new Vector3(-rotated.center.x, centered ? -rotated.center.y : -rotated.min.y - grip, -rotated.center.z);
            return true;
        }

        /// <summary>レンダラーを合わせた境界。space を渡すとその座標系で測る。</summary>
        private static bool TryMeasure(GameObject model, out Bounds bounds, Transform space = null)
        {
            bounds = default;
            bool any = false;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;

                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 toSpace = (space != null ? space.worldToLocalMatrix : Matrix4x4.identity) * filter.transform.localToWorldMatrix;
                Vector3 min = local.min;
                Vector3 max = local.max;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                    Vector3 p = toSpace.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else bounds.Encapsulate(p);
                }
            }

            return any;
        }

        // ---- モデルを撮った絵 ----------------------------------------------

        /// <summary>
        /// 手に持つ見た目と同じ剣をプレビュー用のシーンに置き、斜め 45° に立てて正面から撮る。背景は透明。
        /// 刃の平たい面がカメラを向くよう、横幅の広い向きを画面の横に合わせてから少しひねる。
        /// </summary>
        private static Texture2D WriteModelIcon(WeaponSpec spec)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            var model = new GameObject("IconModel");
            var rt = new RenderTexture(IconSize, IconSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var pivot = new GameObject("Pivot");
                pivot.transform.SetParent(model.transform, false);
                BuildSword(pivot, spec, Gen.Material);
                SceneManager.MoveGameObjectToScene(model, scene);

                // 刃の平たい面を画面に向ける（横幅の広い軸を X に）。少しひねって立体に見せ、45° 傾けて右上へ刃を伸ばす。
                Bounds upright = RendererBounds(model);
                float face = (upright.size.z > upright.size.x ? 90f : 0f) + (spec.IconBackside ? 180f : 0f);
                pivot.transform.localRotation = Quaternion.Euler(0f, face, 0f);
                model.transform.rotation = Quaternion.Euler(0f, 0f, -45f) * Quaternion.Euler(0f, 25f, 0f);

                Bounds bounds = RendererBounds(model);
                var cameraObject = new GameObject("IconCamera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.12f; // 縁取りが切れない余白を残す
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 20f;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.transform.SetPositionAndRotation(bounds.center - Vector3.forward * 5f, Quaternion.identity);
                camera.targetTexture = rt;

                AddLight(scene, Quaternion.Euler(35f, -30f, 0f), 1.2f);
                AddLight(scene, Quaternion.Euler(-20f, 150f, 0f), 0.5f);
                // カメラの側から弱く当て、黒っぽい刃でも面の形が読めるようにする。
                AddLight(scene, Quaternion.Euler(10f, 10f, 0f), 0.6f);

                Texture2D texture = Capture(camera, rt, Color.clear);
                // URP が背景の透明を残さなかったら、抜き取り色で撮り直して色で抜く。
                if (texture.GetPixel(0, 0).a > 0.5f)
                {
                    Object.DestroyImmediate(texture);
                    texture = Capture(camera, rt, KeyColor);
                    KeyOut(texture);
                }

                AddOutline(texture);

                Texture2D icon = Gen.SaveIcon(texture, spec.Id);
                Object.DestroyImmediate(texture);
                return icon;
            }
            finally
            {
                rt.Release();
                Object.DestroyImmediate(rt);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void AddLight(Scene scene, Quaternion rotation, float intensity)
        {
            var go = new GameObject("IconLight");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
        }

        private static Texture2D Capture(Camera camera, RenderTexture rt, Color background)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, IconSize, IconSize), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        /// <summary>抜き取り色に近い画素を透明にする。縁はにじんだ分だけ半透明に。</summary>
        private static void KeyOut(Texture2D texture)
        {
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                // 抜き取り色（マゼンタ）らしさ = 赤と青が緑よりどれだけ強いか。
                float key = Mathf.Clamp01(Mathf.Min(c.r, c.b) - c.g);
                float alpha = 1f - Mathf.SmoothStep(0.35f, 0.85f, key);
                pixels[i] = new Color(c.r, c.g, c.b, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
        }

        /// <summary>
        /// 絵の不透明な部分の周りに <see cref="OutlineColor"/> の縁を敷き、その上に元の絵を重ねる。
        /// 縁の濃さは、不透明な画素までの距離で決める（<see cref="OutlineWidth"/> まで濃く、そこから薄れる）。
        /// </summary>
        private static void AddOutline(Texture2D texture)
        {
            int w = texture.width;
            int h = texture.height;
            Color[] pixels = texture.GetPixels();
            var result = new Color[pixels.Length];
            int reach = Mathf.CeilToInt(OutlineWidth + OutlineSoftness);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // 近くの画素の不透明さを距離で割り引いた最大値 = 縁の濃さ。
                float edge = 0f;
                for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int sx = x + dx;
                    int sy = y + dy;
                    if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;

                    float a = pixels[sy * w + sx].a;
                    if (a <= edge) continue;

                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float falloff = 1f - Mathf.Clamp01((distance - OutlineWidth) / OutlineSoftness);
                    edge = Mathf.Max(edge, a * falloff);
                }

                Color outline = OutlineColor;
                outline.a = edge;
                Color top = pixels[y * w + x];
                // top を outline の上に重ねる（ストレートアルファ）。
                float alpha = top.a + outline.a * (1f - top.a);
                Color rgb = alpha > 0f ? (top * top.a + outline * outline.a * (1f - top.a)) / alpha : Color.clear;
                rgb.a = alpha;
                result[y * w + x] = rgb;
            }

            texture.SetPixels(result);
            texture.Apply();
        }

        private static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one * 0.5f);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
