using TpsDungeon.Items;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// オフハンドの盾（PlayerGear.OffHandShield。盾を持てる武器種を選んでいる間だけ）の見た目を左手に持たせる。
    /// 持ち位置は盾の武器種の左手の値（HeldWeaponGrip）。選んでいる盾は MeleeAttacker が手の物として右手に持たせるので、ここでは出さない。
    /// プレイヤーのルート（PlayerGear と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Held Shield View")]
    public sealed class HeldShieldView : MonoBehaviour
    {
        [SerializeField, Tooltip("左手の骨を探す Animator。未設定なら子から探す。")]
        private Animator animator;

        private PlayerGear gear;
        private ItemInstance shown;
        private GameObject model;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            gear = GetComponent<PlayerGear>();
        }

        private void OnEnable()
        {
            if (gear == null) return;
            gear.Changed += OnGearChanged;
            Show(gear.OffHandShield);
        }

        private void OnDisable()
        {
            if (gear != null) gear.Changed -= OnGearChanged;
            Show(null);
        }

        private void OnGearChanged(PlayerGear _) => Show(gear.OffHandShield);

        private void Show(ItemInstance shield)
        {
            if (shield == shown && (shield == null || model != null)) return;

            shown = shield;
            if (model != null) Destroy(model);
            model = null;

            WeaponDefinition weapon = shield?.Weapon;
            if (weapon == null || weapon.HeldModel == null || animator == null || !animator.isHuman) return;

            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (hand == null) return;

            model = Instantiate(weapon.HeldModel, hand);
            model.name = weapon.HeldModel.name;
            model.AddComponent<HeldWeaponGrip>().Init(weapon.WeaponType, true, isOffHand: true);
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            WeaponAura.Attach(model, weapon.Rank, held: true);
        }
    }
}
