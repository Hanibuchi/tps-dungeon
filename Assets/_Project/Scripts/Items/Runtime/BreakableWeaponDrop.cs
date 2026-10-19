using TpsDungeon.Enemies;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 壊した箱（木箱・宝箱）から武器を出す。<see cref="BreakableProp"/> が武器のランクを選んだら（WeaponRolled）、
    /// <see cref="EnemyDropTable.Default"/> の落とせる武器からそのランクの 1 本を選び、箱の所から少し跳ね上げて床へ落とす。
    /// 箱のプレハブのルートに付ける（BreakablePrefabBuilder が付ける）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BreakableProp))]
    [AddComponentMenu("TPS Dungeon/Breakable Weapon Drop")]
    public sealed class BreakableWeaponDrop : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("武器を出す高さ（箱のルートから上へ、m）。箱の上から床へ落ちる。")]
        private float spawnLift = 0.8f;

        private static readonly System.Random Rng = new System.Random();

        private BreakableProp prop;

        private void Awake()
        {
            prop = GetComponent<BreakableProp>();
        }

        private void OnEnable()
        {
            if (prop == null) prop = GetComponent<BreakableProp>();
            prop.WeaponRolled += OnWeaponRolled;
        }

        private void OnDisable()
        {
            if (prop != null) prop.WeaponRolled -= OnWeaponRolled;
        }

        private void OnWeaponRolled(BreakableProp _, int rank)
        {
            EnemyDropTable table = EnemyDropTable.Default;
            WeaponDefinition weapon = table != null ? table.PickWeapon((WeaponRank)rank, Rng) : null;
            if (weapon == null)
            {
                Debug.LogWarning($"{name} から {(WeaponRank)rank} の武器を出すはずが、ドロップ表に {(WeaponRank)rank} の武器が無い", this);
                return;
            }
            ItemDropper.Drop(ItemInstance.Create(weapon, Rng), transform.position + Vector3.up * spawnLift);
        }
    }
}
