using System.Collections.Generic;
using TpsDungeon.Items;
using TpsDungeon.Map.Data;
using TpsDungeon.Map.Runtime;
using TpsDungeon.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Party
{
    /// <summary>
    /// オートセーブ。ラン中の状態（居場所・並び・持ち物、<see cref="RunSaveData"/>）を run.json に書き、起動したときに戻す。
    /// <see cref="GameFlow"/> が自分の GameObject に付け、地上で始めたときだけ動かす（確認用シーンから始めた状態でセーブを上書きしないように）。
    /// 書くとき:
    /// - シーンに着いたとき（GameFlow から <see cref="SaveNow"/>）。階なら、上り階段で前の階を抜けて着いたところ。
    /// - 階の部屋を全部攻略したとき。
    /// - 地上・死後の場所で、魂の強化を買ったとき（並びの数などで名簿が変わるので。続けて買っても 1 フレームに 1 回にまとめる）。
    ///   持ち物や並びを変えただけでは書かない（終了・中断のときか、次に移ったときに書く）。
    /// - 終了・中断（アプリが裏へ回る・フォーカスを失う）のとき。
    /// 階の中では持ち物が変わっても書かない（再開はその階の入口から）。シーンを移っている途中は書かない。
    /// レベル・経験値・HP は PartyProgression（progress.json）に、書くたびに同じ時点で書かせる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSave : MonoBehaviour
    {
        private GameFlow flow;
        private Party party;
        private SoulUpgradeWorld upgrades;
        private bool dirty;

        /// <summary>
        /// オートセーブで書いたとき（HUD の「セーブ中」の表示用）。終了・中断・フォーカスを失ったときの保存では出さない。
        /// </summary>
        public static event System.Action AutoSaved;

        // ドメインリロードを切っているので、再生のたびに前回の購読を捨てる。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => AutoSaved = null;

        /// <summary>
        /// 書いてよいか。起動時にセーブを戻し終えるまでは偽（戻す前の初期状態で上書きしないように。
        /// エディタが背面のまま Play に入ると、フォーカスを失った知らせが戻すより先に来る）。GameFlow が立てる。
        /// </summary>
        public bool Ready { get; set; }

        /// <summary>全滅の演出の途中か。書くときに印を残し、次に始めたら全滅として扱う（閉じて死を逃れられないように）。</summary>
        public bool WipePending { get; set; }

        public void Initialize(GameFlow owner, Party target, SoulUpgradeWorld world)
        {
            flow = owner;
            party = target;
            upgrades = world;
        }

        private void OnEnable()
        {
            RoomEnemySpawner.AnyAllRoomsCleared += OnAllRoomsCleared;
            MetaUpgrades.Changed += MarkDirty;
        }

        private void OnDisable()
        {
            RoomEnemySpawner.AnyAllRoomsCleared -= OnAllRoomsCleared;
            MetaUpgrades.Changed -= MarkDirty;
        }

        private void Update()
        {
            if (!dirty || !InTown || !CanSave) return;
            SaveNow();
        }

        private void OnApplicationQuit() => SaveNow(false);

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) SaveNow(false);
        }

        /// <summary>今居るのが地上か死後の場所か（持ち物が変わるたびに書く所）。</summary>
        private static bool InTown
        {
            get
            {
                string scene = SceneManager.GetActiveScene().name;
                return scene == SceneTravel.SurfaceScene || scene == SceneTravel.AfterlifeScene;
            }
        }

        private bool CanSave => Ready && isActiveAndEnabled && party != null && (flow == null || !flow.IsTraveling);

        private void MarkDirty() => dirty = true;

        private void OnAllRoomsCleared(RoomEnemySpawner _) => SaveNow();

        /// <summary>最初に居た仲間と、今の並び（重ねずに）。</summary>
        private List<PartyMember> Members()
        {
            var result = new List<PartyMember>();
            if (upgrades != null)
            {
                foreach (PartyMember member in upgrades.Roster)
                {
                    if (member != null && !result.Contains(member)) result.Add(member);
                }
            }
            if (party != null)
            {
                foreach (PartyMember member in party.Members)
                {
                    if (member != null && !result.Contains(member)) result.Add(member);
                }
            }
            return result;
        }

        // ---- 書く ------------------------------------------------------------

        /// <summary>今の状態を書く。今のシーンを居場所にする（階なら今の階）。移っている途中・止めてあるときは何もしない。</summary>
        public void SaveNow() => SaveNow(true);

        /// <param name="notify">オートセーブとして知らせるか（<see cref="AutoSaved"/>）。終了・中断のときは知らせない。</param>
        private void SaveNow(bool notify)
        {
            if (!CanSave) return;
            string scene = SceneManager.GetActiveScene().name;
            Write(scene, IsFloorScene(scene) ? DungeonProgress.Current : null, notify);
        }

        /// <summary>居場所を直に決めて書く（全滅でソウルに変えた直後に、移っている途中でも死後の場所として書くため）。</summary>
        public void SaveAt(string scene, DungeonFloor? floor = null)
        {
            if (!Ready || !isActiveAndEnabled || party == null) return;
            Write(scene, floor, true);
        }

        private void Write(string scene, DungeonFloor? floor, bool notify)
        {
            dirty = false;
            RunSaveStore.Save(Capture(scene, floor));
            PartyProgression.Current?.Save();
            UniqueDropPityStore.Save();
            if (notify) AutoSaved?.Invoke();
        }

        private static bool IsFloorScene(string scene) =>
            scene != SceneTravel.SurfaceScene && scene != SceneTravel.AfterlifeScene && DungeonProgress.Current.HasValue;

        public RunSaveData Capture(string scene, DungeonFloor? floor)
        {
            var data = new RunSaveData { scene = scene, Floor = floor, wipePending = WipePending };

            foreach (PartyMember member in party.Members)
            {
                string id = IdOf(member);
                if (id != null) data.order.Add(id);
            }

            foreach (PartyMember member in Members())
            {
                string id = IdOf(member);
                if (id == null || member.Inventory == null || data.FindHands(id) != null) continue;
                data.hands.Add(new MemberItemsSaveData { id = id, slots = ItemSaveData.FromSlots(member.Inventory.Inventory) });
            }

            data.bag = ItemSaveData.FromSlots(party.Bag);
            return data;
        }

        private static string IdOf(PartyMember member) =>
            member != null && member.Progression != null && !string.IsNullOrEmpty(member.Progression.ProgressId) ? member.Progression.ProgressId : null;

        // ---- 戻す ------------------------------------------------------------

        /// <summary>
        /// 並びと持ち物を戻す（起動したとき 1 回、GameFlow から）。居場所へ移すのは GameFlow。
        /// 魂の強化で決まる人数とバッグの枠が先に当たっているように、SoulUpgradeWorld.Start のあとに呼ぶ。
        /// </summary>
        public void Restore(RunSaveData data)
        {
            if (data == null || party == null) return;

            if (upgrades != null) upgrades.ApplyOrder(data.order);

            ItemCatalog catalog = ItemCatalog.Default;
            if (catalog == null) Debug.LogWarning($"アイテムの id の表（Resources/{ItemCatalog.ResourcePath}）が無いので、持ち物は戻せない");

            foreach (PartyMember member in Members())
            {
                string id = IdOf(member);
                MemberItemsSaveData hands = id != null ? data.FindHands(id) : null;
                if (hands == null || member.Inventory == null) continue;
                ItemSaveData.RestoreSlots(member.Inventory.Inventory, hands.slots, catalog);
            }

            ItemSaveData.RestoreSlots(party.Bag, data.bag, catalog);
            dirty = false;
        }
    }
}
