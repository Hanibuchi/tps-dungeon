using TpsDungeon.Items;
using TpsDungeon.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// プレイヤーの入力を MeleeAttacker（と、あれば RangedAttacker）に渡す。攻撃キーの押下 → PressAttack、ホットバーで選んでいる物 → Equip、
    /// カメラの前 → AimForward、カメラの中心の線 → RangedAttacker.Aim、押している間 → RangedAttacker.AttackHeld（炎の杖が読む）。
    /// 押下は両方に渡し、今の武器を扱える方だけが攻撃する。
    /// 走る段（ダッシュ突き）で走っている間は、ThirdPersonController の歩きの速さを 0 にして（PlayerLocomotionSpeed.Locked）、入力で走りがぶれないようにする。
    /// ThirdPersonController は CharacterController の速度から今の速さを引き継ぐので、そのままだと走りの速さで二重に進み、
    /// 走り終えてからも惰性で滑る。速さの追従（SpeedChangeRate）も一瞬にして 0 へ落とし、走り終えた次のフレームまで止めておく。
    /// キャラのルート（PlayerHotbar・PlayerInventory・MeleeAttacker・RangedAttacker と同じ GameObject）に付ける。
    /// 操作しているキャラ（パーティーの先頭）だけで有効にし、後ろの仲間では止めて仲間の AI に任せる。
    /// PlayerInput は同じ GameObject に無ければシーンから探す（パーティーの操作台が持つ）。
    /// </summary>
    // 押下をその同じフレームの MeleeAttacker / RangedAttacker の Update で使わせるため、先に回す。
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeAttacker))]
    [AddComponentMenu("TPS Dungeon/Player Melee Input")]
    public sealed class PlayerMeleeInput : MonoBehaviour
    {
        /// <summary>走り終えてから歩きを戻すまでのフレーム数。最後に走ったフレームの速度を ThirdPersonController に拾わせない。</summary>
        private const int UnlockDelayFrames = 1;

        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定ならこの GameObject か、シーンから探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("攻撃のアクション名。")]
        private string attackActionName = "Player/Attack";

        private MeleeAttacker attacker;
        private RangedAttacker ranged;
        private PlayerHotbar hotbar;
        private PlayerInventory inventory;
        private InputAction attackAction;
        private Transform cameraTransform;

        private PlayerLocomotionSpeed locomotion;
        private int framesSinceLunge = int.MaxValue;

        private void Reset()
        {
            playerInput = GetComponent<PlayerInput>();
        }

        private void Awake()
        {
            attacker = GetComponent<MeleeAttacker>();
            ranged = GetComponent<RangedAttacker>();
            hotbar = GetComponent<PlayerHotbar>();
            inventory = GetComponent<PlayerInventory>();
            locomotion = GetComponent<PlayerLocomotionSpeed>();
            if (locomotion == null) locomotion = gameObject.AddComponent<PlayerLocomotionSpeed>();
        }

        private void OnEnable()
        {
            if (playerInput == null) playerInput = PlayerInputs.Find(this);
            if (playerInput != null && playerInput.actions != null)
            {
                attackAction = playerInput.actions.FindAction(attackActionName);
                if (attackAction == null) Debug.LogWarning($"アクション '{attackActionName}' が {playerInput.actions.name} に無い", this);
            }

            if (hotbar != null) hotbar.Changed += OnHotbarChanged;
            if (inventory != null) inventory.Changed += OnInventoryChanged;
            Equip(Current(), false);
        }

        private void OnDisable()
        {
            if (ranged != null)
            {
                ranged.Aim = null;
                ranged.AttackHeld = false;
            }
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            attackAction = null;
            SetLocomotionLocked(false);
        }

        // 枠を選び直したら、中身が同じ（空の枠どうしなど）でも持ち替えとして待たせる。
        private void OnHotbarChanged(PlayerHotbar _) => Equip(Current(), true);

        private void OnInventoryChanged(PlayerInventory _) => Equip(Current(), false);

        private void Equip(ItemInstance item, bool switched)
        {
            attacker.Equip(item, switched);
            if (ranged != null) ranged.Equip(item, switched);
        }

        private ItemInstance Current() => hotbar != null && inventory != null ? inventory.Inventory[hotbar.SelectedIndex] : null;

        private void Update()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) attacker.AimForward = cameraTransform.forward;
            if (ranged != null) ranged.Aim = cameraTransform != null ? new Ray(cameraTransform.position, cameraTransform.forward) : (Ray?)null;

            bool listening = attackAction != null && playerInput != null && attackAction.actionMap == playerInput.currentActionMap;
            if (ranged != null) ranged.AttackHeld = listening && attackAction.IsPressed();

            bool pressed = listening && attackAction.WasPressedThisFrame();
            if (!pressed) return;

            attacker.PressAttack();
            if (ranged != null) ranged.PressAttack();
        }

        // MeleeAttacker の Update で走り出し・走り終わりが決まるので、同じフレームのうちに歩きを止める・戻す。
        private void LateUpdate()
        {
            framesSinceLunge = attacker.IsLunging ? 0 : framesSinceLunge == int.MaxValue ? framesSinceLunge : framesSinceLunge + 1;
            SetLocomotionLocked(framesSinceLunge <= UnlockDelayFrames);
        }

        private void SetLocomotionLocked(bool locked)
        {
            if (locomotion != null) locomotion.Locked = locked;
        }
    }
}
