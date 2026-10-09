using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// CharacterMotor の歩き・走りの速さ（MoveSpeed / SprintSpeed）と速さの追従（SpeedChangeRate）を書く役をまとめたもの。
    /// 最初の値を基準に覚え、装備の倍率（<see cref="Multiplier"/>、移動速度のエンチャント）と、
    /// ダッシュ突きで走っている間の止め（<see cref="Locked"/>、PlayerMeleeInput）から毎回書き直す。
    /// 止めと倍率を別々に覚え直さないので、走っている最中に装備が変わっても、走り終えたときに古い速さへ戻すことはない。
    /// 後ろの仲間（NavMeshAgent で歩く）は <see cref="Multiplier"/> を読んで歩く速さに掛ける。
    /// キャラのルート（CharacterMotor と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Locomotion Speed")]
    public sealed class PlayerLocomotionSpeed : MonoBehaviour
    {
        /// <summary>止めている間の速さの追従。一瞬で 0 へ落とす。</summary>
        private const float LockedSpeedChangeRate = 1e6f;

        private CharacterMotor locomotion;
        private float baseMoveSpeed;
        private float baseSprintSpeed;
        private float baseSpeedChangeRate;

        private float multiplier = 1f;
        private bool locked;

        /// <summary>歩き・走りの速さに掛ける倍率（1 で基準のまま）。</summary>
        public float Multiplier
        {
            get => multiplier;
            set
            {
                value = Mathf.Max(0f, value);
                if (value.Equals(multiplier)) return;
                multiplier = value;
                Apply();
            }
        }

        /// <summary>真の間は歩き・走りの速さを 0 にし、入力で動かないようにする（ダッシュ突きで走っている間）。</summary>
        public bool Locked
        {
            get => locked;
            set
            {
                if (value == locked) return;
                locked = value;
                Apply();
            }
        }

        private void Awake() => FindLocomotion();

        private void OnDisable()
        {
            locked = false;
            Apply();
        }

        private void FindLocomotion()
        {
            if (locomotion != null) return;

            locomotion = GetComponent<CharacterMotor>();
            if (locomotion == null) return;

            baseMoveSpeed = locomotion.MoveSpeed;
            baseSprintSpeed = locomotion.SprintSpeed;
            baseSpeedChangeRate = locomotion.SpeedChangeRate;
        }

        private void Apply()
        {
            FindLocomotion();
            if (locomotion == null) return;

            locomotion.MoveSpeed = locked ? 0f : baseMoveSpeed * multiplier;
            locomotion.SprintSpeed = locked ? 0f : baseSprintSpeed * multiplier;
            locomotion.SpeedChangeRate = locked ? LockedSpeedChangeRate : baseSpeedChangeRate;
        }
    }
}
