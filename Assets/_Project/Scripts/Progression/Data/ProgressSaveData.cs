using System;
using System.Collections.Generic;

namespace TpsDungeon.Progression
{
    /// <summary>1 キャラ分の保存データ。JsonUtility で書き出せる形にしてある。</summary>
    [Serializable]
    public sealed class CharacterProgressSaveData
    {
        /// <summary>CharacterProgression.ProgressId。主人公は "player"。</summary>
        public string id;

        public int level = GrowthCurve.MinLevel;

        /// <summary>今のレベルに入ってから貯めた経験値。</summary>
        public int exp;

        /// <summary>現在 HP。負の値は「満タン」として扱う（体力を持たないキャラなど）。</summary>
        public int currentHp = -1;
    }

    /// <summary>パーティ全員分の保存データ。セーブ全体のタスクはこれを丸ごと自分のデータに入れればよい。</summary>
    [Serializable]
    public sealed class PartyProgressSaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public List<CharacterProgressSaveData> members = new List<CharacterProgressSaveData>();

        public CharacterProgressSaveData Find(string id)
        {
            if (members == null) return null;
            foreach (var member in members)
            {
                if (member != null && member.id == id) return member;
            }
            return null;
        }
    }
}
