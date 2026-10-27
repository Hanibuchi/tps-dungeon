namespace TpsDungeon.Items
{
    /// <summary>
    /// 振らずに持っているだけで効く装備の種類。値はアセットに焼き込まれるので並べ替えないこと。
    /// お守りはホットバーに入れておくだけで、盾は手に持っている間だけ効く（盾そのものを選んでいるか、
    /// 盾を持てる武器種（WeaponTypeDefinition.CanUseShield）を選んでいてオフハンドに持つとき）。
    /// </summary>
    public enum PassiveGear
    {
        None = 0,
        Charm = 1,
        Shield = 2,
    }
}
