using System;
using System.Collections;
using System.Linq;
using TpsDungeon.Map.Runtime;
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
    /// - 全員が倒れたら少し待って地上へ戻し、全快にする（レベルや持ち物を戻す死に戻りの処理はまだ無い）。
    /// - 地上へ戻ったとき、シーンに置いてある 2 つ目は、子が起きる前に自分ごと消える（実行順を一番先にしてある）。
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Game Flow")]
    public sealed class GameFlow : MonoBehaviour
    {
        [SerializeField, Tooltip("抱えるパーティー。未設定なら子から探す。")]
        private Party party;

        [SerializeField, Min(0f), Tooltip("全員が倒れてから地上へ戻すまでの秒数。")]
        private float wipeDelay = 2.5f;

        [SerializeField, Min(0f), Tooltip("読み込んだシーンにスポーン地点ができるのを待つ秒数。過ぎたらその場のまま並び直す。")]
        private float spawnWaitTimeout = 5f;

        private Action<string> travelHandler;
        private bool traveling;
        private bool reviveOnArrival;
        private float wipedAt = -1f;

        /// <summary>今シーンをまたいで残っているもの。地上を通らずに始めたシーンでは null。</summary>
        public static GameFlow Instance { get; private set; }

        /// <summary>シーンを移っている途中か（読み込みからスポーン地点へ並べ終えるまで）。</summary>
        public bool IsTraveling => traveling;

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
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (traveling || party == null) return;

            if (!IsWiped())
            {
                wipedAt = -1f;
                return;
            }

            if (wipedAt < 0f) wipedAt = Time.time;
            if (Time.time - wipedAt >= wipeDelay) Travel(SceneTravel.SurfaceScene, revive: true);
        }

        /// <summary>sceneName へ移る。revive なら着いたときに全員を全快にする。移っている途中なら何もしない。</summary>
        public void Travel(string sceneName, bool revive = false)
        {
            if (traveling || string.IsNullOrEmpty(sceneName)) return;

            traveling = true;
            reviveOnArrival = revive;
            wipedAt = -1f;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
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

        /// <summary>階のシーンに NavMesh を焼く役が無ければ付ける。後ろの仲間が歩くのに要る。生成（FloorBootstrap.Start）はこのあとなので、焼き上がりで並ぶ。</summary>
        private static void EnsureFloorNavMesh(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (FloorBuilder builder in root.GetComponentsInChildren<FloorBuilder>(true))
                {
                    if (!builder.TryGetComponent(out FloorNavMesh _)) builder.gameObject.AddComponent<FloorNavMesh>();
                }
            }
        }

        private IEnumerator Arrive(Scene scene)
        {
            PlayerSpawnPoint spawn = null;
            float until = Time.unscaledTime + spawnWaitTimeout;
            while ((spawn = PlayerSpawnPoint.In(scene)) == null && Time.unscaledTime < until) yield return null;

            if (spawn == null) Debug.LogWarning($"{scene.name} にスポーン地点（PlayerSpawnPoint）が無いので、パーティーはその場に居る", this);

            if (party != null)
            {
                if (reviveOnArrival)
                {
                    foreach (PartyMember member in party.Members)
                    {
                        if (member != null && member.Health != null) member.Health.SetCurrent(member.Health.MaxHp);
                    }
                }

                PartyMember leader = party.Leader;
                if (leader != null && spawn != null)
                {
                    Transform at = spawn.transform;
                    leader.PlaceAt(at.position, Quaternion.Euler(0f, at.eulerAngles.y, 0f));
                    if (leader.Motor != null)
                    {
                        leader.Motor.CameraYaw = at.eulerAngles.y;
                        leader.Motor.CameraPitch = 0f;
                    }
                }

                // カメラが前のシーンの位置から滑ってこないように。
                foreach (CinemachineCamera camera in GetComponentsInChildren<CinemachineCamera>(true)) camera.PreviousStateIsValid = false;

                // NavMesh がまだ無ければ並べずに戻り、焼き上がり（FloorNavMesh.Baked）で並ぶ。
                party.Regroup();
            }

            reviveOnArrival = false;
            traveling = false;
        }
    }
}
