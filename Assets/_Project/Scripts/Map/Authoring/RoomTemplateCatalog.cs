using System;
using System.Collections.Generic;
using UnityEngine;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>
    /// 1 つの層で使う部屋テンプレート Prefab の一覧。
    /// 階ごとに差し替えれば、同じ生成器のまま層ごとのテーマを切り替えられる。
    /// </summary>
    [CreateAssetMenu(fileName = "RoomCatalog", menuName = "TPS Dungeon/Room Template Catalog")]
    public sealed class RoomTemplateCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("RoomTemplate コンポーネントを持つ部屋 Prefab。")]
            public GameObject prefab;

            [Min(0f), Tooltip("0 より大きければ Prefab 側の重みを上書きする。")]
            public float weightOverride;
        }

        [SerializeField, Tooltip("この層で使う部屋テンプレートの一覧。テンプレート ID は Prefab 名になる。")]
        private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public void SetEntries(IEnumerable<Entry> newEntries)
        {
            entries = new List<Entry>(newEntries);
        }

        /// <summary>生成器に渡すデータ表現を組み立てる。テンプレート ID は Prefab 名。</summary>
        public List<RoomTemplateData> BuildTemplateData()
        {
            var result = new List<RoomTemplateData>();
            foreach (var entry in entries)
            {
                if (entry?.prefab == null) continue;

                var template = entry.prefab.GetComponent<RoomTemplate>();
                if (template == null)
                {
                    Debug.LogError($"[{name}] {entry.prefab.name} に RoomTemplate が付いていないので無視した", this);
                    continue;
                }
                if (!template.HasSocketOnEverySide())
                {
                    Debug.LogWarning($"[{name}] {entry.prefab.name} は四辺すべてにドア候補が無い。繋がらない方向ができる。", entry.prefab);
                }

                result.Add(template.ToData(entry.prefab.name, entry.weightOverride));
            }
            return result;
        }

        /// <summary>テンプレート ID から Prefab を引く。</summary>
        public GameObject PrefabFor(string templateId)
        {
            foreach (var entry in entries)
            {
                if (entry?.prefab != null && entry.prefab.name == templateId) return entry.prefab;
            }
            return null;
        }
    }
}
