using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 武器種（全 30 種のうちの 1 つ）の定義。挙動の数値と、付けられるエンチャントのランク表を持つ。
    /// 武器（WeaponDefinition）はこれを参照し、強さとランクだけを持つ。
    /// 近接（コンボ）の武器種は comboSteps を持ち、段ごとの当て方（振る・走る・叩きつける）は MeleeComboStep.motion で選ぶ。
    /// 遠距離の武器種は rangedKind（弓・持続弓・杖の雷・連置・炎・投擲・召喚・治癒持続）を持ち、遠距離の欄の値で撃つ（RangedAttacker）。
    /// お守り・盾の武器種は passiveGear を持ち、振らずに持っているだけで効く（PlayerGear）。
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponType", menuName = "TPS Dungeon/Weapons/Weapon Type")]
    public sealed class WeaponTypeDefinition : ScriptableObject
    {
        /// <summary>杖の手に持つ見た目の中で、杖の先に置く目印の名前。杖の雷・連置・炎はここから出る。</summary>
        public const string StaffTipName = "Tip";

        [SerializeField, Tooltip("武器種の番号（Notion の武器種データの番号。例: 01）。")]
        private string id;

        [SerializeField] private string displayName;

        [SerializeField, Tooltip("キャラの Animator の WeaponType に入れる値（CharacterAnimatorBuilder.Weapon）。")]
        private int animatorWeaponType;

        [SerializeField, Tooltip("盾（武器種 27）の防御が効くか。片手武器なら真。")]
        private bool canUseShield;

        [SerializeField, Tooltip("振らずに持っているだけで効く装備（お守り・盾）か。そうなら攻撃欄は使わず、選んでいる間は素手で殴る。")]
        private PassiveGear passiveGear;

        [Header("エンチャント")]
        [SerializeField, Tooltip("この武器種に付けられるエンチャントの種類・段とそのランク。正は CSV（Items/Weapons/EnchantmentRanks.csv）で、保存すると取り込まれてここが書き換わる。")]
        private List<EnchantmentRankEntry> enchantmentRanks = new List<EnchantmentRankEntry>();

        [SerializeField, Tooltip("エンチャントの付き方（全武器種で共通のアセット）。")]
        private EnchantmentRollSettings enchantmentRoll;

        [Header("ダメージ")]
        [SerializeField, Min(0f), Tooltip("持ち主の基礎攻撃力を武器の強さ（DPS）に足すときの係数。値は仮。")]
        private float characterAttackWeight = 1f;

        [SerializeField, Range(0f, 1f), Tooltip("エンチャント・永続強化の前のクリティカル率。値は仮。")]
        private float baseCritChance = 0.05f;

        [SerializeField, Min(1f), Tooltip("エンチャント・永続強化の前のクリティカル倍率。値は仮。")]
        private float baseCritMultiplier = 1.5f;

        [Header("近接コンボ（値は仮）")]
        [SerializeField, Tooltip("段の並び。最後の段のあとは 1 段目に戻る。")]
        private MeleeComboStep[] comboSteps = Array.Empty<MeleeComboStep>();

        [SerializeField, Min(0f), Tooltip("段が終わってからこの秒数以内に押せば次の段に続く。過ぎたら 1 段目から。")]
        private float comboChainGrace = 0.25f;

        [SerializeField, Min(0f), Tooltip("コンボが切れて（最後の段を振り終えるか、猶予を過ぎて）から次に振れるまでの秒数。")]
        private float comboCooldown = 0.3f;

        [Header("ダッシュ（Lunge の段）")]
        [SerializeField, Tooltip("走っている間は敵をすり抜け、通り道の敵みんなに当てる。偽なら敵にぶつかって止まる。壁ではどちらでも止まる。")]
        private bool lungePassesThroughEnemies = true;

        [SerializeField, Tooltip("走っている間は無敵（MeleeAttacker.IsInvulnerable が真になる。被ダメージ側が読む）。")]
        private bool invulnerableDuringLunge = true;

        [Header("数（Slam の段）")]
        [SerializeField, Range(0f, 90f), Tooltip("「数」のエンチャント 1 つで叩きつけが 1 つ増える。本撃と合わせて、持ち主を中心に前から左右対称に、隣とこの角度（度）ずつ回して並べる。" +
            "それぞれ別の判定で、重なった所の敵は重なった数だけ当たる。")]
        private float slamSpreadAngle = 30f;

        [Header("追撃（Slam の段。「多重」のエンチャント 1 つで、叩きつけ 1 つにつき 1 回。ダメージは本撃と同じ。値は仮）")]
        [SerializeField, Min(0f), Tooltip("k 回目は着弾点から、その叩きつけの向きへ k × この距離（m）ずらして落とす。")]
        private float followUpSpacing = 1.5f;

        [SerializeField, Min(0f), Tooltip("k 回目は本撃から k × この秒数あとに落とす。")]
        private float followUpInterval = 0.18f;

        [SerializeField, Min(0.01f), Tooltip("追撃の着弾の見た目は、本撃の着弾（slamEffects）をこの倍率で出す。")]
        private float followUpEffectScale = 0.7f;

        [Header("カメラ揺れ（Slam の段）")]
        [SerializeField, Min(0f), Tooltip("着弾の瞬間にカメラを揺らす強さ。0 で揺らさない。")]
        private float slamShake;

        [SerializeField, Range(0f, 1f), Tooltip("追撃の着弾で揺らす強さ（本撃に対する割合）。")]
        private float followUpShakeRatio = 0.4f;

        [Header("遠距離（弓・持続弓・杖・投擲・召喚・治癒持続。値は仮）")]
        [SerializeField, Tooltip("撃ち方。None なら遠距離の武器ではない。杖（雷・連置・炎）もここ。")]
        private RangedAttackKind rangedKind;

        [SerializeField, Min(0.05f), Tooltip("撃つ間隔（秒、速射の補正前）。1 発のダメージは 強さ × この秒数。炎は吐き切ってからの待ちで、吐ける時間と合わせた 1 周で 強さ × 1 周。")]
        private float fireInterval = 0.8f;

        [SerializeField, Tooltip("矢を放つ位置。キャラの足元から見たローカル位置（m）。")]
        private Vector3 muzzleOffset = new Vector3(0f, 1.4f, 0.5f);

        [SerializeField, Min(1f), Tooltip("照準を探す距離（m）。弓はこの先に何も無ければこの距離の点へ向けて撃つ。持続弓はこの水平距離までの地面に降らせる。")]
        private float aimMaxDistance = 40f;

        [SerializeField, Tooltip("飛ぶ矢の見た目（+Z が矢の先）。当たり判定はコードが持つので、見た目だけでよい。")]
        private GameObject projectilePrefab;

        [SerializeField, Min(0.1f), Tooltip("矢の速さ（m/s、弾速のエンチャントの補正前）。")]
        private float projectileSpeed = 35f;

        [SerializeField, Min(1f), Tooltip("矢が飛べる距離（m）。")]
        private float projectileRange = 45f;

        [SerializeField, Min(0.01f), Tooltip("矢の当たり判定の半径（m）。")]
        private float projectileRadius = 0.15f;

        [SerializeField, Min(0f), Tooltip("矢が当てた敵を押し出す速さ（m/s、ノックバックのエンチャントの補正前）。")]
        private float projectileKnockback = 2f;

        [SerializeField, Range(0f, 45f), Tooltip("「数」で増えた矢を、狙いを中心に左右対称に並べるときの隣との角度（度）。")]
        private float volleySpreadAngle = 8f;

        [SerializeField, Min(0f), Tooltip("「多重」の一斉射の遅れ。k 回目は本撃から k × この秒数あとに同じ向きへ放つ。")]
        private float multishotInterval = 0.12f;

        [SerializeField, Min(0f), Tooltip("ホーミングで矢が向きを変えられる速さ（度/秒）。")]
        private float homingTurnRate = 540f;

        [SerializeField, Min(0f), Tooltip("ホーミングで追う敵を探す距離（m）。矢の前にいる敵だけを追う。")]
        private float homingRange = 12f;

        [Header("矢の雨（持続弓。値は仮）")]
        [SerializeField, Min(0.1f), Tooltip("雨の範囲の半径（m、サイズのエンチャントの補正前）。")]
        private float rainRadius = 1.25f;

        [SerializeField, Min(0.1f), Tooltip("この高さ（m）までの上下にいる敵に当てる。")]
        private float rainHeight = 2f;

        [SerializeField, Min(0f), Tooltip("雨の続く時間（秒、持続時間のエンチャントの補正前）。")]
        private float rainDuration = 3f;

        [SerializeField, Min(0.05f), Tooltip("雨がダメージを与える間隔（秒）。")]
        private float rainTickInterval = 0.5f;

        [SerializeField, Min(0f), Tooltip("撃ってから雨が降り始めるまで（秒、弾速のエンチャントで縮む）。")]
        private float rainDelay = 0.7f;

        [SerializeField, Min(0f), Tooltip("「数」で増えた雨を置く、狙った所からの距離の下限（m、サイズで伸びる）。向きと距離はランダム。")]
        private float rainScatterMin = 1.25f;

        [SerializeField, Min(0f), Tooltip("「数」で増えた雨を置く、狙った所からの距離の上限（m、サイズで伸びる）。")]
        private float rainScatterMax = 3f;

        [SerializeField, Min(0f), Tooltip("「多重」で同じ所にもう一度降らせる遅れ。k 回目は本撃から k × この秒数あとに降り始める。")]
        private float rainRepeatInterval = 1f;

        [SerializeField, Tooltip("雨の見た目（任意）。降り始めに範囲の中心へ出し、雨の間だけ続ける。繰り返すバーストは雨の長さまで延ばす。")]
        private GameObject rainEffect;

        [SerializeField, Min(0.01f), Tooltip("rainEffect の素材そのままの範囲の半径（m）。雨の半径に合うよう水平だけ縮める（縦はそのまま）。")]
        private float rainEffectRadius = 4f;

        [SerializeField, Range(-30f, 80f), Tooltip("持続弓を構えている間、上半身を上へ反らせる角度（度）。空へ放つ構えに見せる。")]
        private float rainAimPitch = 35f;

        [Header("杖（雷・連置）・投擲・召喚・治癒持続。値は仮")]
        [SerializeField, Min(0f), Tooltip("押してから実際に放つまでの秒数。撃ち出しのモーションで杖を突き出す瞬間（投擲・召喚・治癒持続は腕を振り切る瞬間）に合わせる。狙いは放つ瞬間のもの。炎には効かない。腕を振る武器種は速射でモーションを速めた分だけ縮む。")]
        private float castDelay = 0.12f;

        [Header("連鎖する雷（電撃。値は仮）")]
        [SerializeField, Range(0f, 45f), Tooltip("照準の線からこの角度（度）以内にいる敵のうち、いちばん照準に近い敵へ放つ。居なければ照準の先へ空撃ち。")]
        private float chainAimAngle = 10f;

        [SerializeField, Min(0), Tooltip("最初の敵に当ててから、次の敵へ飛び移る回数（数のエンチャントの補正前）。")]
        private int chainJumps = 3;

        [SerializeField, Min(0), Tooltip("「数」のエンチャント 1 つで増える、飛び移る回数。")]
        private int chainJumpsPerCount = 2;

        [SerializeField, Min(0.5f), Tooltip("当てた敵から、この距離（m）以内でいちばん近い、まだ当てていない敵へ飛び移る。")]
        private float chainRange = 6f;

        [SerializeField, Min(0f), Tooltip("1 回飛び移るのにかかる秒数。")]
        private float chainJumpDelay = 0.06f;

        [SerializeField, Tooltip("雷の線の材質（頂点色と透明をそのまま出すもの）。未設定なら線を出さない。")]
        private Material boltMaterial;

        [SerializeField, Tooltip("雷の線の色。芯は白く、この色の光をまとう。")]
        private Color boltColor = new Color(0.55f, 0.75f, 1f, 1f);

        [SerializeField, Min(0.005f), Tooltip("雷の芯の太さ（m）。光はこの 4 倍。")]
        private float boltWidth = 0.06f;

        [SerializeField, Min(0.02f), Tooltip("雷の線が見えている秒数。")]
        private float boltDuration = 0.2f;

        [Header("地面から連ねて出す（範囲連置。値は仮）")]
        [SerializeField, Min(0f), Tooltip("1 つ目を出す、足元から照準の向きへの距離（m）。")]
        private float lineStartDistance = 1.2f;

        [SerializeField, Min(0.1f), Tooltip("隣り合う 1 つとの間隔（m）。")]
        private float lineSpacing = 1.1f;

        [SerializeField, Min(1), Tooltip("1 列に出す数（持続時間のエンチャントの補正前）。壁に当たったらそこで止める。")]
        private int linePillarCount = 8;

        [SerializeField, Min(0f), Tooltip("隣り合う 1 つを出す遅れ（秒）。手前から順に出る。lineEffect があるときは使わず、見た目が届く時刻に合わせる。")]
        private float lineInterval = 0.05f;

        [SerializeField, Min(0.1f), Tooltip("1 つが当たる範囲の半径（m）。1 列は同じ敵に 1 回だけ当たる。")]
        private float lineRadius = 0.9f;

        [SerializeField, Min(0.1f), Tooltip("この高さ（m）までの上下にいる敵に当てる。")]
        private float lineHeight = 2f;

        [SerializeField, Range(0f, 90f), Tooltip("「数」で増えた列を、照準の向きを中心に右左交互に開くときの隣との角度（度）。")]
        private float lineSpreadAngle = 20f;

        [SerializeField, Min(0f), Tooltip("「多重」でもう一列出す遅れ。k 回目は本撃から k × この秒数あとに、同じ向きへ頭から出す。")]
        private float lineRepeatInterval = 0.45f;

        [SerializeField, Tooltip("1 列の見た目（任意）。根の粒が原点から +X へ走り、通り道に結晶などを残す 1 回きりの素材（Crystals front attack を 1 本にしたもの）。" +
            "列の頭に、列の向きへ +X を合わせて出し、根の粒の速さと終わりの飾りの位置を列の長さに合わせて伸び縮みさせる。")]
        private GameObject lineEffect;

        [SerializeField, Min(0.1f), Tooltip("lineEffect の素材そのままの列の長さ（m）。")]
        private float lineEffectLength = 7.8f;

        [SerializeField, Min(0.01f), Tooltip("lineEffect が列の端まで届く秒数（根の粒の寿命）。当たりもこの速さで手前から順に出す。")]
        private float lineEffectTravelTime = 0.32f;

        [Header("炎（火炎放射器。値は仮）")]
        [SerializeField, Min(0.1f), Tooltip("押し続けて炎を吐ける時間（秒、持続時間のエンチャントの補正前）。この時間を刻みの間隔で割った回数だけ刻んだら止まる。" +
            "離せばそこで止まり、止まってから 撃つ間隔 × 吐いた割合 だけ待つ（吐き切れば撃つ間隔まるごと）。")]
        private float flameDuration = 2.4f;

        [SerializeField, Min(0.05f), Tooltip("炎がダメージを与える間隔（秒、速射の補正前）。")]
        private float flameTickInterval = 0.2f;

        [SerializeField, Min(0.5f), Tooltip("炎が届く距離（m、弾速のエンチャントの補正前）。壁があればそこまで。")]
        private float flameRange = 6f;

        [SerializeField, Range(1f, 60f), Tooltip("炎の広がり（中心の線からの角度、度、サイズのエンチャントの補正前）。")]
        private float flameAngle = 14f;

        [SerializeField, Min(0f), Tooltip("杖の先での炎の太さ（半径、m）。先へ行くほど広がりの角度で太くなる。")]
        private float flameBaseRadius = 0.25f;

        [SerializeField, Range(0f, 90f), Tooltip("「数」で増えた炎の筋を、照準の向きを中心に右左交互に開くときの隣との角度（度）。")]
        private float flameSpreadAngle = 25f;

        [SerializeField, Range(0f, 90f), Tooltip("ホーミングで、炎の筋の向きからこの角度（度）以内の敵へ曲げる。")]
        private float flameHomingAngle = 40f;

        [SerializeField, Min(0f), Tooltip("ホーミングで炎の筋が向きを変えられる速さ（度/秒）。")]
        private float flameHomingTurnRate = 120f;

        [SerializeField, Tooltip("吐いている間の炎の見た目（任意。+Z へ吐くループする素材）。筋ごとに 1 つ出し、杖の先で向きを合わせ続ける。")]
        private GameObject flameEffect;

        [SerializeField, Min(0.1f), Tooltip("flameEffect の素材そのままの炎が届く距離（m）。届く距離に合わせて伸び縮みさせる。")]
        private float flameEffectLength = 6f;

        [SerializeField, Min(0.05f), Tooltip("吐いている間、放つ音（swingSound）をこの秒数ごとに鳴らし直す。")]
        private float flameSoundInterval = 0.6f;

        [Header("召喚（値は仮）")]
        [SerializeField, Min(0.1f), Tooltip("呼び出した置物が居る時間（秒、持続時間のエンチャントの補正前）。呼び直せば前の分はその場で消える。")]
        private float summonDuration = 12f;

        [SerializeField, Min(0f), Tooltip("「数」で増えた置物を置く、狙った所からの距離の下限（m）。向きと距離はランダム。")]
        private float summonScatterMin = 1.2f;

        [SerializeField, Min(0f), Tooltip("「数」で増えた置物を置く、狙った所からの距離の上限（m）。")]
        private float summonScatterMax = 2.5f;

        [SerializeField, Min(0f), Tooltip("置物どうしと持ち主から、この距離（m）より近くに置かない（ランダムに引き直す）。")]
        private float summonMinGap = 1f;

        [SerializeField, Min(0f), Tooltip("おとりの体力 ＝ 武器の強さ × この値。敵の攻撃はまだ無いので、値を持たせるところまで。")]
        private float summonHealthPerStrength = 10f;

        [SerializeField, Tooltip("置物が出るときに足元へ出す見た目（任意）。")]
        private GameObject summonEffect;

        [SerializeField, Min(0.01f), Tooltip("summonEffect の大きさの倍率。")]
        private float summonEffectScale = 1f;

        [SerializeField, Tooltip("置物が消えるときに出す見た目（任意）。")]
        private GameObject dismissEffect;

        [SerializeField, Min(0.01f), Tooltip("dismissEffect の大きさの倍率。")]
        private float dismissEffectScale = 1f;

        [Header("治癒の場（治癒持続。値は仮）")]
        [SerializeField, Min(0.1f), Tooltip("治癒の場の半径（m、サイズのエンチャントの補正前）。")]
        private float healRadius = 2f;

        [SerializeField, Min(0.1f), Tooltip("この高さ（m）までの上下にいる味方を回復する。")]
        private float healHeight = 2f;

        [SerializeField, Min(0f), Tooltip("治癒の場の続く時間（秒、持続時間のエンチャントの補正前）。")]
        private float healDuration = 5f;

        [SerializeField, Min(0.05f), Tooltip("治癒の場が回復する間隔（秒）。")]
        private float healTickInterval = 0.5f;

        [SerializeField, Min(0.05f), Tooltip("投げた種が狙った地面に落ちるまでの秒数（弾速のエンチャントは付かない）。")]
        private float healFlightTime = 0.55f;

        [SerializeField, Min(0f), Tooltip("投げた種が描く弧のいちばん高い所（m、投げた所と落ちる所を結ぶ線から）。")]
        private float healArcHeight = 2f;

        [SerializeField, Min(0f), Tooltip("「数」で増えた場を置く、狙った所からの距離の下限（m、サイズで伸びる）。向きと距離はランダム。")]
        private float healScatterMin = 2f;

        [SerializeField, Min(0f), Tooltip("「数」で増えた場を置く、狙った所からの距離の上限（m、サイズで伸びる）。")]
        private float healScatterMax = 4f;

        [SerializeField, Min(0f), Tooltip("「多重」で同じ所にもう一度張る遅れ。k 回目は本撃から k × この秒数あとに張る。")]
        private float healRepeatInterval = 1.5f;

        [SerializeField, Tooltip("投げる種の見た目（任意）。当たり判定は無い。")]
        private GameObject healSeedPrefab;

        [SerializeField, Tooltip("治癒の場の見た目（任意）。場の中心に出し、水平だけ半径に合わせて縮め、場の間だけ続ける。")]
        private GameObject healEffect;

        [SerializeField, Min(0.01f), Tooltip("healEffect の素材そのままの範囲の半径（m）。")]
        private float healEffectRadius = 4f;

        [SerializeField, Tooltip("治癒の場の円の色。")]
        private Color healRingColor = new Color(0.45f, 1f, 0.5f, 0.85f);

        [SerializeField, Tooltip("場を張った瞬間と、味方を回復した刻みに鳴らす音。")]
        private AudioClip healSound;

        [SerializeField, Range(0f, 1f), Tooltip("治癒の音の大きさ（武器種の音量に対して）。刻みごとの音はこの 0.4 倍。")]
        private float healSoundVolume = 0.8f;

        [Header("遠距離の効果音（未設定なら鳴らさない。放つ音は swingSound、敵に当たった音は hitSound）")]
        [SerializeField, Tooltip("弓を引き絞る音。弓のモーションが引き絞り（Bow Draw）に入るたびに鳴らす（持ち替えたときと、撃って引き直すとき）。")]
        private AudioClip drawSound;

        [SerializeField, Tooltip("矢が壁や床に刺さった音。")]
        private AudioClip stickSound;

        [SerializeField, Range(0f, 1f), Tooltip("壁や床に刺さった音の大きさ（武器種の音量に対して）。")]
        private float stickSoundVolume = 0.6f;

        [SerializeField, Tooltip("矢の雨が降る音。降り始めと、刻みごとに鳴らす。")]
        private AudioClip rainSound;

        [SerializeField, Range(0f, 1f), Tooltip("矢の雨が降る音の大きさ（武器種の音量に対して）。刻みごとの音はこの半分。")]
        private float rainSoundVolume = 0.8f;

        [SerializeField, Tooltip("範囲の円（照準の地面の円と、降っている雨の円）の線の材質。未設定なら円を出さない。")]
        private Material rangeRingMaterial;

        [SerializeField, Tooltip("降る雨の範囲の円の色。")]
        private Color rainRingColor = new Color(1f, 0.55f, 0.2f, 0.85f);

        [SerializeField, Tooltip("照準の地面の円の色。")]
        private Color aimRingColor = new Color(1f, 1f, 1f, 0.6f);

        [Header("見た目")]
        [SerializeField, Tooltip("手に持つのを左手にする（弓）。偽なら右手。")]
        private bool heldInLeftHand;

        [SerializeField, Tooltip("手に持ったときの見た目の位置合わせ（持つ手の骨から見たローカル）。" +
            "Play 中に手の武器（Tools/TPS Dungeon/Player/手の武器を選ぶ）を Scene ビューで動かすと、ここに書き戻る。")]
        private Vector3 heldLocalPosition;

        [SerializeField, Tooltip("手に持ったときの見た目の向き（右手の骨から見たローカルのオイラー角）。")]
        private Vector3 heldLocalEuler;

        [Header("エフェクト")]
        [SerializeField, Tooltip("振りの判定の瞬間に出す（任意）。位置と向きは段ごとの swingEffectOffset / swingEffectEuler。")]
        private GameObject swingEffect;

        [SerializeField, Min(0.01f), Tooltip("振りのエフェクトの大きさの倍率。")]
        private float swingEffectScale = 1f;

        [SerializeField, Min(0f), Tooltip("振りのエフェクトの中で「放つ」時刻（秒）。溜めてから放つ素材（Charge slash など）用。" +
            "0 より大きいと、段の振り始めに体へ付けて出して溜めを見せ、この時刻より後にしか出ない粒（斬撃の本体）は判定の瞬間に出す。0 なら判定の瞬間に全部出す。")]
        private float swingEffectLeadTime;

        [SerializeField, Tooltip("敵に当たった所に出す（任意）。")]
        private GameObject hitEffect;

        [SerializeField, Min(0.01f), Tooltip("命中のエフェクトの大きさの倍率。")]
        private float hitEffectScale = 1f;

        [SerializeField, Tooltip("Slam の段で着弾点に重ねて出す（任意）。追撃の着弾にも縮めて出す。")]
        private EffectLayer[] slamEffects = Array.Empty<EffectLayer>();

        [Header("効果音（未設定なら鳴らさない）")]
        [SerializeField, Tooltip("振りの判定の瞬間（遠距離なら矢を放った瞬間）に、当たっても外れても鳴らす音（風切り）。段ごとに替えるなら MeleeComboStep.swingSound。")]
        private AudioClip swingSound;

        [SerializeField, Tooltip("敵に当たったときに鳴らす音。何体に当たっても 1 振りに 1 回。敵側の被弾音にも重なる。段ごとに替えるなら MeleeComboStep.hitSound。")]
        private AudioClip hitSound;

        [SerializeField, Range(0f, 1f), Tooltip("この武器種の音の音量（SE 音量に掛かる）。")]
        private float soundVolume = 1f;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public int AnimatorWeaponType => animatorWeaponType;
        public bool CanUseShield => canUseShield;
        public PassiveGear PassiveGear => passiveGear;
        public bool IsCharm => passiveGear == PassiveGear.Charm;
        public bool IsShield => passiveGear == PassiveGear.Shield;
        public bool IsPassiveGear => passiveGear != PassiveGear.None;
        public IReadOnlyList<EnchantmentRankEntry> EnchantmentRanks => enchantmentRanks;

        /// <summary>ランク表に出てくる種類（重複なし、表の並び順）。</summary>
        public IReadOnlyList<EnchantmentDefinition> AllowedEnchantments
        {
            get
            {
                var result = new List<EnchantmentDefinition>();
                foreach (EnchantmentRankEntry entry in enchantmentRanks)
                {
                    if (entry.Definition != null && !result.Contains(entry.Definition)) result.Add(entry.Definition);
                }

                return result;
            }
        }
        public EnchantmentRollSettings EnchantmentRoll => enchantmentRoll;
        public float CharacterAttackWeight => characterAttackWeight;
        public float BaseCritChance => baseCritChance;
        public float BaseCritMultiplier => baseCritMultiplier;
        public IReadOnlyList<MeleeComboStep> ComboSteps => comboSteps;
        public float ComboChainGrace => comboChainGrace;
        public float ComboCooldown => comboCooldown;
        public bool IsMelee => comboSteps != null && comboSteps.Length > 0;
        public RangedAttackKind RangedKind => rangedKind;
        public bool IsRanged => rangedKind != RangedAttackKind.None;
        public float FireInterval => fireInterval;
        public Vector3 MuzzleOffset => muzzleOffset;
        public float AimMaxDistance => aimMaxDistance;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileRange => projectileRange;
        public float ProjectileRadius => projectileRadius;
        public float ProjectileKnockback => projectileKnockback;
        public float VolleySpreadAngle => volleySpreadAngle;
        public float MultishotInterval => multishotInterval;
        public float HomingTurnRate => homingTurnRate;
        public float HomingRange => homingRange;
        public float RainRadius => rainRadius;
        public float RainHeight => rainHeight;
        public float RainDuration => rangedKind == RangedAttackKind.Rain ? rainDuration : 0f;
        public float RainTickInterval => rainTickInterval;
        public float RainDelay => rainDelay;
        public float RainScatterMin => rainScatterMin;
        public float RainScatterMax => rainScatterMax;
        public float RainRepeatInterval => rainRepeatInterval;
        public GameObject RainEffect => rainEffect;
        public float RainEffectRadius => rainEffectRadius;
        public float RainAimPitch => rainAimPitch;
        public float CastDelay => castDelay;
        public float ChainAimAngle => chainAimAngle;
        public float ChainRange => chainRange;
        public float ChainJumpDelay => chainJumpDelay;
        public Material BoltMaterial => boltMaterial;
        public Color BoltColor => boltColor;
        public float BoltWidth => boltWidth;
        public float BoltDuration => boltDuration;
        public float LineStartDistance => lineStartDistance;
        public float LineSpacing => lineSpacing;
        public float LineInterval => lineInterval;
        public float LineRadius => lineRadius;
        public float LineHeight => lineHeight;
        public float LineSpreadAngle => lineSpreadAngle;
        public float LineRepeatInterval => lineRepeatInterval;
        public GameObject LineEffect => lineEffect;
        public float LineEffectLength => lineEffectLength;
        public float LineEffectTravelTime => lineEffectTravelTime;
        public float FlameDuration => rangedKind == RangedAttackKind.Flame ? flameDuration : 0f;
        public float FlameTickInterval => flameTickInterval;
        public float FlameRange => flameRange;
        public float FlameAngle => flameAngle;
        public float FlameBaseRadius => flameBaseRadius;
        public float FlameSpreadAngle => flameSpreadAngle;
        public float FlameHomingAngle => flameHomingAngle;
        public float FlameHomingTurnRate => flameHomingTurnRate;
        public GameObject FlameEffect => flameEffect;
        public float FlameEffectLength => flameEffectLength;
        public float FlameSoundInterval => flameSoundInterval;

        public float SummonDuration => rangedKind == RangedAttackKind.Summon ? summonDuration : 0f;
        public float SummonScatterMin => summonScatterMin;
        public float SummonScatterMax => summonScatterMax;
        public float SummonMinGap => summonMinGap;
        public float SummonHealthPerStrength => summonHealthPerStrength;
        public GameObject SummonEffect => summonEffect;
        public float SummonEffectScale => summonEffectScale;
        public GameObject DismissEffect => dismissEffect;
        public float DismissEffectScale => dismissEffectScale;
        public float HealRadius => healRadius;
        public float HealHeight => healHeight;
        public float HealDuration => rangedKind == RangedAttackKind.HealField ? healDuration : 0f;
        public float HealTickInterval => healTickInterval;
        public float HealFlightTime => healFlightTime;
        public float HealArcHeight => healArcHeight;
        public float HealScatterMin => healScatterMin;
        public float HealScatterMax => healScatterMax;
        public float HealRepeatInterval => healRepeatInterval;
        public GameObject HealSeedPrefab => healSeedPrefab;
        public GameObject HealEffect => healEffect;
        public float HealEffectRadius => healEffectRadius;
        public Color HealRingColor => healRingColor;
        public AudioClip HealSound => healSound;
        public float HealSoundVolume => healSoundVolume;

        /// <summary>召喚か。狙った地面に置物（おとり）を呼び出す。</summary>
        public bool IsSummon => rangedKind == RangedAttackKind.Summon;

        /// <summary>治癒持続か。狙った地面へ種を投げ、落ちた所に治癒の場を張る。</summary>
        public bool IsHealField => rangedKind == RangedAttackKind.HealField;

        /// <summary>狙う先が照準の線の当たった所ではなく、その真下の地面か（持続弓・召喚・治癒持続）。地面に範囲の円を出す。</summary>
        public bool AimsAtGround => rangedKind == RangedAttackKind.Rain || IsSummon || IsHealField;

        /// <summary>撃つとき腕を前へ振るモーション（Kevin の右手の突き）を流すか（投擲・召喚・治癒持続）。速射でモーションも速める。</summary>
        public bool UsesThrowMotion => IsThrow || IsSummon || IsHealField;

        /// <summary>杖（雷・連置・炎）か。弓と違って、腕ではなく体の前を狙いへ向け、杖の先（手の武器の Tip）から放つ。</summary>
        public bool IsStaff => rangedKind == RangedAttackKind.Chain || rangedKind == RangedAttackKind.Line || rangedKind == RangedAttackKind.Flame;

        /// <summary>投擲か。手に持った武器の見た目そのものを投げ、投げてから次が投げられるまで手を空にする。</summary>
        public bool IsThrow => rangedKind == RangedAttackKind.Throw;

        /// <summary>
        /// 狙うとき、腕ではなく体の前を狙いへ向けるか（背骨も曲げない）。杖は構えで腕の向きが変わり、投擲は腕を振るので、
        /// 弓のように腕を狙いへ向けると体がぶれる。召喚・治癒持続も腕を振るので同じ。
        /// </summary>
        public bool FacesBodyToAim => IsStaff || UsesThrowMotion;

        /// <summary>雷が最初の敵から飛び移る回数。extraProjectiles は「数」の合計の切り捨て（RangedWeaponStats.ExtraProjectiles）。</summary>
        public int ChainJumpsFor(int extraProjectiles) => Mathf.Max(0, chainJumps + Mathf.Max(0, extraProjectiles) * chainJumpsPerCount);

        /// <summary>連置の 1 列に出す数。durationScale は持続時間の倍率（RangedWeaponStats.DurationScale）。</summary>
        public int LinePillarsFor(float durationScale) => Mathf.Max(1, Mathf.RoundToInt(linePillarCount * Mathf.Max(0.1f, durationScale)));
        public AudioClip DrawSound => drawSound;
        public AudioClip StickSound => stickSound;
        public float StickSoundVolume => stickSoundVolume;
        public AudioClip RainSound => rainSound;
        public float RainSoundVolume => rainSoundVolume;
        public Material RangeRingMaterial => rangeRingMaterial;
        public Color RainRingColor => rainRingColor;
        public Color AimRingColor => aimRingColor;
        public bool HeldInLeftHand => heldInLeftHand;
        public Vector3 HeldLocalPosition => heldLocalPosition;
        public Vector3 HeldLocalEuler => heldLocalEuler;
        public GameObject SwingEffect => swingEffect;
        public float SwingEffectScale => swingEffectScale;
        public float SwingEffectLeadTime => swingEffectLeadTime;
        public bool LungePassesThroughEnemies => lungePassesThroughEnemies;
        public bool InvulnerableDuringLunge => invulnerableDuringLunge;
        public float SlamSpreadAngle => slamSpreadAngle;
        public float FollowUpSpacing => followUpSpacing;
        public float FollowUpInterval => followUpInterval;
        public float FollowUpEffectScale => followUpEffectScale;
        public float SlamShake => slamShake;
        public float FollowUpShakeRatio => followUpShakeRatio;
        public IReadOnlyList<EffectLayer> SlamEffects => slamEffects ?? Array.Empty<EffectLayer>();
        public GameObject HitEffect => hitEffect;
        public float HitEffectScale => hitEffectScale;
        public AudioClip SwingSound => swingSound;
        public AudioClip HitSound => hitSound;
        public float SoundVolume => soundVolume;
    }
}
