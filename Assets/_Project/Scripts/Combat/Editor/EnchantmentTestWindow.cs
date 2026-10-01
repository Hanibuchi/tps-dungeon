using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TpsDungeon.Enemies;
using TpsDungeon.Items;
using TpsDungeon.Player;
using TpsDungeon.Progression;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TpsDungeon.Combat.Editor
{
    /// <summary>
    /// 武器とエンチャントの組み合わせを手早く試す窓。
    ///   1. 武器を選び、その武器種に付けられるエンチャントの個数を ± で決める（ランダムに振ることもできる）
    ///   2. 数値の見込み（段ごとの 1 撃・DPS・範囲・叩きつけの数や追撃のダメージ、弓の矢の数や雨の刻みなど）がその場で出る
    ///   3. Play 中は「持たせる」で、選んでいるホットバーの枠へその武器を入れる（個数を変えたら自動で持たせ直せる）
    ///   4. 前方に硬い的を並べ、当てたダメージを本撃・爆発・追撃・雨に分けて記録し、実測の DPS を出す（近接も遠距離も）
    /// ゲーム側には何も足さない（的と持ち物は Play を止めれば消える）。
    /// </summary>
    public sealed class EnchantmentTestWindow : EditorWindow
    {
        private const string MenuPath = "Tools/TPS Dungeon/Player/エンチャントの試験";
        private const string DefaultDummyPath = "Assets/_Project/Prefabs/Enemies/Blob_GreenBlob.prefab";
        private const string DummyRootName = "[エンチャント試験の的]";
        private const int MaxRecordLines = 14;
        private const int MaxRecords = 2000;

        private enum DummyLayout
        {
            前に一列,
            横一列,
            囲む,
        }

        [SerializeField] private WeaponDefinition weapon;
        [SerializeField] private bool autoEquip = true;
        [SerializeField] private GameObject dummyPrefab;
        [SerializeField] private DummyLayout layout = DummyLayout.前に一列;
        [SerializeField] private int dummyCount = 3;
        [SerializeField] private float dummyDistance = 2f;
        [SerializeField] private float dummySpacing = 1.5f;
        [SerializeField] private int dummyHp = 99999;
        [SerializeField] private bool dummyImmortal;
        [SerializeField] private bool logToConsole;

        // 武器種ごとの個数（並びは武器種の AllowedEnchantments と同じ）。武器を替えて戻っても残す。
        private readonly Dictionary<WeaponTypeDefinition, int[]> counts = new Dictionary<WeaponTypeDefinition, int[]>();
        private readonly List<MeleeHitRecord> records = new List<MeleeHitRecord>();
        private readonly List<float> recordTimes = new List<float>();
        private List<WeaponDefinition> weapons;
        private MeleeAttacker hooked;
        private RangedAttacker hookedRanged;
        private Vector2 scroll;

        [MenuItem(MenuPath)]
        public static void Open() => GetWindow<EnchantmentTestWindow>("エンチャントの試験");

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (dummyPrefab == null) dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultDummyPath);
            LoadWeapons();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Unhook();
        }

        private void OnFocus() => LoadWeapons();

        private void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) Unhook();
            Repaint();
        }

        // Play 中は的の HP や記録が動くので、ときどき描き直す。
        private void OnInspectorUpdate()
        {
            if (!EditorApplication.isPlaying) return;

            Hook();
            Repaint();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawWeapon();
            WeaponTypeDefinition type = weapon != null ? weapon.WeaponType : null;
            if (type != null)
            {
                EditorGUILayout.Space();
                DrawEnchantments(type);
                EditorGUILayout.Space();
                DrawPreview(type);
            }

            EditorGUILayout.Space();
            DrawPlay();
            EditorGUILayout.EndScrollView();
        }

        // ---- 武器 ----

        private void LoadWeapons()
        {
            weapons = AssetDatabase.FindAssets("t:" + nameof(WeaponDefinition))
                .Select(guid => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(w => w != null && w.WeaponType != null && (w.WeaponType.IsMelee || w.WeaponType.IsRanged)
                            && w.WeaponType.AllowedEnchantments.Count > 0)
                .OrderBy(w => w.WeaponType.Id).ThenBy(w => w.Rank).ThenBy(w => w.DisplayName)
                .ToList();
            if (weapon == null && weapons.Count > 0) weapon = weapons[0];
        }

        private void DrawWeapon()
        {
            EditorGUILayout.LabelField("武器", EditorStyles.boldLabel);
            if (weapons == null || weapons.Count == 0)
            {
                EditorGUILayout.HelpBox("エンチャントの付く武器が無い（「プレースホルダの武器を生成」で作れる）。", MessageType.Info);
                return;
            }

            string[] labels = weapons.Select(w => $"{w.WeaponType.Id} {w.WeaponType.DisplayName}/{w.DisplayName}（{WeaponRanks.Label(w.Rank)}・{w.Strength:0.#}）").ToArray();
            int index = Mathf.Max(0, weapons.IndexOf(weapon));
            int chosen = EditorGUILayout.Popup(index, labels);
            if (chosen != index || weapon == null)
            {
                weapon = weapons[chosen];
                if (autoEquip) EquipIfPlaying();
            }
        }

        // ---- エンチャント ----

        private int[] CountsOf(WeaponTypeDefinition type)
        {
            int size = type.AllowedEnchantments.Count;
            if (!counts.TryGetValue(type, out int[] values) || values.Length != size)
            {
                values = new int[size];
                counts[type] = values;
            }

            return values;
        }

        private void DrawEnchantments(WeaponTypeDefinition type)
        {
            EditorGUILayout.LabelField($"エンチャント（{type.DisplayName} に付けられるもの）", EditorStyles.boldLabel);
            int[] values = CountsOf(type);
            IReadOnlyList<EnchantmentDefinition> allowed = type.AllowedEnchantments;
            bool changed = false;

            for (int i = 0; i < allowed.Count; i++)
            {
                EnchantmentDefinition definition = allowed[i];
                if (definition == null) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(new GUIContent(definition.DisplayName, definition.Description), GUILayout.Width(110));
                    using (new EditorGUI.DisabledScope(values[i] <= 0))
                    {
                        if (GUILayout.Button("−", GUILayout.Width(24))) { values[i]--; changed = true; }
                    }

                    int typed = EditorGUILayout.IntField(values[i], GUILayout.Width(36));
                    if (typed != values[i]) { values[i] = Mathf.Max(0, typed); changed = true; }
                    if (GUILayout.Button("+", GUILayout.Width(24))) { values[i]++; changed = true; }
                    EditorGUILayout.LabelField(definition.Description, EditorStyles.miniLabel);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全部 0"))
                {
                    Array.Clear(values, 0, values.Length);
                    changed = true;
                }

                if (GUILayout.Button("各 1"))
                {
                    for (int i = 0; i < values.Length; i++) values[i] = 1;
                    changed = true;
                }

                if (GUILayout.Button(new GUIContent("ランダムに振る", "拾ったときと同じ確率で振る。")))
                {
                    RollRandom(type, values);
                    changed = true;
                }
            }

            if (changed && autoEquip) EquipIfPlaying();
        }

        private void RollRandom(WeaponTypeDefinition type, int[] values)
        {
            Array.Clear(values, 0, values.Length);
            ItemInstance rolled = ItemInstance.Create(weapon, new System.Random());
            IReadOnlyList<EnchantmentDefinition> allowed = type.AllowedEnchantments;
            foreach (EnchantmentStack stack in rolled.Enchantments)
            {
                for (int i = 0; i < allowed.Count; i++)
                {
                    if (allowed[i] != stack.Definition) continue;
                    values[i] += stack.Count;
                    break;
                }
            }
        }

        private List<EnchantmentDefinition> Chosen(WeaponTypeDefinition type)
        {
            var result = new List<EnchantmentDefinition>();
            int[] values = CountsOf(type);
            IReadOnlyList<EnchantmentDefinition> allowed = type.AllowedEnchantments;
            for (int i = 0; i < allowed.Count; i++)
            {
                if (allowed[i] == null) continue;
                for (int k = 0; k < values[i]; k++) result.Add(allowed[i]);
            }

            return result;
        }

        // ---- 数値の見込み ----

        private void DrawPreview(WeaponTypeDefinition type)
        {
            EditorGUILayout.LabelField("数値の見込み", EditorStyles.boldLabel);

            MeleeAttacker attacker = EditorApplication.isPlaying ? FindAttacker() : null;
            CharacterProgression progression = attacker != null ? attacker.GetComponent<CharacterProgression>() : null;
            float characterAttack = progression != null ? progression.BaseAttack : 0f;

            ProgressionModifiers modifiers = EditorApplication.isPlaying && PartyProgression.Current != null ? PartyProgression.Current.Modifiers : null;
            Func<float, float> critChance = null;
            Func<float, float> critMultiplier = null;
            if (modifiers != null)
            {
                critChance = x => (float)modifiers.CritChance.Apply(x);
                critMultiplier = x => (float)modifiers.CritMultiplier.Apply(x);
            }

            EnchantmentTotals totals = new ItemInstance(weapon, Chosen(type)).EnchantmentTotals();
            if (type.IsRanged)
            {
                RangedWeaponStats ranged = weapon.ComputeRangedStats(totals, characterAttack, critChance, critMultiplier);
                EditorGUILayout.HelpBox(RangedPreviewText(type, ranged, characterAttack, attacker != null), MessageType.None);
                return;
            }

            MeleeWeaponStats stats = weapon.ComputeMeleeStats(totals, characterAttack, critChance, critMultiplier);
            EditorGUILayout.HelpBox(PreviewText(type, stats, characterAttack, attacker != null), MessageType.None);
        }

        private string PreviewText(WeaponTypeDefinition type, MeleeWeaponStats stats, float characterAttack, bool fromPlayer)
        {
            var text = new StringBuilder();
            var hits = new List<int>();
            for (int i = 0; i < stats.StepCount; i++) hits.Add(stats.HitDamage(i));

            text.AppendLine(fromPlayer
                ? $"基礎攻撃力 {characterAttack:0.#}（プレイヤーの今の値）を足している"
                : "基礎攻撃力 0 として出している（Play 中はプレイヤーの今の値を足す）");
            text.AppendLine($"1 撃（段ごと）: {string.Join(" / ", hits)}　1 周 {stats.CycleDuration:0.00} 秒");
            text.AppendLine($"平均 DPS: {stats.AverageDps:0.0}（クリティカル込み。爆発・増えた叩きつけ・追撃・コンボボーナスは含めない）");
            if (stats.ComboBonus > 0f)
            {
                // 表示の COMBO の数 N の 1 撃には、続けて当てた N - 1 段ぶんが乗る。
                string Example(int shown) => string.Join(" / ", Enumerable.Range(0, stats.StepCount).Select(i => stats.HitDamage(i, shown - 1)));
                text.AppendLine($"コンボボーナス: 続けて当てた段 1 つごとに +{stats.ComboBonus:P0}（上限なし。空振りか手を止めると途切れる）");
                text.AppendLine($"  5 COMBO のとき {Example(5)}　10 COMBO のとき {Example(10)}");
            }
            text.AppendLine($"クリティカル: {stats.CritChance:P0} で ×{stats.CritMultiplier:0.##}");
            text.AppendLine($"攻撃速度 ×{stats.AttackSpeed:0.##}　範囲 ×{stats.HitboxScale:0.##}　ノックバック +{stats.KnockbackBonus:0.#} m/s　スタン判定 ×{stats.ReactionScale:0.##}");

            if (stats.ExplosionRatio > 0f)
                text.AppendLine($"爆発: 1 撃ごとに 半径 {stats.ExplosionRadius:0.#} m へ {string.Join(" / ", hits.Select(stats.ExplosionDamage))}");
            if (stats.DropRateBonus > 0f)
                text.AppendLine($"ドロップ率 +{stats.DropRateBonus:P0}（ドロップの仕組みはまだ読まない）");

            IReadOnlyList<MeleeComboStep> steps = type.ComboSteps;
            for (int i = 0; i < steps.Count; i++)
            {
                MeleeComboStep step = steps[i];
                string head = steps.Count > 1 ? $"{i + 1} 段目の" : string.Empty;
                if (step.motion == MeleeStepMotion.Lunge)
                {
                    text.AppendLine($"{head}ダッシュ: {step.lungeDistance * stats.LungeTimeScale:0.##} m を {step.lungeDuration * stats.LungeTimeScale:0.##} 秒 × {stats.FollowUpCount + 1} 回"
                                    + (type.LungePassesThroughEnemies ? "（敵をすり抜ける）" : "（敵で止まる）"));
                }
                else if (step.motion == MeleeStepMotion.Slam)
                {
                    int hit = stats.HitDamage(i);
                    text.AppendLine($"{head}着弾: 半径 {step.slamRadius * stats.HitboxScale:0.##} m");
                    text.AppendLine($"  叩きつけ: {stats.ExtraSlamCount + 1} 個 × {hit}（{type.SlamSpreadAngle:0.#}° ずつ扇状に）");
                    text.AppendLine($"  追撃: {stats.FollowUpCount} 回 × 叩きつけ {stats.ExtraSlamCount + 1} 個 × {hit}（{type.FollowUpSpacing:0.#} m ずつ先へ）");
                }
            }

            return text.ToString().TrimEnd();
        }

        private static string RangedPreviewText(WeaponTypeDefinition type, RangedWeaponStats stats, float characterAttack, bool fromPlayer)
        {
            var text = new StringBuilder();
            text.AppendLine(fromPlayer
                ? $"基礎攻撃力 {characterAttack:0.#}（プレイヤーの今の値）を足している"
                : "基礎攻撃力 0 として出している（Play 中はプレイヤーの今の値を足す）");

            int count = stats.ExtraProjectiles + 1;
            int volleys = stats.MultishotCount + 1;
            if (type.RangedKind == RangedAttackKind.Rain)
            {
                text.AppendLine($"雨の 1 刻み: {stats.RainTickDamage} × {stats.RainTickCount} 回（{type.RainTickInterval:0.##} 秒ごと）"
                                + $"＝ずっと居れば {stats.RainTickDamage * stats.RainTickCount}　撃つ間隔 {stats.FireInterval:0.00} 秒");
                text.AppendLine($"平均 DPS: {stats.AverageDps:0.0}（クリティカル込み。持続時間の延び・数・多重・重なりは含めない）");
                text.AppendLine($"雨: 半径 {type.RainRadius * stats.SizeScale:0.##} m を {count} か所（狙った所に 1 つ、残りは {type.RainScatterMin * stats.SizeScale:0.#}〜{type.RainScatterMax * stats.SizeScale:0.#} m 離れたランダムな所）"
                                + $" × {volleys} 回（{type.RainRepeatInterval:0.##} 秒ずつ遅れて同じ所に）");
                text.AppendLine($"降り始めまで {type.RainDelay / stats.ProjectileSpeedScale:0.##} 秒　続く時間 ×{stats.DurationScale:0.##}");
            }
            else
            {
                text.AppendLine($"1 発: {stats.ShotDamage}　撃つ間隔 {stats.FireInterval:0.00} 秒");
                text.AppendLine($"平均 DPS: {stats.AverageDps:0.0}（クリティカル込み。数・多重・爆発は含めない）");
                text.AppendLine($"矢: {count} 本（1 本は照準へ、残りは {type.VolleySpreadAngle:0.#}° ずつ右左交互に） × {volleys} 回（{type.MultishotInterval:0.##} 秒ずつ遅れて）"
                                + $"　速さ {type.ProjectileSpeed * stats.ProjectileSpeedScale:0.#} m/s");
                text.AppendLine($"貫通 {stats.PierceCount} 体　ホーミング {(stats.Homing ? "あり" : "なし")}");
                if (stats.ExplosionRatio > 0f)
                    text.AppendLine($"爆発: 1 本ごとに 半径 {stats.ExplosionRadius:0.#} m へ {stats.ExplosionDamage(stats.ShotDamage)}");
            }

            text.AppendLine($"クリティカル: {stats.CritChance:P0} で ×{stats.CritMultiplier:0.##}");
            text.AppendLine($"攻撃速度 ×{stats.AttackSpeed:0.##}　ノックバック +{stats.KnockbackBonus:0.#} m/s　スタン判定 ×{stats.ReactionScale:0.##}");
            if (stats.DropRateBonus > 0f)
                text.AppendLine($"ドロップ率 +{stats.DropRateBonus:P0}（ドロップの仕組みはまだ読まない）");
            return text.ToString().TrimEnd();
        }

        // ---- Play 中 ----

        private void DrawPlay()
        {
            EditorGUILayout.LabelField("Play 中に試す", EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play すると、選んだ武器を持たせる・的を並べる・当てたダメージを記録する、ができる。", MessageType.Info);
                return;
            }

            MeleeAttacker attacker = FindAttacker();
            if (attacker == null)
            {
                EditorGUILayout.HelpBox("シーンに MeleeAttacker（プレイヤー）が無い。", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(weapon == null))
                {
                    if (GUILayout.Button("選んでいる枠に持たせる", GUILayout.Height(24))) Equip(attacker);
                }

                autoEquip = GUILayout.Toggle(autoEquip, "変えたらすぐ持たせ直す");
            }

            RangedAttacker ranged = attacker.GetComponent<RangedAttacker>();
            WeaponDefinition holding = ranged != null && ranged.HeldWeapon != null ? ranged.HeldWeapon : attacker.HeldWeapon;
            EditorGUILayout.LabelField("手に持っている", holding != null ? holding.DisplayName : "なし");

            EditorGUILayout.Space();
            DrawDummies(attacker);
            EditorGUILayout.Space();
            DrawRecords(attacker);
        }

        private void EquipIfPlaying()
        {
            if (!EditorApplication.isPlaying) return;

            MeleeAttacker attacker = FindAttacker();
            if (attacker != null) Equip(attacker);
        }

        /// <summary>選んでいるホットバーの枠へ、今の個数のエンチャントを付けた武器を入れる（入っていた物は捨てる）。</summary>
        private void Equip(MeleeAttacker attacker)
        {
            if (weapon == null) return;

            var inventory = attacker.GetComponent<PlayerInventory>();
            var hotbar = attacker.GetComponent<PlayerHotbar>();
            if (inventory == null || hotbar == null)
            {
                Debug.LogWarning("PlayerInventory か PlayerHotbar が無いので持たせられない", attacker);
                return;
            }

            inventory.Inventory.Place(hotbar.SelectedIndex, new ItemInstance(weapon, Chosen(weapon.WeaponType)));
        }

        // ---- 的 ----

        private void DrawDummies(MeleeAttacker attacker)
        {
            EditorGUILayout.LabelField("的", EditorStyles.miniBoldLabel);
            dummyPrefab = (GameObject)EditorGUILayout.ObjectField("敵のプレハブ", dummyPrefab, typeof(GameObject), false);
            layout = (DummyLayout)EditorGUILayout.EnumPopup("並べ方", layout);
            dummyCount = Mathf.Clamp(EditorGUILayout.IntField("数", dummyCount), 1, 20);
            dummyDistance = Mathf.Max(0.5f, EditorGUILayout.FloatField(layout == DummyLayout.囲む ? "半径 (m)" : "最初の的まで (m)", dummyDistance));
            if (layout != DummyLayout.囲む) dummySpacing = Mathf.Max(0.3f, EditorGUILayout.FloatField("間隔 (m)", dummySpacing));
            dummyHp = Mathf.Max(1, EditorGUILayout.IntField("HP", dummyHp));
            bool immortal = EditorGUILayout.Toggle(new GUIContent("死なない", "HP が尽きる一撃で満タンに戻る。HP を低くしてスタン・気絶・ノックバックを試すときに。"), dummyImmortal);
            if (immortal != dummyImmortal)
            {
                dummyImmortal = immortal;
                ForEachDummy(d => d.SetImmortal(immortal));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(dummyPrefab == null || dummyPrefab.GetComponentInChildren<EnemyHealth>() == null))
                {
                    if (GUILayout.Button("プレイヤーの前に並べる")) PlaceDummies(attacker.transform);
                }

                if (GUILayout.Button("HP を戻す")) ForEachDummy(d => d.SetMaxHp(dummyHp));
                if (GUILayout.Button("片付ける")) ClearDummies();
            }

            var alive = new List<string>();
            ForEachDummy(d => alive.Add($"{d.name} {d.CurrentHp}/{d.MaxHp}"));
            if (alive.Count > 0) EditorGUILayout.LabelField(string.Join("　", alive), EditorStyles.wordWrappedMiniLabel);
        }

        private void PlaceDummies(Transform player)
        {
            ClearDummies();
            var root = new GameObject(DummyRootName);

            Vector3 origin = player.position;
            Vector3 forward = player.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            for (int i = 0; i < dummyCount; i++)
            {
                Vector3 position = layout switch
                {
                    DummyLayout.横一列 => origin + forward * dummyDistance + right * ((i - (dummyCount - 1) * 0.5f) * dummySpacing),
                    DummyLayout.囲む => origin + Quaternion.AngleAxis(360f * i / dummyCount, Vector3.up) * forward * dummyDistance,
                    _ => origin + forward * (dummyDistance + i * dummySpacing),
                };

                Vector3 toPlayer = origin - position;
                toPlayer.y = 0f;
                Quaternion facing = toPlayer.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
                GameObject dummy = Instantiate(dummyPrefab, position, facing, root.transform);
                dummy.name = $"的{i + 1}";
                foreach (EnemyHealth health in dummy.GetComponentsInChildren<EnemyHealth>())
                {
                    health.SetMaxHp(dummyHp);
                    health.SetImmortal(dummyImmortal);
                }
            }
        }

        private static void ClearDummies()
        {
            GameObject root = GameObject.Find(DummyRootName);
            if (root != null) Destroy(root);
        }

        private static void ForEachDummy(Action<EnemyHealth> action)
        {
            GameObject root = GameObject.Find(DummyRootName);
            if (root == null) return;

            foreach (EnemyHealth health in root.GetComponentsInChildren<EnemyHealth>()) action(health);
        }

        // ---- 記録 ----

        private void DrawRecords(MeleeAttacker attacker)
        {
            EditorGUILayout.LabelField("当てたダメージ", EditorStyles.miniBoldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("記録を消す")) ClearRecords();
                bool log = GUILayout.Toggle(logToConsole, "コンソールにも出す");
                if (log != logToConsole || attacker.LogHits != log)
                {
                    logToConsole = log;
                    attacker.LogHits = log;
                    if (hookedRanged != null) hookedRanged.LogHits = log;
                }
            }

            if (records.Count == 0)
            {
                EditorGUILayout.LabelField("まだ当てていない（どの敵に当てても記録する）。", EditorStyles.miniLabel);
                return;
            }

            var summary = new StringBuilder();
            foreach (MeleeHitKind kind in Enum.GetValues(typeof(MeleeHitKind)))
            {
                var ofKind = records.Where(r => r.Kind == kind).ToList();
                if (ofKind.Count == 0) continue;
                int crits = ofKind.Count(r => r.IsCritical);
                summary.AppendLine($"{KindLabel(kind)}: {ofKind.Count} 回 合計 {ofKind.Sum(r => r.Dealt)}"
                                   + (crits > 0 ? $"（クリティカル {crits} 回）" : string.Empty));
            }

            int total = records.Sum(r => r.Dealt);
            float span = recordTimes[recordTimes.Count - 1] - recordTimes[0];
            summary.Append($"全部: {total}");
            if (span > 0.05f) summary.Append($"　{span:0.00} 秒で実測 {total / span:0.0} DPS（最初と最後の命中の間）");
            EditorGUILayout.HelpBox(summary.ToString(), MessageType.None);

            var lines = new StringBuilder();
            bool rangedHeld = hookedRanged != null && hookedRanged.HeldWeapon != null;
            for (int i = records.Count - 1; i >= 0 && i >= records.Count - MaxRecordLines; i--)
            {
                MeleeHitRecord r = records[i];
                string target = r.Enemy != null ? r.Enemy.name : "（消えた敵）";
                string step = rangedHeld ? string.Empty : $"{r.Step + 1} 段目 ";
                lines.AppendLine($"{recordTimes[i] - recordTimes[0],6:0.00}s  {step}{KindLabel(r.Kind)} → {target}: {r.Dealt}"
                                 + (r.Dealt != r.Damage ? $"（{r.Damage} のうち）" : string.Empty)
                                 + (r.IsCritical ? "  クリティカル" : string.Empty)
                                 + (r.Combo > 0 ? $"  {r.Combo} COMBO" : string.Empty));
            }

            EditorGUILayout.LabelField(lines.ToString().TrimEnd(), EditorStyles.wordWrappedMiniLabel);
        }

        private static string KindLabel(MeleeHitKind kind) => kind switch
        {
            MeleeHitKind.Explosion => "爆発",
            MeleeHitKind.FollowUp => "追撃",
            MeleeHitKind.Tick => "雨",
            _ => "本撃",
        };

        private void ClearRecords()
        {
            records.Clear();
            recordTimes.Clear();
        }

        private void Hook()
        {
            MeleeAttacker attacker = FindAttacker();
            if (attacker == hooked) return;

            Unhook();
            hooked = attacker;
            if (hooked == null) return;

            hooked.Dealt += OnDealt;
            hooked.LogHits = logToConsole;
            hookedRanged = hooked.GetComponent<RangedAttacker>();
            if (hookedRanged == null) return;

            hookedRanged.Dealt += OnDealt;
            hookedRanged.LogHits = logToConsole;
        }

        private void Unhook()
        {
            if (hooked != null) hooked.Dealt -= OnDealt;
            if (hookedRanged != null) hookedRanged.Dealt -= OnDealt;
            hooked = null;
            hookedRanged = null;
            ClearRecords();
        }

        private void OnDealt(MeleeHitRecord record)
        {
            // 長く試し続けても重くならないよう、古い記録から捨てる。
            if (records.Count >= MaxRecords)
            {
                records.RemoveRange(0, MaxRecords / 2);
                recordTimes.RemoveRange(0, MaxRecords / 2);
            }

            records.Add(record);
            recordTimes.Add(Time.time);
            Repaint();
        }

        private static MeleeAttacker FindAttacker() => Object.FindAnyObjectByType<MeleeAttacker>();
    }
}
