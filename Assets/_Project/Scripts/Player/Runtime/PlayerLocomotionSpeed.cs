using System.Reflection;
using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// ThirdPersonController の歩き・走りの速さ（MoveSpeed / SprintSpeed）と速さの追従（SpeedChangeRate）を書く役をまとめたもの。
    /// 最初の値を基準に覚え、装備の倍率（<see cref="Multiplier"/>、移動速度のエンチャント）と、
    /// ダッシュ突きで走っている間の止め（<see cref="Locked"/>、PlayerMeleeInput）から毎回書き直す。
    /// 止めと倍率を別々に覚え直さないので、走っている最中に装備が変わっても、走り終えたときに古い速さへ戻すことはない。
    /// ThirdPersonController は名前で探してフィールドを名前で書くので、Starter Assets のアセンブリには依存しない（GamePauser と同じ）。
    /// プレイヤーのルート（ThirdPersonController と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Locomotion Speed")]
    public sealed class PlayerLocomotionSpeed : MonoBehaviour
    {
        private const string LocomotionTypeName = "ThirdPersonController";
        private const string MoveSpeedField = "MoveSpeed";
        private const string SprintSpeedField = "SprintSpeed";
        private const string SpeedChangeRateField = "SpeedChangeRate";

        /// <summary>止めている間の速さの追従。一瞬で 0 へ落とす。</summary>
        private const float LockedSpeedChangeRate = 1e6f;

        private Component locomotion;
        private FieldInfo moveSpeed;
        private FieldInfo sprintSpeed;
        private FieldInfo speedChangeRate;
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

            foreach (Component component in GetComponents<Component>())
            {
                if (component == null || component.GetType().Name != LocomotionTypeName) continue;

                FieldInfo move = FloatField(component, MoveSpeedField);
                FieldInfo sprint = FloatField(component, SprintSpeedField);
                FieldInfo rate = FloatField(component, SpeedChangeRateField);
                if (move == null || sprint == null || rate == null) return;

                locomotion = component;
                moveSpeed = move;
                sprintSpeed = sprint;
                speedChangeRate = rate;
                baseMoveSpeed = (float)move.GetValue(component);
                baseSprintSpeed = (float)sprint.GetValue(component);
                baseSpeedChangeRate = (float)rate.GetValue(component);
                return;
            }
        }

        private static FieldInfo FloatField(Component component, string fieldName)
        {
            FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            return field != null && field.FieldType == typeof(float) ? field : null;
        }

        private void Apply()
        {
            FindLocomotion();
            if (locomotion == null) return;

            moveSpeed.SetValue(locomotion, locked ? 0f : baseMoveSpeed * multiplier);
            sprintSpeed.SetValue(locomotion, locked ? 0f : baseSprintSpeed * multiplier);
            speedChangeRate.SetValue(locomotion, locked ? LockedSpeedChangeRate : baseSpeedChangeRate);
        }
    }
}
