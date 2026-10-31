using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 敵の id（lava_slime など）。敵のプレハブのルートに付ける。
    /// 敵の台帳（Enemies.csv）・武器のドロップ表（EnemyWeaponDrops.csv など）は、どれもこの id で敵を書く。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Enemy Identity")]
    public sealed class EnemyIdentity : MonoBehaviour
    {
        [SerializeField, Tooltip("敵の id。小文字の英字と _ で書く（lava_slime など）。CSV はこの id で敵を書く。")]
        private string id;

        public string Id => id;
    }
}
