using System;

namespace TpsDungeon.Map.Data
{
    /// <summary>部屋テンプレートがどの役割に使えるかを表すフラグ。</summary>
    [Flags]
    public enum RoomTag
    {
        None = 0,
        Normal = 1 << 0,
        Shop = 1 << 1,
        Stair = 1 << 2,
        Boss = 1 << 3,
        /// <summary>ランダム階のショートカット（井戸）の部屋。</summary>
        Shortcut = 1 << 4,
    }

    /// <summary>生成時に各部屋へ割り当てられる役割。</summary>
    public enum RoomRole
    {
        Normal = 0,
        Entrance = 1,
        Exit = 2,
        Shop = 3,
        /// <summary>ランダム階のショートカット（井戸）。</summary>
        Shortcut = 4,
    }

    public static class RoomRoleUtil
    {
        /// <summary>その役割を担えるテンプレートが持つべきタグ。</summary>
        public static RoomTag RequiredTag(this RoomRole role)
        {
            switch (role)
            {
                case RoomRole.Shop: return RoomTag.Shop;
                case RoomRole.Shortcut: return RoomTag.Shortcut;
                case RoomRole.Entrance:
                case RoomRole.Exit: return RoomTag.Stair;
                default: return RoomTag.Normal;
            }
        }
    }
}
