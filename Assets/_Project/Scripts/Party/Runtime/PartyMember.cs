using TpsDungeon.Combat;
using TpsDungeon.Enemies;
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
    /// HP が 0 になったら倒れる（<see cref="IsDown"/>）。その場でラグドールで倒れ込み、操作も列について歩くのも戦うのも止めて置いていかれる。
    /// 並びを後ろへ回すのはパーティー（<see cref="Party"/>）。起きるのは転生で HP を戻したとき（<see cref="Revive"/>）だけ。
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

        [SerializeField, Min(0f), Tooltip("倒れるときに後ろへ押す速さ（m/s）。")]
        private float fallPush = 1.5f;

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
        private MeleeAttacker melee;
        private EnemyRagdoll ragdoll;
        private Animator animator;
        private int[] boneLayers;
        private Quaternion standingRotation;
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

        /// <summary>倒れている（HP 0）。転生で起こすまでそのまま。</summary>
        public bool IsDown { get; private set; }

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

        private void OnEnable()
        {
            Initialize();
            if (health != null) health.Changed += OnHealthChanged;
            SyncDown();
        }

        private void OnDisable()
        {
            if (health != null) health.Changed -= OnHealthChanged;
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
            melee = GetComponent<MeleeAttacker>();
            ragdoll = GetComponent<EnemyRagdoll>();
            animator = GetComponentInChildren<Animator>();
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

            // 倒れている間は先頭でも後ろでも何も動かさない（先頭が替わったとき、倒れた前の先頭に列の歩きが付かないように）。
            if (IsDown)
            {
                StopAll();
                return;
            }

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

        /// <summary>HP を全快にして起こす（転生のとき）。役（先頭か）はパーティーが当て直す。</summary>
        public void Revive()
        {
            Initialize();
            if (health != null) health.SetCurrent(health.MaxHp);
            SyncDown();
        }

        private void OnHealthChanged(PlayerHealth _) => SyncDown();

        private void SyncDown()
        {
            bool down = health != null && health.IsDead;
            if (down == IsDown) return;

            if (down) FallDown();
            else StandUp();
            Party?.OnMemberDownChanged(this);
        }

        private void FallDown()
        {
            IsDown = true;
            StopAll();

            if (ragdoll != null && ragdoll.Bodies.Count > 0)
            {
                // 骨は死体のレイヤーへ。床と壁にだけ当たり、生きている仲間の歩きや敵の狙いに引っかからない。
                int debris = DebrisLayer.Index;
                boneLayers = new int[ragdoll.Bodies.Count];
                for (int i = 0; i < boneLayers.Length; i++)
                {
                    Rigidbody body = ragdoll.Bodies[i];
                    if (body == null) continue;
                    boneLayers[i] = body.gameObject.layer;
                    if (debris >= 0) body.gameObject.layer = debris;
                }
                ragdoll.Fall(-transform.forward * fallPush);
            }
            else
            {
                // 骨の体がまだ焼かれていないプレハブ。仰向けに寝かせるだけ。
                standingRotation = transform.rotation;
                if (animator != null) animator.enabled = false;
                if (TryGetComponent(out CharacterController controller)) controller.enabled = false;
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Quaternion.Euler(-90f, 0f, 0f);
                transform.position += Vector3.up * 0.15f;
            }
        }

        private void StandUp()
        {
            IsDown = false;

            if (ragdoll != null && ragdoll.Bodies.Count > 0)
            {
                ragdoll.GetUp();
                if (boneLayers != null)
                {
                    for (int i = 0; i < boneLayers.Length && i < ragdoll.Bodies.Count; i++)
                    {
                        if (ragdoll.Bodies[i] != null) ragdoll.Bodies[i].gameObject.layer = boneLayers[i];
                    }
                }
            }
            else
            {
                transform.position -= Vector3.up * 0.15f;
                transform.rotation = standingRotation;
                if (animator != null) animator.enabled = true;
                if (TryGetComponent(out CharacterController controller)) controller.enabled = true;
            }

            SetEnabled(melee, true);
            SetEnabled(ranged, true);
        }

        /// <summary>操作・列の歩き・戦い（攻撃の部品も）を全部止める。</summary>
        private void StopAll()
        {
            SetEnabled(meleeInput, false);
            SetEnabled(interactor, false);
            SetEnabled(hotbar, false);
            SetEnabled(motor, false);
            SetEnabled(brain, false);
            SetEnabled(locomotion, false);
            if (agent != null) agent.enabled = false;
            if (ranged != null) ranged.ShowAimRing = false;
            SetEnabled(melee, false);
            SetEnabled(ranged, false);
        }

        private static void SetEnabled(Behaviour behaviour, bool value)
        {
            if (behaviour != null && behaviour.enabled != value) behaviour.enabled = value;
        }
    }
}
