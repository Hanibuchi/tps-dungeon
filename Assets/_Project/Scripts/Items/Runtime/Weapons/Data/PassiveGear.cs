namespace TpsDungeon.Items
{
    /// <summary>
    /// 振らずに持っているだけで効く装備の種類。値はアセットに焼き込まれるので並べ替えないこと。
    /// お守りはホットバーに入れておくだけで、盾は片手武器（WeaponTypeDefinition.CanUseShield）を持っている間だけ効く。
    /// </summary>
    public enum PassiveGear
    {
        None = 0,
        Charm = 1,
        Shield = 2,
    }
}
