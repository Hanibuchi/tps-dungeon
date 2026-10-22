using System.Collections.Generic;
using System.Linq;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Party;
using TpsDungeon.Player;
using UnityEditor;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// 敵の挙動を手早く試す窓。Play 中に使う（Player_Debug など、パーティーの居るシーンで）。
    ///   1. 敵を出す … 敵のプレハブを選び、先頭の前の NavMesh の上に出す（ほかの敵を消してから 1 体だけ、もできる）
    ///   2. 場を整える … 仲間に戦わせない・仲間の HP を減らさない・敵を倒れなくする・時間をゆっくりにする
    ///   3. 敵の一覧 … 今していること（溜め・飛ぶ・導火…）・狙っている相手・HP・次の攻撃までを見ながら、
    ///      1 体ずつ止める・動かす・スタン・気絶・倒す・それだけ残す
    ///   4. 被ダメージの記録 … どの敵がどの仲間に何ダメージ与えたか（敵ごとの合計も）
    /// シーンの表示: Game 画面の敵の頭の上に状態を出せる。シーンビューで敵を選ぶと、見つける距離と攻撃の間合いを円で描く。
    /// ゲーム側には何も残さない（出した敵や表示は Play を止めれば消える）。
    /// </summary>
    public sealed class EnemyBehaviourTestWindow : EditorWindow
    {
        private const string MenuPath = "Tools/TPS Dungeon/デバッグ/敵の挙動の試験";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Enemies";
        private const string SpawnRootName = "[敵の挙動の試験]";
        private const int MaxRecords = 200;
        private const int ShownRecords = 12;

        private struct Record
        {
            public float Time;
            public string Source;
            public string Target;
            public int Amount;
        }

        [SerializeField] private int prefabIndex;
        [SerializeField] private float spawnDistance = 7f;
        [SerializeField] private int spawnCount = 1;
        [SerializeField] private bool clearBeforeSpawn = true;
        [SerializeField] private bool followersPassive = true;
        [SerializeField] private bool partyInvincible = true;
        [SerializeField] private bool enemiesImmortal;
        [SerializeField] private bool showLabels = true;
        [SerializeField] private float timeScale = 1f;

        private List<GameObject> prefabs = new List<GameObject>();
        // IMGUI は Layout と Repaint で同じ数の部品を描かないと崩れるので、一覧は Layout のときだけ取り直す。
        private List<EnemyBrain> shownBrains = new List<EnemyBrain>();
        private readonly List<Record> records = new List<Record>();
        private readonly HashSet<Behaviour> pausedFollowers = new HashSet<Behaviour>();
        private Vector2 scroll;

        [MenuItem(MenuPath, false, 2)]
        public static void Open() => GetWindow<EnemyBehaviourTestWindow>("敵の挙動の試験");

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            LoadPrefabs();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EnemyTarget.Damaged -= OnDamaged;
            if (EditorApplication.isPlaying) Restore();
        }

        private void OnFocus() => LoadPrefabs();

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode) records.Clear();
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EnemyTarget.Damaged -= OnDamaged;
                pausedFollowers.Clear();
            }
            Repaint();
        }

        // Play 中は敵の状態が動くので、ときどき描き直し、決めた場の設定をかけ続ける。
        private void OnInspectorUpdate()
        {
            if (!EditorApplication.isPlaying) return;

            // 敵の攻撃の知らせは Play のたびに聞き手が外れるので、付け直す（二重にはならない）。
            EnemyTarget.Damaged -= OnDamaged;
            EnemyTarget.Damaged += OnDamaged;

            ApplyField();
            Repaint();
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 中に使う。パーティーの居るシーン（Player_Debug など）で Play してから、敵を出して試す。", MessageType.Info);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawSpawn();
            EditorGUILayout.Space();
            DrawField();
            EditorGUILayout.Space();
            DrawEnemies();
            EditorGUILayout.Space();
            DrawRecords();
            EditorGUILayout.EndScrollView();
        }

        // ---- 1. 敵を出す ----

        private void LoadPrefabs()
        {
            prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(p => p != null && p.GetComponent<EnemyBrain>() != null)
                .OrderBy(p => KindOrder(p)).ThenBy(p => p.name)
                .ToList();
            prefabIndex = Mathf.Clamp(prefabIndex, 0, Mathf.Max(0, prefabs.Count - 1));
        }

        private void DrawSpawn()
        {
            EditorGUILayout.LabelField("敵を出す", EditorStyles.boldLabel);
            if (prefabs.Count == 0)
            {
                EditorGUILayout.HelpBox($"{PrefabFolder} に敵のプレハブが無い（「モンスターの Animator とプレハブを作る」で作れる）。", MessageType.Info);
                return;
            }

            prefabIndex = EditorGUILayout.Popup("敵", prefabIndex, prefabs.Select(p => $"{KindLabel(p)}  {p.name}").ToArray());
            spawnDistance = EditorGUILayout.Slider("先頭からの距離 (m)", spawnDistance, 1f, 20f);
            spawnCount = EditorGUILayout.IntSlider("数", spawnCount, 1, 6);
            clearBeforeSpawn = EditorGUILayout.Toggle("ほかの敵を消してから出す", clearBeforeSpawn);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("先頭の前に出す"))
                {
                    Spawn(prefabs[prefabIndex]);
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("敵を全部消す", GUILayout.Width(110)))
                {
                    ClearEnemies(null);
                    GUIUtility.ExitGUI();
                }
            }
        }

        private void Spawn(GameObject prefab)
        {
            Transform leader = Leader();
            if (leader == null)
            {
                ShowNotification(new GUIContent("パーティーの先頭が居ない"));
                return;
            }
            if (clearBeforeSpawn) ClearEnemies(null);

            GameObject root = GameObject.Find(SpawnRootName);
            if (root == null) root = new GameObject(SpawnRootName);
            Vector3 forward = Vector3.ProjectOnPlane(leader.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            GameObject last = null;
            for (int i = 0; i < spawnCount; i++)
            {
                // 2 体目からは横に 1.5 m ずつ、左右交互に並べる。
                float side = (i + 1) / 2 * 1.5f * (i % 2 == 0 ? 1f : -1f);
                Vector3 position = EnemyMotor.OnNavMesh(leader.position + forward * spawnDistance + right * side, 4f);
                Quaternion facing = Quaternion.LookRotation(-forward, Vector3.up);
                last = (GameObject)Instantiate(prefab, position, facing, root.transform);
                last.name = prefab.name;
            }
            if (last != null) Selection.activeGameObject = last;
        }

        private static void ClearEnemies(EnemyBrain keep)
        {
            foreach (EnemyBrain brain in Brains())
            {
                if (brain != keep) Destroy(brain.gameObject);
            }
        }

        // ---- 2. 場を整える ----

        private void DrawField()
        {
            EditorGUILayout.LabelField("場", EditorStyles.boldLabel);
            followersPassive = EditorGUILayout.ToggleLeft("仲間に戦わせない（敵の動きだけを見る）", followersPassive);
            partyInvincible = EditorGUILayout.ToggleLeft("仲間の HP を減らさない（減ったらすぐ戻す。記録には残る）", partyInvincible);
            enemiesImmortal = EditorGUILayout.ToggleLeft("敵を倒れなくする（HP が 0 になったら満タンに戻る）", enemiesImmortal);
            showLabels = EditorGUILayout.ToggleLeft("Game 画面の敵の頭の上に状態を出す", showLabels);
            timeScale = EditorGUILayout.Slider("時間の速さ", timeScale, 0.1f, 1f);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("仲間を全回復")) HealParty();
                if (GUILayout.Button("先頭の前の扉を開ける")) OpenDoorsAhead();
            }
        }

        /// <summary>場の設定を今のシーンにかける。Play 中にときどき呼ぶ（出し直した敵や入れ替わった仲間にも効くように）。</summary>
        private void ApplyField()
        {
            Time.timeScale = timeScale;

            Party.Party party = Party.Party.Current;
            if (party != null)
            {
                foreach (PartyMember member in party.Members)
                {
                    if (member == null) continue;
                    var brain = member.GetComponent<FollowerBrain>();
                    if (brain != null)
                    {
                        // 先頭の間は PartyMember が切っているので、こちらが切ったものだけ戻す。
                        if (followersPassive && brain.enabled)
                        {
                            brain.enabled = false;
                            pausedFollowers.Add(brain);
                        }
                        else if (!followersPassive && pausedFollowers.Remove(brain) && !member.IsLeader)
                        {
                            brain.enabled = true;
                        }
                    }

                    PlayerHealth health = member.Health;
                    if (partyInvincible && health != null && health.CurrentHp < health.MaxHp) health.SetCurrent(health.MaxHp);
                }
            }

            foreach (EnemyBrain brain in Brains())
            {
                var health = brain.GetComponent<EnemyHealth>();
                if (health != null && health.Immortal != enemiesImmortal) health.SetImmortal(enemiesImmortal);
            }

            var root = GameObject.Find(SpawnRootName);
            if (showLabels && root == null) root = new GameObject(SpawnRootName);
            if (root != null)
            {
                var labels = root.GetComponent<EnemyDebugLabels>();
                if (showLabels && labels == null) root.AddComponent<EnemyDebugLabels>();
                else if (!showLabels && labels != null) Destroy(labels);
            }
        }

        /// <summary>止めていた仲間を戻し、時間の速さを戻す（窓を閉じたとき）。</summary>
        private void Restore()
        {
            foreach (Behaviour brain in pausedFollowers)
            {
                var member = brain != null ? brain.GetComponent<PartyMember>() : null;
                if (member != null && !member.IsLeader) brain.enabled = true;
            }
            pausedFollowers.Clear();
            Time.timeScale = 1f;
        }

        private static void HealParty()
        {
            Party.Party party = Party.Party.Current;
            if (party == null) return;
            foreach (PartyMember member in party.Members)
            {
                if (member != null && member.Health != null) member.Health.SetCurrent(member.Health.MaxHp);
            }
        }

        /// <summary>先頭の前 6 m 以内の閉じた扉を開ける（敵の居る部屋と見通しを通すため）。</summary>
        private void OpenDoorsAhead()
        {
            Transform leader = Leader();
            if (leader == null) return;

            int opened = 0;
            foreach (Door door in FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                Vector3 to = door.transform.position - leader.position;
                to.y = 0f;
                if (to.magnitude > 6f || Vector3.Dot(to, leader.forward) < 0f || door.IsOpen) continue;
                door.Interact(leader.gameObject);
                opened++;
            }
            ShowNotification(new GUIContent(opened > 0 ? $"扉を {opened} 枚開けた" : "前に閉じた扉が無い"));
        }

        // ---- 3. 敵の一覧 ----

        private void DrawEnemies()
        {
            EditorGUILayout.LabelField("敵", EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying) return;

            if (Event.current.type == EventType.Layout) shownBrains = Brains().OrderBy(b => b.name).ToList();
            List<EnemyBrain> brains = shownBrains.Where(b => b != null).ToList();
            if (brains.Count == 0)
            {
                EditorGUILayout.LabelField("（居ない）");
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全部止める")) foreach (EnemyBrain b in brains) Pause(b, true);
                if (GUILayout.Button("全部動かす")) foreach (EnemyBrain b in brains) Pause(b, false);
            }

            Transform leader = Leader();
            foreach (EnemyBrain brain in brains)
            {
                var health = brain.GetComponent<EnemyHealth>();
                var reaction = brain.GetComponent<EnemyDamageReaction>();
                bool dead = health != null && health.IsDead;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button($"{brain.name}（{KindLabel(brain.gameObject)}）", EditorStyles.boldLabel))
                        {
                            Selection.activeGameObject = brain.gameObject;
                            EditorGUIUtility.PingObject(brain.gameObject);
                        }
                        GUILayout.FlexibleSpace();
                        if (health != null) GUILayout.Label($"HP {health.CurrentHp}/{health.MaxHp}");
                    }

                    string state = dead ? "倒れた"
                        : reaction != null && reaction.IsFainted ? "気絶"
                        : reaction != null && reaction.IsStunned ? "スタン"
                        : !brain.enabled ? "止めている"
                        : brain.StateLabel;
                    string target = brain.HasTarget
                        ? $"{brain.Target.Name}（{(brain.TargetVisible ? "見えている" : "見失い")}・{Vector3.Distance(brain.transform.position, brain.Target.Position):0.0} m）"
                        : "なし";
                    string distance = leader != null ? $"{Vector3.Distance(brain.transform.position, leader.position):0.0} m" : "-";
                    EditorGUILayout.LabelField($"今: {state}    次の攻撃まで: {brain.CooldownRemaining:0.0}s    1 回: {brain.Damage}");
                    EditorGUILayout.LabelField($"狙い: {target}    先頭から: {distance}");

                    using (new EditorGUI.DisabledScope(dead))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button(brain.enabled ? "止める" : "動かす")) Pause(brain, brain.enabled);
                        if (GUILayout.Button("スタン") && reaction != null) reaction.Apply(DamageReaction.Stun);
                        if (GUILayout.Button("気絶") && reaction != null)
                            reaction.Apply(DamageReaction.Faint, new DamageInfo(1, null, -brain.transform.forward, false, 0f, 1f));
                        if (GUILayout.Button("倒す") && health != null)
                        {
                            health.SetImmortal(false);
                            health.TakeDamage(new DamageInfo(health.CurrentHp, null, -brain.transform.forward, false, 0f, 1f));
                        }
                        if (GUILayout.Button("これだけ残す"))
                        {
                            ClearEnemies(brain);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
        }

        /// <summary>頭を止める。歩きかけの先と向きも消して、その場に立たせる。</summary>
        private static void Pause(EnemyBrain brain, bool pause)
        {
            if (brain == null || brain.GetComponent<EnemyHealth>().IsDead) return;
            brain.enabled = !pause;
            if (!pause) return;
            var motor = brain.GetComponent<EnemyMotor>();
            motor.Destination = null;
            motor.LookAt = null;
            motor.SpeedScale = 1f;
        }

        // ---- 4. 被ダメージの記録 ----

        private void OnDamaged(GameObject source, EnemyTarget target, int amount)
        {
            records.Add(new Record
            {
                Time = Time.time,
                Source = source != null ? source.name : "（不明）",
                Target = target.Character != null ? MemberName(target.Character) : target.Name,
                Amount = amount,
            });
            if (records.Count > MaxRecords) records.RemoveAt(0);
        }

        private void DrawRecords()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("仲間が受けたダメージ", EditorStyles.boldLabel);
                if (GUILayout.Button("消す", GUILayout.Width(50))) records.Clear();
            }
            if (records.Count == 0)
            {
                EditorGUILayout.LabelField("（まだ無い）");
                return;
            }

            foreach (var group in records.GroupBy(r => r.Source))
            {
                int hits = group.Count();
                float span = group.Last().Time - group.First().Time;
                string rate = hits > 1 && span > 0f ? $"・{(hits - 1) / span * 60f:0} 回/分" : "";
                EditorGUILayout.LabelField($"{group.Key}: 合計 {group.Sum(r => r.Amount)}（{hits} 回{rate}）");
            }

            EditorGUILayout.Space(2);
            for (int i = records.Count - 1; i >= Mathf.Max(0, records.Count - ShownRecords); i--)
            {
                Record r = records[i];
                EditorGUILayout.LabelField($"{r.Time,7:0.00}s  {r.Source} → {r.Target}  -{r.Amount}", EditorStyles.miniLabel);
            }
        }

        // ---- 道具 ----

        private static IEnumerable<EnemyBrain> Brains() => FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None);

        private static Transform Leader()
        {
            Party.Party party = Party.Party.Current;
            return party != null && party.Leader != null ? party.Leader.transform : null;
        }

        private static string MemberName(PlayerHealth health)
        {
            var member = health.GetComponent<PartyMember>();
            return member != null && !string.IsNullOrEmpty(member.DisplayName) ? member.DisplayName : health.name;
        }

        /// <summary>敵種（Notion「敵種」の 01〜07）の呼び名。浮くかどうかは EnemyMotor の高さで見分ける。</summary>
        private static string KindLabel(GameObject enemy)
        {
            var brain = enemy.GetComponent<EnemyBrain>();
            var motor = enemy.GetComponent<EnemyMotor>();
            bool floating = motor != null && motor.HoverHeight > 0f;
            return brain switch
            {
                MeleeEnemyBrain _ => floating ? "03 浮遊近距離" : "01 単純近距離",
                LeapEnemyBrain _ => "02 飛びつき",
                BomberEnemyBrain _ => "04 自爆",
                PoisonGasEnemyBrain _ => "05 毒霧",
                RangedEnemyBrain _ => floating ? "06 浮遊遠距離" : "07 遠距離",
                _ => brain != null ? brain.GetType().Name : "?",
            };
        }

        private static string KindOrder(GameObject enemy) => KindLabel(enemy);
    }
}
