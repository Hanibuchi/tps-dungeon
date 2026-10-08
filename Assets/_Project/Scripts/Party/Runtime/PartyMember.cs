using TpsDungeon.Combat;
using TpsDungeon.Interaction;
using TpsDungeon.Items;
using TpsDungeon.Player;
using TpsDungeon.Progression;
using UnityEngine;
using UnityEngine.AI;

namespace TpsDungeon.Party
{
    /// <summary>
    /// パーティーの 1 人。全員が同じプレハブで、先頭（操作しているキャラ）かどうかで次を切り替える。
    /// - 先頭: CharacterMotor（歩き・カメラ）・PlayerMeleeInput（攻撃の入力）・PlayerInteractor・PlayerHotbar（入力）を有効にする。
    /// - 後ろ: それらを止め、FollowerLocomotion（NavMeshAgent で列について歩く）と FollowerBrain（敵を見て戦う）を有効にする。
    /// CharacterController はどちらでも当たりとして残す（味方どうしは Ally レイヤーで当たらない）。
    /// キャラのルートに付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Party Member")]
    public sealed class PartyMember : MonoBehaviour
    {
        [SerializeField, Tooltip("画面に出す名前。")]
        private string displayName = "仲間";

        [SerializeField, Tooltip("最初に手持ちに入れておく物（仮配置の仲間の装備）。左の枠から入る。")]
        private ItemDefinition[] startingItems = new ItemDefinition[0];

        private bool initialized;
        private CharacterMotor motor;
        private PlayerMeleeInput meleeInput;
        private PlayerInteractor interactor;
        private PlayerHotbar hotbar;
        private PlayerInventory inventory;
        private RangedAttacker ranged;
        private NavMeshAgent agent;
        private FollowerLocomotion locomotion;
        private FollowerBrain brain;
        private CharacterProgression progression;
        private PlayerHealth health;
        private bool startingItemsGiven;

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        /// <summary>入っているパーティー。抜けていれば null。</summary>
        public Party Party { get; private set; }

        /// <summary>並びの位置（0 が先頭）。抜けていれば -1。</summary>
        public int Index { get; private set; } = -1;

        public bool IsLeader { get; private set; }

        public CharacterMotor Motor { get { Initialize(); return motor; } }
        public PlayerInventory Inventory { get { Initialize(); return inventory; } }
        public PlayerHotbar Hotbar { get { Initialize(); return hotbar; } }
        public CharacterProgression Progression { get { Initialize(); return progression; } }
        public PlayerHealth Health { get { Initialize(); return health; } }

        /// <summary>カメラが追う注視点。</summary>
        public Transform CameraTarget
        {
            get
            {
                Initialize();
                return motor != null && motor.CinemachineCameraTarget != null ? motor.CinemachineCameraTarget.transform : transform;
            }
        }

        private void Awake()
        {
            Initialize();
            GiveStartingItems();
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            motor = GetComponent<CharacterMotor>();
            meleeInput = GetComponent<PlayerMeleeInput>();
            interactor = GetComponent<PlayerInteractor>();
            hotbar = GetComponent<PlayerHotbar>();
            inventory = GetComponent<PlayerInventory>();
            ranged = GetComponent<RangedAttacker>();
            agent = GetComponent<NavMeshAgent>();
            locomotion = GetComponent<FollowerLocomotion>();
            brain = GetComponent<FollowerBrain>();
            progression = GetComponent<CharacterProgression>();
            health = GetComponent<PlayerHealth>();
        }

        private void GiveStartingItems()
        {
            if (startingItemsGiven || inventory == null || startingItems == null) return;
            startingItemsGiven = true;

            var random = new System.Random(GetInstanceID());
            foreach (ItemDefinition definition in startingItems)
            {
                if (definition != null) inventory.Inventory.TryAdd(ItemInstance.Create(definition, random));
            }
        }

        /// <summary>party の index 番目に入った（並びが変わるたびにも呼ばれる）。</summary>
        public void Join(Party party, int index)
        {
            Initialize();
            Party = party;
            Index = index;
            if (inventory != null) inventory.Bag = party.Bag;
        }

        /// <summary>パーティーを抜けた。その場に立ち止まる。</summary>
        public void Leave()
        {
            Initialize();
            SetLeader(false, null);
            Party = null;
            Index = -1;
            if (inventory != null) inventory.Bag = null;
            if (locomotion != null) locomotion.enabled = false;
            if (brain != null) brain.enabled = false;
        }

        /// <summary>先頭（操作するキャラ）にするか、後ろについてくる側にするか。input は先頭の歩きが読む入力。</summary>
        public void SetLeader(bool leader, CharacterInput input)
        {
            Initialize();
            IsLeader = leader;

            // 止める側を先に止めてから、使う側を有効にする（攻撃の狙いや持ち替えの登録が二重にならないように）。
            if (leader)
            {
                SetEnabled(brain, false);
                SetEnabled(locomotion, false);
                if (agent != null) agent.enabled = false;

                if (motor != null) motor.Input = input;
                if (ranged != null) ranged.ShowAimRing = true;
                SetEnabled(motor, true);
                SetEnabled(hotbar, true);
                SetEnabled(meleeInput, true);
                SetEnabled(interactor, true);
            }
            else
            {
                SetEnabled(meleeInput, false);
                SetEnabled(interactor, false);
                SetEnabled(hotbar, false);
                SetEnabled(motor, false);

                if (ranged != null) ranged.ShowAimRing = false;
                SetEnabled(locomotion, Party != null);
                SetEnabled(brain, Party != null);
            }
        }

        /// <summary>position（の近くの NavMesh 上）へ跳ばす。後ろの仲間の並び直しに使う。</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            Initialize();
            if (locomotion != null && locomotion.isActiveAndEnabled)
            {
                locomotion.Warp(position, rotation);
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
        }

        private static void SetEnabled(Behaviour behaviour, bool value)
        {
            if (behaviour != null && behaviour.enabled != value) behaviour.enabled = value;
        }
    }
}
