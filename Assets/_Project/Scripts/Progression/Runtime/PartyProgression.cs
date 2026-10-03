using System;
using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// パーティ（主人公と仲間）の成長をまとめる。主人公のルートに 1 つ置く。
    /// - 敵を倒したときの経験値を、倍率を掛けて全員に同じ量だけ配る（<see cref="GrantExp"/>）
    /// - 永続アップグレードの補正を持つ（<see cref="Modifiers"/>）
    /// - ゲームオーバーで全員を Lv1 に戻す（<see cref="ResetForNewRun"/>）
    /// - 全員のレベル・経験値・現在 HP を保存・復元する（<see cref="CaptureSaveData"/> / <see cref="RestoreSaveData"/>）
    /// CharacterProgression は有効になると <see cref="Current"/> に自分で登録する。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("TPS Dungeon/Party Progression")]
    public sealed class PartyProgression : MonoBehaviour
    {
        [SerializeField, Tooltip("開始時に保存データを読み込んで、途中から再開する。")]
        private bool autoLoad = true;

        [SerializeField, Tooltip("レベルアップ・ゲームオーバー・終了・非アクティブ化のたびに保存する。")]
        private bool autoSave = true;

        private readonly List<CharacterProgression> members = new List<CharacterProgression>();
        private readonly ProgressionModifiers modifiers = new ProgressionModifiers();

        // 保存データにあるが、まだ登録されていない仲間の分。登録されたときに当てる。捨てずに保存にも書き戻す。
        private readonly Dictionary<string, CharacterProgressSaveData> pending = new Dictionary<string, CharacterProgressSaveData>();

        // GrantExp で配っている最中。何人上がっても保存は最後に 1 回にする。
        private bool granting;
        private bool leveledWhileGranting;

        private float gearExpBonus;

        /// <summary>今動いているパーティ。無ければ null。</summary>
        public static PartyProgression Current { get; private set; }

        public IReadOnlyList<CharacterProgression> Members => members;

        /// <summary>経験値倍率などの補正。永続アップグレードがここに値を入れる。</summary>
        public ProgressionModifiers Modifiers => modifiers;

        /// <summary>
        /// 装備（お守り・盾の経験値）による経験値の上乗せ（0.1 で +10%）。永続アップグレードの倍率に掛け足す。PlayerGear が書き込む。
        /// </summary>
        public float GearExpBonus
        {
            get => gearExpBonus;
            set => gearExpBonus = Mathf.Max(0f, value);
        }

        public bool AutoSave
        {
            get => autoSave;
            set => autoSave = value;
        }

        /// <summary>保存先のファイル。null か空なら ProgressSaveStore.DefaultPath。テストで差し替える。</summary>
        [field: System.NonSerialized]
        public string SavePath { get; set; }

        /// <summary>(自分, 倍率を掛けた後の量)。</summary>
        public event Action<PartyProgression, int> ExpGranted;

        /// <summary>ResetForNewRun で全員が Lv1 に戻った。</summary>
        public event Action<PartyProgression> RunReset;

        /// <summary>CharacterProgression から呼ばれる。パーティがまだ無ければ何もしない（パーティ側が有効になったときに拾う）。</summary>
        public static void RegisterToCurrent(CharacterProgression member)
        {
            if (Current != null) Current.Register(member);
        }

        private void OnEnable()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning($"PartyProgression が 2 つある。後から有効になった {name} を使う。", this);
            }
            Current = this;

            // 先に有効になっていたメンバーを拾う。
            foreach (var member in FindObjectsByType<CharacterProgression>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                Register(member);
            }
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
        }

        private void Start()
        {
            if (autoLoad) Load();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && autoSave) Save();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && autoSave) Save();
        }

        private void OnApplicationQuit()
        {
            if (autoSave) Save();
        }

        public void SetExpMultiplier(float value) => modifiers.SetExpMultiplier(value);

        public void Register(CharacterProgression member)
        {
            if (member == null || members.Contains(member)) return;

            foreach (var other in members)
            {
                if (other.ProgressId == member.ProgressId)
                {
                    Debug.LogWarning($"ProgressId \"{member.ProgressId}\" が重なっている（{other.name} と {member.name}）。保存で区別できない。", member);
                }
            }

            members.Add(member);
            member.SetModifiers(modifiers);
            member.LeveledUp += OnMemberLeveledUp;

            if (pending.TryGetValue(member.ProgressId, out var data))
            {
                pending.Remove(member.ProgressId);
                member.Restore(data);
            }
        }

        public void Unregister(CharacterProgression member)
        {
            if (member == null || !members.Remove(member)) return;

            member.LeveledUp -= OnMemberLeveledUp;
            member.SetModifiers(null);

            // 抜けた仲間の進行も保存から消さない。戻ってきたら続きから。
            var data = member.Capture();
            if (!string.IsNullOrEmpty(data.id)) pending[data.id] = data;
        }

        /// <summary>
        /// 敵を倒したときなどの経験値。倍率を掛けた同じ量をパーティ全員に足す。実際に配った量を返す。
        /// </summary>
        public int GrantExp(int baseAmount)
        {
            int amount = modifiers.ApplyExp(baseAmount, 1f + gearExpBonus);
            if (amount <= 0) return 0;

            // 足している途中でレベルアップの購読者がメンバーを増減させても崩れないように写してから回す。
            granting = true;
            leveledWhileGranting = false;
            try
            {
                foreach (var member in members.ToArray()) member.AddExp(amount);
            }
            finally
            {
                granting = false;
            }

            if (leveledWhileGranting && autoSave) Save();
            ExpGranted?.Invoke(this, amount);
            return amount;
        }

        /// <summary>ゲームオーバー。全員（まだ合流していない仲間の保存分も）を Lv1・経験値 0・HP 満タンに戻し、保存も書き換える。</summary>
        public void ResetForNewRun()
        {
            pending.Clear();
            foreach (var member in members.ToArray()) member.ResetProgress();
            RunReset?.Invoke(this);
            if (autoSave) Save();
        }

        /// <summary>全員分の保存データ。セーブ全体のタスクはこれを自分のデータに入れて書き出せばよい。</summary>
        public PartyProgressSaveData CaptureSaveData()
        {
            var data = new PartyProgressSaveData();
            foreach (var member in members) data.members.Add(member.Capture());
            foreach (var entry in pending.Values)
            {
                if (data.Find(entry.id) == null) data.members.Add(entry);
            }
            return data;
        }

        /// <summary>保存データに戻す。まだいない仲間の分は、その仲間が登録されたときに当てる。</summary>
        public void RestoreSaveData(PartyProgressSaveData data)
        {
            pending.Clear();
            if (data?.members == null) return;

            foreach (var entry in data.members)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;

                var member = FindMember(entry.id);
                if (member != null) member.Restore(entry);
                else pending[entry.id] = entry;
            }
        }

        public CharacterProgression FindMember(string progressId)
        {
            foreach (var member in members)
            {
                if (member.ProgressId == progressId) return member;
            }
            return null;
        }

        public void Save()
        {
            ProgressSaveStore.Save(CaptureSaveData(), SavePath);
        }

        /// <summary>保存データがあれば読み込んで戻す。読めたら true。</summary>
        public bool Load()
        {
            if (!ProgressSaveStore.TryLoad(out var data, SavePath)) return false;
            RestoreSaveData(data);
            return true;
        }

        private void OnMemberLeveledUp(CharacterProgression member, int from, int to)
        {
            if (granting) leveledWhileGranting = true;
            else if (autoSave) Save();
        }
    }
}
