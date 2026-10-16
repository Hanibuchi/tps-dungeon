using System;
using TpsDungeon.Enemies;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Player;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace TpsDungeon.Party
{
    /// <summary>
    /// フロアを生成し終えたら（FloorBuilder.Built）、その床と壁から NavMesh を焼く。パーティーの後ろの仲間が歩くのに使う。
    /// - 当たり（PhysicsColliders）から焼き、パーティーのメンバー（Ally レイヤー）は含めない。
    /// - 高さメッシュも焼く。NavMesh はボクセルで近似されて床より少し上にできるので、それだけだと agent が浮いて見える。
    /// - 扉の板は開け閉めで通れるかが変わるので、焼くときは含めず、閉じている間だけ NavMeshObstacle で道を切る（<see cref="DoorNavObstacle"/>）。
    ///   外すのは動く扉板（Hinge の下）と照準用のトリガーだけ。柱と鴨居は壁と同じく焼く（扉ごと外すと柱の所が通れてしまい、敵が壁を抜けてくる）。
    /// - 天井（FloorBuilder.CeilingRoot）は焼かない。上面に届かない歩ける所ができるだけなので。
    /// 焼き終えたら <see cref="Baked"/> を知らせる（パーティーが全員を先頭の後ろへ並べ直す）。
    /// FloorBuilder と同じ GameObject に付ける。NavMeshSurface はここで足す。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FloorBuilder))]
    [AddComponentMenu("TPS Dungeon/Floor NavMesh")]
    public sealed class FloorNavMesh : MonoBehaviour
    {
        [SerializeField, Tooltip("焼くときに見るレイヤー（味方は除く）。")]
        private LayerMask layers = ~0;

        private FloorBuilder builder;
        private NavMeshSurface surface;

        /// <summary>どれかのフロアの NavMesh を焼き終えた。</summary>
        public static event Action Baked;

        /// <summary>最後に焼き終えてからの、焼けているか。</summary>
        public bool HasNavMesh => surface != null && surface.navMeshData != null;

        private void Awake()
        {
            builder = GetComponent<FloorBuilder>();
            surface = GetComponent<NavMeshSurface>();
            if (surface == null) surface = gameObject.AddComponent<NavMeshSurface>();

            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // 味方と、すり抜けられる壊せる草花（Breakable）は焼かない。
            surface.layerMask = BreakableLayer.Exclude(AllyLayer.Exclude(layers));
            surface.buildHeightMesh = true;
        }

        private void OnEnable()
        {
            builder.Built += OnBuilt;
            // このコンポーネントより先に生成が済んでいたら、ここで焼く。
            if (builder.GeneratedRoot != null && builder.CurrentLayout != null) Bake();
        }

        private void OnDisable()
        {
            builder.Built -= OnBuilt;
        }

        private void OnBuilt(FloorLayout _) => Bake();

        /// <summary>今のフロアの NavMesh を焼き直す。</summary>
        public void Bake()
        {
            if (builder.GeneratedRoot == null) return;

            foreach (Door door in builder.GeneratedRoot.GetComponentsInChildren<Door>(true))
            {
                foreach (Transform hinge in door.Hinges) IgnoreFromBuild(hinge.gameObject);
                foreach (Collider c in door.GetComponentsInChildren<Collider>(true))
                {
                    if (c.isTrigger) IgnoreFromBuild(c.gameObject);
                }
                if (!door.TryGetComponent(out DoorNavObstacle _)) door.gameObject.AddComponent<DoorNavObstacle>();
            }

            // 天井は上面が平らで広いので、焼くとその上に歩ける所ができてしまう。
            if (builder.CeilingRoot != null) IgnoreFromBuild(builder.CeilingRoot.gameObject);

            // 生成したばかりの物の当たりを、焼く前に物理へ反映させる。
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            Baked?.Invoke();
        }

        private static void IgnoreFromBuild(GameObject target)
        {
            if (!target.TryGetComponent(out NavMeshModifier modifier)) modifier = target.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
        }

        // ドメインリロードを切っているので、再生のたびに前回の受け手を捨てる。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Baked = null;
    }
}
