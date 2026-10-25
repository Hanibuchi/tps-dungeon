using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TpsDungeon.Audio.Runtime;
using TpsDungeon.Items;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Progression;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Party
{
    /// <summary>
    /// パーティーを抱えてシーンをまたぐ。地上（Surface）のパーティー一式（操作台・仲間・カメラ）をまとめた親に付ける。
    /// - 最初の 1 つは DontDestroyOnLoad で残り、門や階段の頼み（<see cref="SceneTravel"/>）を受けてシーンを読み込む。
    /// - 読み込んだシーンにあるカメラは止め（こちらのカメラで見る）、階なら NavMesh を焼く役（<see cref="FloorNavMesh"/>）を付け、スポーン地点（<see cref="PlayerSpawnPoint"/>）ができたら先頭をそこへ移して並び直す。
    ///   階のスポーン地点は生成（FloorBootstrap.Start）のあとにできるので、しばらく待つ。
    /// - 移るときは音を鳴らして暗転し、読み込んだシーンで先頭を並べ終えたら明転する（<see cref="ScreenFade"/>）。
    /// - 全員が倒れたら全滅の演出（<see cref="WipeSequence"/>）を回してから死後の場所（Afterlife）へ送り、全快にする（そこで魂の姿で歩き、転生の渦から地上へ戻る）。
    ///   送る暗転の間に、操作していたメンバー 1 人のレベルをソウルに変え、全員を Lv1 に戻す（<see cref="SoulUpgradeWorld.Harvest"/>）。
    /// - 転生の渦に入って白く飛んだら、持ち越せる数（魂の強化）があれば持ち越す武器を選ばせ（<see cref="CarryOverPicker"/>）、ほかの持ち物を手放す。
    /// - 魂の強化をゲームに当てるのは <see cref="SoulUpgradeWorld"/>（自分の GameObject に付ける）。
    /// - 全滅から死後の場所に着いたときは、明転の代わりに目を覚ます演出（<see cref="WakeSequence"/>。瞬きしながら視界がはっきりしてくる）を回す。
    /// - 死後の場所から地上へ戻る（転生する）ときは、暗転の代わりに転生の演出（<see cref="RebirthSequence"/>。渦に吸い込まれて白く飛び、白い光の中で目覚める）を回す。
    /// - 倒れた人を残して別のシーンへ移るときは、その人をそこに置いていく（次のシーンには出さない。転生で全快にするとき戻る）。
    /// - 地上へ戻ったとき、シーンに置いてある 2 つ目は、子が起きる前に自分ごと消える（実行順を一番先にしてある）。
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Game Flow")]
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField, Tooltip("抱えるパーティー。未設定なら子から探す。")]
        private Party party;

        [SerializeField, Tooltip("全員が倒れたときに流す曲。")]
        private AudioClip deathBgm;

        [SerializeField, Tooltip("転生の渦に入ったときに鳴らす音。")]
        private AudioClip rebirthSound;

        [SerializeField, Min(0f), Tooltip("読み込んだシーンにスポーン地点ができるのを待つ秒数。過ぎたらその場のまま並び直す。")]
        private float spawnWaitTimeout = 5f;

        [Header("移るときの演出")]
        [SerializeField, Tooltip("シーンを移るときに鳴らす音（暗転し始めに 1 回）。")]
        private AudioClip travelSound;

        [SerializeField, Range(0f, 1f), Tooltip("移るときの音の大きさ。")]
        private float travelSoundVolume = 0.8f;

        [SerializeField, Min(0f), Tooltip("暗転にかける秒数。暗転と明転を合わせて移るときの演出の長さになる。")]
        private float fadeOutSeconds = 0.75f;

        [SerializeField, Min(0f), Tooltip("着いたシーンで先頭を並べ終えてから明転にかける秒数。")]
        private float fadeInSeconds = 0.75f;

        private ScreenFade fade;

        private Action<string> travelHandler;
        private bool traveling;
        private WipeSequence wipe;
        private bool wiping;
        private WakeSequence wake;
        private bool wakeOnArrival;
        private RebirthSequence rebirth;
        private bool rebirthOnArrival;
        private SoulUpgradeWorld soulUpgrades;

        /// <summary>転生で持ち越す武器を選ばせる画面。画面の側が自分を入れる。無ければ選ばせずに全部手放す。</summary>
        public static ICarryOverPicker CarryOverPicker { get; set; }

        /// <summary>今シーンをまたいで残っているもの。地上を通らずに始めたシーンでは null。</summary>
        public static GameFlow Instance { get; private set; }

        /// <summary>シーンを移っている途中か（読み込みからスポーン地点へ並べ終えるまで）。</summary>
        public bool IsTraveling => traveling;

        /// <summary>全滅の演出の途中か（全員が倒れてから、死後の場所へ移る暗転まで）。この間はポーズやメニューを開かせない。</summary>
        public bool IsWiping => wiping || (wipe != null && wipe.IsPlaying);

        /// <summary>全滅の演出。</summary>
        public WipeSequence Wipe => wipe;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 地上へ戻ってきたときの、シーンに置いてある方。子（操作台・仲間）が起きる前に止めて消す。
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (party == null) party = GetComponentInChildren<Party>(true);
            travelHandler = sceneName => Travel(sceneName);
            fade = ScreenFade.Create(transform);
            wipe = WipeSequence.Create(transform);
            wipe.DeathBgm = deathBgm;
            wake = WakeSequence.Create(transform);
            rebirth = RebirthSequence.Create(transform);
            rebirth.DiveSound = rebirthSound;
            soulUpgrades = gameObject.AddComponent<SoulUpgradeWorld>();
            soulUpgrades.Party = party;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            if (Instance != this) return;
            SceneTravel.Handler = travelHandler;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            if (SceneTravel.Handler == travelHandler) SceneTravel.Handler = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            CarryOverPicker = null;
            SceneTravel.Traveling = false;
        }

        private void Update()
        {
            if (traveling || party == null) return;

            if (wiping || !IsWiped()) return;
            StartCoroutine(PlayWipe());
        }

        /// <summary>全滅の演出を回してから死後の場所へ送る。演出は暗転し終えたところで元へ戻す（<see cref="LeaveFor"/>）。</summary>
        private IEnumerator PlayWipe()
        {
            wiping = true;
            if (wipe != null) yield return wipe.Play(party.Leader);
            wiping = false;
            Travel(SceneTravel.AfterlifeScene, revive: true);
        }

        /// <summary>sceneName へ移る。revive なら読み込む前（暗転の間）に全員を全快にして起こす。移っている途中なら何もしない。</summary>
        public void Travel(string sceneName, bool revive = false)
        {
            if (traveling || string.IsNullOrEmpty(sceneName)) return;

            traveling = true;
            SceneTravel.Traveling = true;
            wakeOnArrival = revive;
            rebirthOnArrival = IsRebirth(sceneName);
            StartCoroutine(LeaveFor(sceneName, revive));
        }

        /// <summary>死後の場所から地上へ戻る（転生の渦に入った）か。</summary>
        private static bool IsRebirth(string sceneName) =>
            sceneName == SceneTravel.SurfaceScene && SceneManager.GetActiveScene().name == SceneTravel.AfterlifeScene;

        /// <summary>音を鳴らして暗転してから読み込む。明転は着いたシーンで並べ終えてから（<see cref="Arrive"/>）。転生のときは白く飛ばしてから、その裏で暗転する。</summary>
        private IEnumerator LeaveFor(string sceneName, bool revive)
        {
            if (rebirthOnArrival && rebirth != null)
            {
                yield return rebirth.Leave();
                yield return LeaveItemsBehind();
                if (fade != null) yield return fade.FadeTo(1f, 0f);
            }
            else
            {
                if (travelSound != null) GameAudio.Instance?.PlaySe(travelSound, travelSoundVolume);
                if (fade != null) yield return fade.FadeTo(1f, fadeOutSeconds);
            }
            if (wipe != null) wipe.Restore();
            if (wake != null && wake.IsPlaying) wake.Finish();

            // 起こすのは読み込む前（暗転の間）。着いたシーンの魂の姿（SoulForm）は読み込んだ時点で攻撃の部品を止めるので、
            // そのあとに起こすと止めた部品がまた動いてしまう。
            if (party != null)
            {
                // 全滅して死後の場所へ。操作していたメンバーのレベルをソウルに変えて、全員 Lv1 に戻す（起こす前に。戻すと HP も満タンになる）。
                if (revive && sceneName == SceneTravel.AfterlifeScene) SoulUpgradeWorld.Harvest(party);
                if (revive) party.ReviveAll();
                else party.LeaveDownedBehind();
            }
            SceneTravel.Load(sceneName);
        }

        /// <summary>
        /// 転生で持ち物を手放す。持ち越せる数があって持ち物があれば、選ぶ画面を出して決めるのを待つ。
        /// 選ぶ画面は UI Toolkit なので、その間は白い幕（uGUI）を隠し、画面が自分の白から出て白へ戻す。
        /// </summary>
        private IEnumerator LeaveItemsBehind()
        {
            var keep = new HashSet<ItemInstance>();
            int limit = MetaUpgrades.CarryOverLimit;
            if (limit > 0 && CarryOverPicker != null && HasAnyItem())
            {
                rebirth.WhiteVisible = false;
                yield return CarryOverPicker.Pick(party, limit, keep);
                rebirth.WhiteVisible = true;
            }
            if (soulUpgrades != null) soulUpgrades.DiscardItems(keep);
        }

        private bool HasAnyItem()
        {
            if (party == null) return false;
            if (HasItem(party.Bag)) return true;
            foreach (PartyMember member in party.Members)
            {
                if (member != null && member.Inventory != null && HasItem(member.Inventory.Inventory)) return true;
            }
            return false;
        }

        private static bool HasItem(Inventory items)
        {
            if (items == null) return false;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null) return true;
            }
            return false;
        }

        private bool IsWiped()
        {
            var members = party.Members;
            if (members.Count == 0) return false;
            return members.All(m => m == null || m.Health == null || m.Health.IsDead);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;

            DisableSceneCameras(scene);
            EnsureFloorNavMesh(scene);
            StartCoroutine(Arrive(scene));
        }

        /// <summary>シーン側のカメラ（階の俯瞰の確認用など）を止める。こちらの Main Camera で見るので、MainCamera のタグや AudioListener がぶつからないように。</summary>
        private static void DisableSceneCameras(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true)) camera.gameObject.SetActive(false);
                foreach (AudioListener listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
        }

        /// <summary>階のシーンに NavMesh を焼く役と、部屋に敵を出す役（<see cref="RoomEnemySpawner"/>）が無ければ付ける。NavMesh は後ろの仲間が歩くのに要る。生成（FloorBootstrap.Start）はこのあとなので、焼き上がりで並ぶ。</summary>
        private static void EnsureFloorNavMesh(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (FloorBuilder builder in root.GetComponentsInChildren<FloorBuilder>(true))
                {
                    if (!builder.TryGetComponent(out FloorNavMesh _)) builder.gameObject.AddComponent<FloorNavMesh>();
                    if (!builder.TryGetComponent(out RoomEnemySpawner _)) builder.gameObject.AddComponent<RoomEnemySpawner>();
                }
            }
        }

        private IEnumerator Arrive(Scene scene)
        {
            // 門や階段が頼んだ立ち位置（地上の城の門の前など）。無ければ既定のスポーン地点。
            string point = SceneTravel.TakeArrivalPoint();
            PlayerSpawnPoint spawn = null;
            float until = Time.unscaledTime + spawnWaitTimeout;
            while ((spawn = PlayerSpawnPoint.In(scene, point)) == null && Time.unscaledTime < until) yield return null;

            if (spawn == null) Debug.LogWarning($"{scene.name} にスポーン地点（PlayerSpawnPoint）が無いので、パーティーはその場に居る", this);

            if (party != null)
            {
                if (spawn != null) party.PlaceLeaderAt(spawn.transform);

                // 転生なら連れて行ける人数に合わせてから並べる。
                if (soulUpgrades != null) soulUpgrades.OnArrived(rebirthOnArrival);

                // カメラが前のシーンの位置から滑ってこないように。
                foreach (CinemachineCamera camera in GetComponentsInChildren<CinemachineCamera>(true)) camera.PreviousStateIsValid = false;

                // NavMesh がまだ無ければ並べずに戻り、焼き上がり（FloorNavMesh.Baked）で並ぶ。
                party.Regroup();
            }

            traveling = false;
            SceneTravel.Traveling = false;

            if (rebirthOnArrival && rebirth != null)
            {
                // 白で覆ったまま裏の暗転を外し、白い光の中から地上を見せる。
                rebirthOnArrival = false;
                if (fade != null) yield return fade.FadeTo(0f, 0f);
                yield return rebirth.Arrive();
                yield break;
            }

            if (wakeOnArrival && wake != null)
            {
                // まぶたを閉じた姿にしてから暗転を外し、瞬きしながら目を開ける。
                wakeOnArrival = false;
                wake.Close();
                if (fade != null) yield return fade.FadeTo(0f, 0f);
                yield return wake.Play();
                yield break;
            }

            if (fade != null) yield return fade.FadeTo(0f, fadeInSeconds);
        }
    }
}
