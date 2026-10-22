using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Progression
{
    /// <summary>
    /// キーで経験値やレベルを足して、成長を確かめるための入力。エディタと開発ビルドでだけ動き、製品版では何もしない。
    /// 結果はコンソールに出す（画面には何も足さない）。Shift を押しながらだと量が 10 倍。
    /// 魂の強化のソウルを足す・強化を全部戻すキーもここにある。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Progression Debug Input")]
    public sealed class ProgressionDebugInput : MonoBehaviour
    {
        // 製品版では Update ごと消えるので、設定の欄が読まれないという警告を黙らせる。
#pragma warning disable CS0414, CS0649
        [SerializeField, Tooltip("操作するパーティ。未設定なら PartyProgression.Current。")]
        private PartyProgression party;

        [SerializeField, Min(1), Tooltip("1 回で足す経験値（倍率を掛ける前）。")]
        private int expAmount = 100;

        [SerializeField, Tooltip("経験値を足す（敵を倒したときと同じ経路で、倍率も掛かる）。")]
        private Key grantExpKey = Key.F5;

        [SerializeField, Tooltip("全員 1 レベル上げる。")]
        private Key levelUpKey = Key.F6;

        [SerializeField, Tooltip("ゲームオーバーと同じく全員 Lv1 に戻す（保存も戻る）。")]
        private Key resetKey = Key.F7;

        [SerializeField, Tooltip("保存を消す。")]
        private Key deleteSaveKey = Key.F8;

        [SerializeField, Min(1), Tooltip("1 回で足すソウル（魂の強化の通貨）。")]
        private int soulAmount = 100;

        [SerializeField, Tooltip("ソウルを足す。")]
        private Key grantSoulsKey = Key.F9;

        [SerializeField, Tooltip("魂の強化を全部戻す（ソウルも 0 にする）。")]
        private Key resetUpgradesKey = Key.F10;
#pragma warning restore CS0414, CS0649

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            var keyboard = Keyboard.current;
            var target = party != null ? party : PartyProgression.Current;
            if (keyboard == null || target == null) return;

            int scale = keyboard.shiftKey.isPressed ? 10 : 1;

            if (keyboard[grantExpKey].wasPressedThisFrame)
            {
                int given = target.GrantExp(expAmount * scale);
                Log(target, $"経験値 +{given}（倍率 ×{target.Modifiers.ExpMultiplier}）");
            }

            if (keyboard[levelUpKey].wasPressedThisFrame)
            {
                foreach (var member in target.Members) member.AddLevels(scale);
                if (target.AutoSave) target.Save();
                Log(target, $"全員 +{scale} レベル");
            }

            if (keyboard[resetKey].wasPressedThisFrame)
            {
                target.ResetForNewRun();
                Log(target, "ゲームオーバー相当のリセット");
            }

            if (keyboard[grantSoulsKey].wasPressedThisFrame)
            {
                MetaUpgrades.AddSouls((long)soulAmount * scale);
                Debug.Log($"[Progression] ソウル +{soulAmount * scale}（手持ち {MetaUpgrades.Souls}）");
            }

            if (keyboard[resetUpgradesKey].wasPressedThisFrame)
            {
                MetaUpgrades.ResetAll();
                Debug.Log("[Progression] 魂の強化を全部戻した");
            }

            if (keyboard[deleteSaveKey].wasPressedThisFrame)
            {
                ProgressSaveStore.Delete(target.SavePath);
                Debug.Log($"[Progression] 保存を消した: {(string.IsNullOrEmpty(target.SavePath) ? ProgressSaveStore.DefaultPath : target.SavePath)}");
            }
        }

        private static void Log(PartyProgression target, string action)
        {
            var text = new System.Text.StringBuilder("[Progression] ").Append(action);
            foreach (var m in target.Members)
            {
                text.Append($"\n  {m.ProgressId}: Lv{m.Level}  Exp {m.Exp}/{m.ExpToNext}  HP {m.CurrentHp}/{m.MaxHp}  ATK {m.BaseAttack:0.0}");
            }
            Debug.Log(text.ToString());
        }
#endif
    }
}
