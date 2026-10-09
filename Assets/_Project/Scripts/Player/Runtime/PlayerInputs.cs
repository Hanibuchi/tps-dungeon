using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Player
{
    /// <summary>
    /// 入力を読むキャラ側のコンポーネント（ホットバー・攻撃・インタラクトなど）が PlayerInput を探す手順。
    /// 同じ GameObject に無ければ（パーティーではキャラに PlayerInput を持たせず、操作台が 1 つ持つ）、シーンの PlayerInput を使う。
    /// </summary>
    public static class PlayerInputs
    {
        public static PlayerInput Find(Component self)
        {
            if (self != null && self.TryGetComponent(out PlayerInput own)) return own;
            if (PlayerInput.all.Count > 0) return PlayerInput.all[0];
            return Object.FindAnyObjectByType<PlayerInput>();
        }
    }
}
