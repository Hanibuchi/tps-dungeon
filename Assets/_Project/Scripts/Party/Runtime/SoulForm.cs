using System.Collections.Generic;
using TpsDungeon.Combat;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 死後の場所（Afterlife）にいる間だけ、パーティーの先頭を魂の姿にする。Afterlife のシーンに 1 つ置く。
    /// - 死後の場所に来るのは操作しているキャラ（先頭）だけで、仲間は消しておく（GameObject を止める）。先頭が替わったら、新しい先頭を出して前の先頭を消す。
    /// - 体と小物（角・耳・しっぽ）の材質を魂の材質（TpsDungeon/Soul）に差し替え、手に持った武器は隠す（魂は武器を持たない）。
    /// - 攻撃と狙い（攻撃の入力・近接と遠距離の攻撃・肩越しのカメラ）も止める。遠距離武器を持ったままだと、狙いに合わせて体がカメラの向きへ回り続け、
    ///   着いたときに北（強化の台）を向かせても、すぐカメラの向きに戻ってしまう。
    /// - 構えは素手にする（Animator の WeaponType を 0 に）。弓などを持ったまま倒れると、その構えのまま固まって見えるため。
    ///   持ち替えると攻撃の部品が構えを入れ直すので毎フレーム 0 に戻し、出るときに持っている物の構えへ戻す。
    /// - 持ち替えや仲間の入れ替えで増えた物も拾えるよう、少しおきに見直す。
    /// - 一人ずつに弱い青白い光を付けて歩かせる。暗い死後の場所では、これが主な光になる。
    /// - パーティーは地上から持ち越されて次のシーンへも行くので、このシーンを出るとき（OnDestroy）に材質も武器も光も仲間も元へ戻す。
    ///   仲間は止めていた所に出るが、着いたシーンで GameFlow が先頭の後ろへ並べ直す。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Soul Form")]
    public sealed class SoulForm : MonoBehaviour
    {
        [SerializeField, Tooltip("魂の材質（TpsDungeon/Soul）。")]
        private Material soulMaterial;

        [Header("魂の光")]
        [SerializeField, Tooltip("一人ずつに付ける光の色。")]
        private Color lightColor = new Color(0.55f, 0.9f, 1f);

        [SerializeField, Min(0f), Tooltip("光の強さ。")]
        private float lightIntensity = 1.6f;

        [SerializeField, Min(0f), Tooltip("光の届く距離（m）。")]
        private float lightRange = 6f;

        [SerializeField, Tooltip("光を置く高さ（キャラの足元から、m）。")]
        private float lightHeight = 1.1f;

        [SerializeField, Min(0.05f), Tooltip("増えた物（持ち替えた武器・入った仲間）を見直す間隔（秒）。")]
        private float rescanInterval = 0.25f;

        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private readonly HashSet<Renderer> hidden = new HashSet<Renderer>();
        private readonly Dictionary<PartyMember, Light> lights = new Dictionary<PartyMember, Light>();
        private readonly List<Renderer> scratch = new List<Renderer>();
        private readonly HashSet<PartyMember> away = new HashSet<PartyMember>();
        private readonly HashSet<Behaviour> stopped = new HashSet<Behaviour>();
        private readonly List<Behaviour> behaviourScratch = new List<Behaviour>();
        private float nextScan;

        private static readonly int WeaponTypeParam = Animator.StringToHash("WeaponType");
        private static readonly int CastingParam = Animator.StringToHash("Casting");
        private readonly Dictionary<Animator, (bool, bool)> parameterCache = new Dictionary<Animator, (bool, bool)>();

        // シーンを読み込んだとき（GameFlow が先頭を着地点へ置くより前）に 1 度済ませる。
        // 遅れると、着いたフレームに遠距離武器の狙いがまだ前の場所のカメラの向きへ体を回してしまい、北を向かせた向きが崩れる。
        private void Awake() => Scan();

        private void LateUpdate()
        {
            ForceUnarmedPose();
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + rescanInterval;
            Scan();
        }

        private void Scan()
        {
            Party party = Party.Current;
            if (party == null || soulMaterial == null) return;

            PartyMember leader = party.Leader;
            foreach (PartyMember member in party.Members)
            {
                if (member == null) continue;
                Apply(member);
                StopCombat(member);
                EnsureLight(member);
                ShowOnlyLeader(member, member == leader);
            }
        }

        /// <summary>攻撃と狙いの部品を止める。先頭が替わると入力が付け直されるので、見直すたびに止め直す。</summary>
        private void StopCombat(PartyMember member)
        {
            behaviourScratch.Clear();
            behaviourScratch.Add(member.GetComponent<PlayerMeleeInput>());
            behaviourScratch.Add(member.GetComponent<MeleeAttacker>());
            behaviourScratch.Add(member.GetComponent<RangedAttacker>());
            behaviourScratch.Add(member.GetComponent<RangedShoulderCamera>());
            foreach (Behaviour behaviour in behaviourScratch)
            {
                if (behaviour == null || !behaviour.enabled) continue;
                behaviour.enabled = false;
                stopped.Add(behaviour);
            }
        }

        /// <summary>全員の構えを素手にする（杖を構えている途中なら構えも解く）。</summary>
        private void ForceUnarmedPose()
        {
            Party party = Party.Current;
            if (party == null) return;
            foreach (PartyMember member in party.Members)
            {
                if (member == null || !member.isActiveAndEnabled) continue;
                Animator animator = member.GetComponentInChildren<Animator>();
                if (animator == null || animator.runtimeAnimatorController == null) continue;
                (bool weaponType, bool casting) = ParametersOf(animator);
                if (weaponType) animator.SetInteger(WeaponTypeParam, 0);
                if (casting) animator.SetBool(CastingParam, false);
            }
        }

        /// <summary>出るときに、持っている物の構えへ戻す。</summary>
        private void RestorePose()
        {
            Party party = Party.Current;
            if (party == null) return;
            foreach (PartyMember member in party.Members)
            {
                if (member == null) continue;
                Animator animator = member.GetComponentInChildren<Animator>(true);
                var melee = member.GetComponent<MeleeAttacker>();
                if (animator == null || melee == null || animator.runtimeAnimatorController == null) continue;
                if (ParametersOf(animator).weaponType) animator.SetInteger(WeaponTypeParam, melee.AnimatorWeaponType);
            }
        }

        /// <summary>animator に WeaponType（int）と Casting（bool）があるか。parameters は読むたびに配列を作るので、1 度だけ調べて覚える。</summary>
        private (bool weaponType, bool casting) ParametersOf(Animator animator)
        {
            if (parameterCache.TryGetValue(animator, out (bool, bool) known)) return known;

            bool weaponType = false;
            bool casting = false;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == WeaponTypeParam && p.type == AnimatorControllerParameterType.Int) weaponType = true;
                else if (p.nameHash == CastingParam && p.type == AnimatorControllerParameterType.Bool) casting = true;
            }
            parameterCache[animator] = (weaponType, casting);
            return (weaponType, casting);
        }

        private void ShowOnlyLeader(PartyMember member, bool isLeader)
        {
            if (isLeader)
            {
                if (away.Remove(member)) member.gameObject.SetActive(true);
            }
            else if (member.gameObject.activeSelf)
            {
                away.Add(member);
                member.gameObject.SetActive(false);
            }
        }

        private void Apply(PartyMember member)
        {
            var animator = member.GetComponentInChildren<Animator>();
            Transform leftHand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            Transform rightHand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;

            member.GetComponentsInChildren(true, scratch);
            foreach (Renderer renderer in scratch)
            {
                // 手の下にある物は持っている武器（盾も）。ランクのオーラの粒も一緒に隠す。
                if ((leftHand != null && renderer.transform.IsChildOf(leftHand)) || (rightHand != null && renderer.transform.IsChildOf(rightHand)))
                {
                    if (renderer.enabled && hidden.Add(renderer)) renderer.enabled = false;
                    continue;
                }

                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;

                if (originals.ContainsKey(renderer)) continue;

                Material[] materials = renderer.sharedMaterials;
                originals.Add(renderer, materials);
                var souls = new Material[materials.Length];
                for (int i = 0; i < souls.Length; i++) souls[i] = soulMaterial;
                renderer.sharedMaterials = souls;
            }
        }

        private void EnsureLight(PartyMember member)
        {
            if (lights.TryGetValue(member, out Light existing) && existing != null) return;

            var holder = new GameObject("SoulLight");
            holder.transform.SetParent(member.transform, false);
            holder.transform.localPosition = new Vector3(0f, lightHeight, 0f);
            var light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightColor;
            light.intensity = lightIntensity;
            light.range = lightRange;
            light.shadows = LightShadows.None;
            lights[member] = light;
        }

        private void OnDestroy()
        {
            RestorePose();

            foreach (KeyValuePair<Renderer, Material[]> pair in originals)
            {
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            }
            originals.Clear();

            foreach (Renderer renderer in hidden)
            {
                if (renderer != null) renderer.enabled = true;
            }
            hidden.Clear();

            foreach (Light light in lights.Values)
            {
                if (light != null) Destroy(light.gameObject);
            }
            lights.Clear();

            // 攻撃の入力は先頭だけが持つ（死後の場所で先頭を替えていたら、前の先頭の入力は戻さない）。
            PartyMember leader = Party.Current != null ? Party.Current.Leader : null;
            foreach (Behaviour behaviour in stopped)
            {
                if (behaviour == null) continue;
                behaviour.enabled = !(behaviour is PlayerMeleeInput) || (leader != null && behaviour.gameObject == leader.gameObject);
            }
            stopped.Clear();

            foreach (PartyMember member in away)
            {
                if (member != null) member.gameObject.SetActive(true);
            }
            away.Clear();
        }
    }
}
