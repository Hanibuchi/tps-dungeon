using System.Collections.Generic;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 武器ごとの待ちの明ける時刻。持ち替えて離れている間もその武器の待ちとして減っていき、戻ってきたら残りを引き継ぐ。
    /// 待ちは武器どうしで共有しない（持ち替えの待ちは攻撃役が別に持つ）。キーは手に持つ物（ItemInstance）で、素手は null。UnityEngine に依存しない。
    /// </summary>
    public sealed class WeaponCooldowns
    {
        private static readonly object Unarmed = new object();

        private readonly Dictionary<object, float> readyAt = new Dictionary<object, float>();
        private readonly List<object> expired = new List<object>();

        /// <summary>key の待ちが now から remaining 秒残っていると覚える。明けた物はここで忘れる。</summary>
        public void Store(object key, float now, float remaining)
        {
            expired.Clear();
            foreach (KeyValuePair<object, float> pair in readyAt)
            {
                if (pair.Value <= now) expired.Add(pair.Key);
            }

            foreach (object old in expired) readyAt.Remove(old);

            if (remaining > 0f) readyAt[key ?? Unarmed] = now + remaining;
            else readyAt.Remove(key ?? Unarmed);
        }

        /// <summary>key の待ちの now からの残り（秒）。覚えていなければ 0。</summary>
        public float Remaining(object key, float now)
        {
            return readyAt.TryGetValue(key ?? Unarmed, out float at) && at > now ? at - now : 0f;
        }

        /// <summary>全部忘れる。</summary>
        public void Clear() => readyAt.Clear();
    }
}
