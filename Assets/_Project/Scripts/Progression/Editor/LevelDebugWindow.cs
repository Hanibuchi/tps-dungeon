using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TpsDungeon.Progression.Editor
{
    /// <summary>
    /// パーティのレベルを決めるためのウィンドウ（Tools/TPS Dungeon/デバッグ/レベルのデバッグ）。
    /// - Play 中は今いる仲間（PartyProgression.Current）のレベルを書き換え、保存にも書く。
    /// - Play していなければ保存（progress.json）にある仲間のレベルを書き換える。次に Play するとそのレベルで始まる。
    /// どちらも経験値は 0、HP は満タンになる。全員まとめても 1 人ずつでも決められる。
    /// </summary>
    public sealed class LevelDebugWindow : EditorWindow
    {
        private Label status;
        private IntegerField allLevel;
        private VisualElement allRow;
        private ScrollView list;
        private readonly List<(string id, Label label, IntegerField field)> rows = new List<(string, Label, IntegerField)>();
        private string builtKey;

        [MenuItem("Tools/TPS Dungeon/デバッグ/レベルのデバッグ", false, 2)]
        public static void Open()
        {
            var window = GetWindow<LevelDebugWindow>();
            window.titleContent = new GUIContent("レベルのデバッグ");
            window.minSize = new Vector2(320f, 220f);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;

            status = new Label { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 6 } };
            root.Add(status);

            allRow = Row();
            allLevel = new IntegerField("全員") { value = 10, style = { flexGrow = 1 } };
            allLevel.labelElement.style.minWidth = 40;
            allRow.Add(allLevel);
            allRow.Add(new Button(() => SetAll(allLevel.value)) { text = "にする" });
            allRow.Add(new Button(() => AddAll(1)) { text = "+1" });
            allRow.Add(new Button(() => AddAll(-1)) { text = "-1" });
            allRow.Add(new Button(() => SetAll(LevelBalance.MaxLevel)) { text = "最大" });
            allRow.Add(new Button(() => SetAll(GrowthCurve.MinLevel)) { text = "1" });
            root.Add(allRow);

            root.Add(new Label("1 人ずつ") { style = { marginTop = 8, unityFontStyleAndWeight = FontStyle.Bold } });
            list = new ScrollView { style = { flexGrow = 1 } };
            root.Add(list);

            root.schedule.Execute(Refresh).Every(250);
            Refresh();
        }

        private static VisualElement Row() => new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 2 } };

        /// <summary>今のレベルを (id, 名前, レベル, 経験値の説明) で並べる。Play 中は仲間、そうでなければ保存から。</summary>
        private static List<(string id, string name, int level, string detail)> Entries()
        {
            var entries = new List<(string, string, int, string)>();
            if (EditorApplication.isPlaying)
            {
                var party = PartyProgression.Current;
                if (party == null) return entries;
                foreach (var m in party.Members)
                {
                    entries.Add((m.ProgressId, m.name, m.Level, $"Exp {m.Exp}/{m.ExpToNext}  HP {m.CurrentHp}/{m.MaxHp}  ATK {m.BaseAttack:0.0}"));
                }
            }
            else if (ProgressSaveStore.TryLoad(out var data))
            {
                foreach (var m in data.members)
                {
                    if (m != null && !string.IsNullOrEmpty(m.id)) entries.Add((m.id, null, m.level, $"Exp {m.exp}"));
                }
            }
            return entries;
        }

        private void Refresh()
        {
            if (status == null) return;

            var entries = Entries();
            bool playing = EditorApplication.isPlaying;
            if (playing)
            {
                status.text = PartyProgression.Current != null
                    ? $"Play 中: 今のパーティ（{entries.Count} 人）を書き換える。最大 Lv{LevelBalance.MaxLevel}。"
                    : "Play 中だがパーティ（PartyProgression）が無い。";
            }
            else
            {
                status.text = entries.Count > 0
                    ? $"保存（{ProgressSaveStore.FileName}）の {entries.Count} 人を書き換える。次に Play するとこのレベルで始まる。最大 Lv{LevelBalance.MaxLevel}。"
                    : $"保存（{ProgressSaveStore.FileName}）に仲間がいない。一度 Play するか、Play 中に使う。";
            }
            allRow.SetEnabled(entries.Count > 0);

            // 顔ぶれが変わったときだけ作り直す（打っている途中の欄を消さないように）。
            var key = new System.Text.StringBuilder(playing ? "play" : "save");
            foreach (var e in entries) key.Append('|').Append(e.id);
            if (key.ToString() != builtKey) BuildList(entries, key.ToString());

            for (int i = 0; i < rows.Count && i < entries.Count; i++)
            {
                var e = entries[i];
                rows[i].label.text = $"{(e.name != null ? $"{e.id}（{e.name}）" : e.id)}  Lv{e.level}  {e.detail}";
            }
        }

        private void BuildList(List<(string id, string name, int level, string detail)> entries, string key)
        {
            builtKey = key;
            list.Clear();
            rows.Clear();

            foreach (var e in entries)
            {
                string id = e.id;
                var label = new Label { style = { whiteSpace = WhiteSpace.Normal, marginTop = 4 } };
                list.Add(label);

                var row = Row();
                var field = new IntegerField("レベル") { value = e.level, style = { flexGrow = 1 } };
                field.labelElement.style.minWidth = 40;
                row.Add(field);
                row.Add(new Button(() => SetOne(id, field.value)) { text = "にする" });
                row.Add(new Button(() => AddOne(id, 1)) { text = "+1" });
                row.Add(new Button(() => AddOne(id, -1)) { text = "-1" });
                list.Add(row);

                rows.Add((id, label, field));
            }
        }

        private void SetAll(int level) => Apply(null, _ => level, $"全員 Lv{ClampLevel(level)}");

        private void AddAll(int delta) => Apply(null, current => current + delta, $"全員 {(delta >= 0 ? "+" : "")}{delta} レベル");

        private void SetOne(string id, int level) => Apply(id, _ => level, $"{id} を Lv{ClampLevel(level)}");

        private void AddOne(string id, int delta) => Apply(id, current => current + delta, $"{id} を {(delta >= 0 ? "+" : "")}{delta} レベル");

        private static int ClampLevel(int level) => Mathf.Clamp(level, GrowthCurve.MinLevel, LevelBalance.MaxLevel);

        /// <summary>id の仲間（null なら全員）のレベルを next(今のレベル) にする。</summary>
        private void Apply(string id, System.Func<int, int> next, string action)
        {
            if (EditorApplication.isPlaying)
            {
                var party = PartyProgression.Current;
                if (party == null) return;
                foreach (var m in party.Members)
                {
                    if (id == null || m.ProgressId == id) m.SetLevel(next(m.Level));
                }
                if (party.AutoSave) party.Save();
            }
            else
            {
                if (!ProgressSaveStore.TryLoad(out var data)) return;
                foreach (var m in data.members)
                {
                    if (m == null || (id != null && m.id != id)) continue;
                    m.level = ClampLevel(next(m.level));
                    m.exp = 0;
                    m.currentHp = -1;
                }
                ProgressSaveStore.Save(data);
            }

            Debug.Log($"[Progression] デバッグ: {action}");
            Refresh();
            SyncFields();
        }

        /// <summary>書き換えたあと、欄の数字を今のレベルに合わせる。</summary>
        private void SyncFields()
        {
            var entries = Entries();
            for (int i = 0; i < rows.Count && i < entries.Count; i++) rows[i].field.SetValueWithoutNotify(entries[i].level);
        }
    }
}
