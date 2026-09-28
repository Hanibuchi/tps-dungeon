namespace TpsDungeon.Progression
{
    /// <summary>
    /// CharacterProgression が MaxHP を書き込む先。主人公は PlayerHealth がこれを実装している。
    /// 同じ GameObject に置けば CharacterProgression が見つける。
    /// </summary>
    public interface IHealthPool
    {
        int MaxHp { get; }
        int CurrentHp { get; }

        /// <summary>最大と現在をまとめて変える（変更の通知は 1 回にする）。現在は 0〜最大に収める。</summary>
        void SetMaxAndCurrent(int max, int current);
    }
}
