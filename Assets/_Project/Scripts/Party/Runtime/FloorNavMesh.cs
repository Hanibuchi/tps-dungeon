using System;
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
    /// - 扉は開け閉めで通れるかが変わるので、焼くときは含めず、閉じている間だけ NavMeshObstacle で道を切る（<see cref="DoorNavObstacle"/>）。
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
            surface.layerMask = AllyLayer.Exclude(layers);
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
                if (!door.TryGetComponent(out NavMeshModifier modifier)) modifier = door.gameObject.AddComponent<NavMeshModifier>();
                modifier.ignoreFromBuild = true;
                if (!door.TryGetComponent(out DoorNavObstacle _)) door.gameObject.AddComponent<DoorNavObstacle>();
            }

            // 生成したばかりの物の当たりを、焼く前に物理へ反映させる。
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            Baked?.Invoke();
        }

        // ドメインリロードを切っているので、再生のたびに前回の受け手を捨てる。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Baked = null;
    }
}
