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
    }

    /// <summary>生成時に各部屋へ割り当てられる役割。</summary>
    public enum RoomRole
    {
        Normal = 0,
        StairUp = 1,
        StairDown = 2,
        Shop = 3,
    }

    public static class RoomRoleUtil
    {
        /// <summary>その役割を担えるテンプレートが持つべきタグ。</summary>
        public static RoomTag RequiredTag(this RoomRole role)
        {
            switch (role)
            {
                case RoomRole.Shop: return RoomTag.Shop;
                case RoomRole.StairUp:
                case RoomRole.StairDown: return RoomTag.Stair;
                default: return RoomTag.Normal;
            }
        }
    }
}
