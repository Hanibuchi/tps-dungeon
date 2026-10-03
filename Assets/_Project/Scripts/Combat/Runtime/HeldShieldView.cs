using TpsDungeon.Items;
using UnityEngine;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// 効いている盾（PlayerGear.ActiveShield。片手武器を持っている間だけ）の見た目を左手に持たせる。
    /// 持ち位置は盾の武器種の HeldLocalPosition / HeldLocalEuler（HeldWeaponGrip）。盾そのものを選んでいるときは
    /// 効いている盾にならず、MeleeAttacker が手の物として持たせるので、ここでは出さない。
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
            Show(gear.ActiveShield);
        }

        private void OnDisable()
        {
            if (gear != null) gear.Changed -= OnGearChanged;
            Show(null);
        }

        private void OnGearChanged(PlayerGear _) => Show(gear.ActiveShield);

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
            model.AddComponent<HeldWeaponGrip>().Init(weapon.WeaponType);
            foreach (Collider c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            WeaponAura.Attach(model, weapon.Rank);
        }
    }
}
