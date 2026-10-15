using UnityEngine;
using UnityEngine.InputSystem;
using TpsDungeon.Map.Runtime;

namespace TpsDungeon.Map.DebugTools
{
    /// <summary>
    /// 階を確かめるためのキー入力。エディタと開発ビルドでだけ動き、製品版では何もしない。結果はコンソールに出す。
    /// 下の階へ移る（普段は下へ戻れない）・上の階へ飛ぶ・ボス部屋の閉ざされた扉を開けられるようにする。
    /// 階のシーンの FloorBootstrap と同じ GameObject に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Floor Debug Input")]
    public sealed class FloorDebugInput : MonoBehaviour
    {
        // 製品版では Update ごと消えるので、設定の欄が読まれないという警告を黙らせる。
#pragma warning disable CS0414, CS0649
        [SerializeField, Tooltip("操作する階。未設定なら同じ GameObject かシーンから探す。")]
        private FloorBootstrap bootstrap;

        [SerializeField, Tooltip("1 つ下の階へ移る（1 層 1 階なら地上へ）。")]
        private Key downKey = Key.F9;

        [SerializeField, Tooltip("1 つ上の階へ移る。")]
        private Key upKey = Key.F10;

        [SerializeField, Tooltip("閉ざされた扉（ボス部屋の奥の出口の扉など）を開けられるようにする。")]
        private Key unlockKey = Key.F11;
#pragma warning restore CS0414, CS0649

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Awake()
        {
            if (bootstrap == null) bootstrap = GetComponent<FloorBootstrap>();
            if (bootstrap == null) bootstrap = FindFirstObjectByType<FloorBootstrap>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || bootstrap == null) return;

            if (keyboard[downKey].wasPressedThisFrame) GoDown();
            if (keyboard[upKey].wasPressedThisFrame) GoUp();
            if (keyboard[unlockKey].wasPressedThisFrame) Unlock();
        }
#endif

        /// <summary>1 つ下の階へ。1 層 1 階なら地上へ。</summary>
        public void GoDown()
        {
            var route = bootstrap.Route;
            if (route == null || !bootstrap.CurrentFloor.HasValue)
            {
                Debug.Log("[Dungeon] 道順に載っていない階なので、下の階が分からない");
                return;
            }

            var previous = route.Previous(bootstrap.CurrentFloor.Value);
            if (previous.HasValue)
            {
                Debug.Log($"[Dungeon] デバッグ: {route.Label(previous.Value)} へ下りる");
                bootstrap.GoToFloor(previous.Value);
            }
            else
            {
                Debug.Log("[Dungeon] デバッグ: 地上へ下りる");
                SceneTravel.Go(SceneTravel.SurfaceScene);
            }
        }

        /// <summary>1 つ上の階へ。最後の階なら何もしない。</summary>
        public void GoUp()
        {
            var route = bootstrap.Route;
            var next = route != null && bootstrap.CurrentFloor.HasValue ? route.Next(bootstrap.CurrentFloor.Value) : null;
            if (!next.HasValue)
            {
                Debug.Log("[Dungeon] デバッグ: この上の階は無い");
                return;
            }

            Debug.Log($"[Dungeon] デバッグ: {route.Label(next.Value)} へ上がる");
            bootstrap.GoToFloor(next.Value);
        }

        /// <summary>閉ざされた扉を開けられるようにする。</summary>
        public void Unlock()
        {
            var builder = bootstrap.GetComponent<FloorBuilder>();
            int count = builder != null ? builder.LockedDoors.Count : 0;
            if (builder != null) builder.UnlockDoors();
            Debug.Log($"[Dungeon] デバッグ: 閉ざされた扉を開けられるようにした（{count} 枚）");
        }
    }
}
