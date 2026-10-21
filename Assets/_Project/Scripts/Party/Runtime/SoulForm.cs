using System.Collections.Generic;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 死後の場所（Afterlife）にいる間だけ、パーティーの全員を魂の姿にする。Afterlife のシーンに 1 つ置く。
    /// - 体と小物（角・耳・しっぽ）の材質を魂の材質（TpsDungeon/Soul）に差し替え、手に持った武器は隠す（魂は武器を持たない）。
    /// - 持ち替えや仲間の入れ替えで増えた物も拾えるよう、少しおきに見直す。
    /// - 一人ずつに弱い青白い光を付けて歩かせる。暗い死後の場所では、これが主な光になる。
    /// - パーティーは地上から持ち越されて次のシーンへも行くので、このシーンを出るとき（OnDestroy）に材質も武器も光も元へ戻す。
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
        private float nextScan;

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + rescanInterval;

            Party party = Party.Current;
            if (party == null || soulMaterial == null) return;

            foreach (PartyMember member in party.Members)
            {
                if (member == null) continue;
                Apply(member);
                EnsureLight(member);
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
        }
    }
}
