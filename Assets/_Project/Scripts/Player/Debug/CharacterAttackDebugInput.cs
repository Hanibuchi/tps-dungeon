using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Player.DebugTools
{
    /// <summary>
    /// 数字キーで武器を持ち替えて攻撃させ、上半身のアニメーションを目で確かめるための入力。
    /// Animator のパラメータを直接叩くだけで、ゲーム側のコードには何も依存させていないし、依存もされない。
    /// 武器と攻撃の仕組みができたら、このフォルダごと消せば切り離せる（アセンブリも自動参照にしていない）。
    /// 製品版のシーンやプレハブには残さない想定。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Character Attack Debug Input")]
    public sealed class CharacterAttackDebugInput : MonoBehaviour
    {
        // CharacterAnimatorBuilder（エディタ専用アセンブリ）が焼き込んだ値。
        // ランタイムからは参照できないので写しを持つ。向こうを変えたらここも揃えること。
        private const string WeaponTypeParam = "WeaponType";
        private const string AttackParam = "Attack";

        private static readonly string[] WeaponNames = { "素手", "片手武器", "両手武器", "弓", "魔法" };

        [SerializeField, Tooltip("操作する Animator。未設定ならこの GameObject と子から探す。")]
        private Animator animator;

        [Header("キー割り当て（並びは WeaponType の値の順）")]
        [SerializeField, Tooltip("押すとその武器に持ち替えて攻撃する。弓は持ち替え直後だと引き終わってから放つ。")]
        private Key[] attackKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

        [SerializeField, Tooltip("押している間は数字キーで持ち替えるだけにして、攻撃しない。構えを眺めたいとき用。")]
        private Key equipOnlyModifier = Key.LeftShift;

        [SerializeField, Tooltip("画面左上にキー割り当てと今の武器を表示する。")]
        private bool showOverlay = true;

        private int weaponTypeHash;
        private int attackHash;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            weaponTypeHash = Animator.StringToHash(WeaponTypeParam);
            attackHash = Animator.StringToHash(AttackParam);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || animator == null) return;

            bool equipOnly = keyboard[equipOnlyModifier].isPressed;
            for (int weapon = 0; weapon < attackKeys.Length && weapon < WeaponNames.Length; weapon++)
            {
                if (!keyboard[attackKeys[weapon]].wasPressedThisFrame) continue;

                if (equipOnly) Equip(weapon);
                else Attack(weapon);
            }
        }

        /// <summary>武器を持ち替える。</summary>
        public void Equip(int weaponType)
        {
            animator.SetInteger(weaponTypeHash, weaponType);
        }

        /// <summary>
        /// 武器を持ち替えて攻撃する。持ち替えと同じフレームにトリガーを立ててよい。
        /// 近接と魔法はその場で、弓は構えに入った瞬間に放つよう Animator 側が待ってくれる。
        /// </summary>
        public void Attack(int weaponType)
        {
            Equip(weaponType);
            animator.SetTrigger(attackHash);
        }

        private void OnGUI()
        {
            if (!showOverlay) return;

            GUILayout.BeginArea(new Rect(10, 10, 320, 200), GUI.skin.box);
            if (animator == null)
            {
                GUILayout.Label("Animator が見つからない");
            }
            else
            {
                int current = animator.GetInteger(weaponTypeHash);
                string currentName = current >= 0 && current < WeaponNames.Length ? WeaponNames[current] : current.ToString();
                GUILayout.Label($"武器: {currentName}");
                for (int i = 0; i < attackKeys.Length && i < WeaponNames.Length; i++)
                {
                    GUILayout.Label($"[{attackKeys[i]}] {WeaponNames[i]}で攻撃");
                }
                GUILayout.Label($"[{equipOnlyModifier}] + 数字: 持ち替えだけ");
            }
            GUILayout.EndArea();
        }
    }
}
