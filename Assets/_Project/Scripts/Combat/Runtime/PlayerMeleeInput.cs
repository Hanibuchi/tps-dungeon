using System.Reflection;
using TpsDungeon.Items;
using TpsDungeon.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Combat
{
    /// <summary>
    /// プレイヤーの入力を MeleeAttacker に渡す。攻撃キーの押下 → PressAttack、ホットバーで選んでいる物 → Equip、カメラの前 → AimForward。
    /// 走る段（ダッシュ突き）で走っている間は、ThirdPersonController の歩きの速さを 0 にして、入力で走りがぶれないようにする。
    /// ThirdPersonController は CharacterController の速度から今の速さを引き継ぐので、そのままだと走りの速さで二重に進み、
    /// 走り終えてからも惰性で滑る。速さの追従（SpeedChangeRate）も一瞬にして 0 へ落とし、走り終えた次のフレームまで止めておく。
    /// ThirdPersonController は名前で探してフィールドを名前で書くので、Starter Assets のアセンブリには依存しない（GamePauser と同じ）。
    /// プレイヤーのルート（PlayerInput・PlayerHotbar・PlayerInventory・MeleeAttacker と同じ GameObject）に付ける。
    /// </summary>
    // 押下をその同じフレームの MeleeAttacker.Update で使わせるため、先に回す。
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeleeAttacker))]
    [AddComponentMenu("TPS Dungeon/Player Melee Input")]
    public sealed class PlayerMeleeInput : MonoBehaviour
    {
        private const string LocomotionTypeName = "ThirdPersonController";
        private static readonly (string field, float locked)[] LocomotionLocks =
        {
            ("MoveSpeed", 0f),
            ("SprintSpeed", 0f),
            ("SpeedChangeRate", 1e6f),
        };

        /// <summary>走り終えてから歩きを戻すまでのフレーム数。最後に走ったフレームの速度を ThirdPersonController に拾わせない。</summary>
        private const int UnlockDelayFrames = 1;

        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定ならこの GameObject から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("攻撃のアクション名。")]
        private string attackActionName = "Player/Attack";

        private MeleeAttacker attacker;
        private PlayerHotbar hotbar;
        private PlayerInventory inventory;
        private InputAction attackAction;
        private Transform cameraTransform;

        private Component locomotion;
        private FieldInfo[] locomotionFields;
        private float[] savedSpeeds;
        private bool locomotionLocked;
        private int framesSinceLunge = int.MaxValue;

        private void Reset()
        {
            playerInput = GetComponent<PlayerInput>();
        }

        private void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
            attacker = GetComponent<MeleeAttacker>();
            hotbar = GetComponent<PlayerHotbar>();
            inventory = GetComponent<PlayerInventory>();
            FindLocomotion();
        }

        private void OnEnable()
        {
            if (playerInput != null && playerInput.actions != null)
            {
                attackAction = playerInput.actions.FindAction(attackActionName);
                if (attackAction == null) Debug.LogWarning($"アクション '{attackActionName}' が {playerInput.actions.name} に無い", this);
            }

            if (hotbar != null) hotbar.Changed += OnHotbarChanged;
            if (inventory != null) inventory.Changed += OnInventoryChanged;
            attacker.Equip(Current());
        }

        private void OnDisable()
        {
            if (hotbar != null) hotbar.Changed -= OnHotbarChanged;
            if (inventory != null) inventory.Changed -= OnInventoryChanged;
            attackAction = null;
            SetLocomotionLocked(false);
        }

        // 枠を選び直したら、中身が同じ（空の枠どうしなど）でも持ち替えとして待たせる。
        private void OnHotbarChanged(PlayerHotbar _) => attacker.Equip(Current(), true);

        private void OnInventoryChanged(PlayerInventory _) => attacker.Equip(Current());

        private ItemInstance Current() => hotbar != null && inventory != null ? inventory.Inventory[hotbar.SelectedIndex] : null;

        private void Update()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) attacker.AimForward = cameraTransform.forward;

            bool pressed = attackAction != null && attackAction.WasPressedThisFrame()
                           && playerInput != null && attackAction.actionMap == playerInput.currentActionMap;
            if (pressed) attacker.PressAttack();
        }

        // MeleeAttacker の Update で走り出し・走り終わりが決まるので、同じフレームのうちに歩きを止める・戻す。
        private void LateUpdate()
        {
            framesSinceLunge = attacker.IsLunging ? 0 : framesSinceLunge == int.MaxValue ? framesSinceLunge : framesSinceLunge + 1;
            SetLocomotionLocked(framesSinceLunge <= UnlockDelayFrames);
        }

        private void FindLocomotion()
        {
            foreach (Component component in GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != LocomotionTypeName) continue;

                var fields = new FieldInfo[LocomotionLocks.Length];
                for (int i = 0; i < fields.Length; i++)
                {
                    fields[i] = component.GetType().GetField(LocomotionLocks[i].field, BindingFlags.Public | BindingFlags.Instance);
                    if (fields[i] == null || fields[i].FieldType != typeof(float)) return;
                }

                locomotion = component;
                locomotionFields = fields;
                savedSpeeds = new float[fields.Length];
                return;
            }
        }

        private void SetLocomotionLocked(bool locked)
        {
            if (locked == locomotionLocked || locomotion == null) return;

            locomotionLocked = locked;
            for (int i = 0; i < locomotionFields.Length; i++)
            {
                if (locked)
                {
                    savedSpeeds[i] = (float)locomotionFields[i].GetValue(locomotion);
                    locomotionFields[i].SetValue(locomotion, LocomotionLocks[i].locked);
                }
                else
                {
                    locomotionFields[i].SetValue(locomotion, savedSpeeds[i]);
                }
            }
        }
    }
}
