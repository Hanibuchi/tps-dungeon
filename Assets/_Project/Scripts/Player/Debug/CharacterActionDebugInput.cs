using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Player.DebugTools
{
    /// <summary>
    /// 全身の動き（CharacterAction）を 1 本ずつ選んで再生し、目で確かめるための入力。
    /// [ と ] で選び、Enter で再生、Backspace で止める。画面左上に今の選択を出す。
    /// 確認用シーンのプレイヤーにだけ付ける想定で、製品版のシーンやプレハブには残さない。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Character Action Debug Input")]
    public sealed class CharacterActionDebugInput : MonoBehaviour
    {
        [SerializeField, Tooltip("再生の窓口。未設定ならこの GameObject から探す。")]
        private CharacterActions actions;

        [SerializeField] private Key previousKey = Key.LeftBracket;
        [SerializeField] private Key nextKey = Key.RightBracket;
        [SerializeField] private Key playKey = Key.Enter;
        [SerializeField] private Key stopKey = Key.Backspace;

        [SerializeField, Tooltip("画面左上に今の選択とキー割り当てを表示する。")]
        private bool showOverlay = true;

        private CharacterAction[] all;
        private int selected = 1;

        private void Reset()
        {
            actions = GetComponent<CharacterActions>();
        }

        private void Awake()
        {
            if (actions == null) actions = GetComponent<CharacterActions>();
            all = (CharacterAction[])Enum.GetValues(typeof(CharacterAction));
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || actions == null) return;

            if (keyboard[previousKey].wasPressedThisFrame) selected = (selected - 1 + all.Length) % all.Length;
            if (keyboard[nextKey].wasPressedThisFrame) selected = (selected + 1) % all.Length;
            if (keyboard[playKey].wasPressedThisFrame) actions.Play(all[selected]);
            if (keyboard[stopKey].wasPressedThisFrame) actions.Stop();
        }

        private void OnGUI()
        {
            if (!showOverlay || all == null) return;

            GUILayout.BeginArea(new Rect(10, 220, 320, 90), GUI.skin.box);
            GUILayout.Label($"全身の動き: {all[selected]}（{selected}/{all.Length - 1}）");
            GUILayout.Label(actions != null && actions.IsPlaying ? $"再生中: {actions.Requested}" : "再生していない");
            GUILayout.Label($"[{previousKey}] [{nextKey}] 選ぶ / [{playKey}] 再生 / [{stopKey}] 止める");
            GUILayout.EndArea();
        }
    }
}
