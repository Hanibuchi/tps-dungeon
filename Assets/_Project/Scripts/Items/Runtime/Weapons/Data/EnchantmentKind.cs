namespace TpsDungeon.Items
{
    /// <summary>
    /// エンチャントの種類（ゲーム全体で 22 種）。値はアセットに焼き込まれるので並べ替えず、足すときは末尾に。
    /// どの武器種に付くかは武器種の定義（WeaponTypeDefinition）が持つ。
    /// </summary>
    public enum EnchantmentKind
    {
        DamageUp = 0,
        CritChance = 1,
        DropUp = 2,
        RapidFire = 3,
        ProjectileCount = 4,
        Size = 5,
        Duration = 6,
        Pierce = 7,
        Multishot = 8,
        HealUp = 9,
        Homing = 10,
        ChargeTimeDown = 11,
        Stun = 12,
        ProjectileSpeed = 13,
        Knockback = 14,
        Explosion = 15,
        ComboBonus = 16,
        MoveSpeed = 17,
        Exp = 18,
        MaxHp = 19,
        Defense = 20,
        Regen = 21,
    }
}
