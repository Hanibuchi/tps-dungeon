using TpsDungeon.Interaction;
using TpsDungeon.Map.Authoring;
using TpsDungeon.Map.Data;
using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>ショートカットでどちらへ行くか。</summary>
    public enum ShortcutDirection
    {
        /// <summary>ボス階の井戸から、地上のその層のロープの所へ降りる。</summary>
        ToSurface = 0,
        /// <summary>地上のロープを登って、その層のボス階のショートカットの部屋へ。</summary>
        ToBossFloor = 1,
    }

    /// <summary>
    /// ボス階と地上を結ぶショートカット（ボス階はロープの垂れた底の抜けた井戸、地上は空から垂れたロープ）。
    /// その層のショートカットを解放してあれば（<see cref="ShortcutUnlocks"/>）開き、調べると行き先へ移る。解放の切り替えにはその場で追いつく。
    /// 閉じているときの案内（closedLabel）が空なら、閉じている間は触れない物として扱う。ボス階の井戸はそうしておき、
    /// 閉ざされた扉の隙間から照準が届いても調べられないようにする。
    /// 開いたとき・閉じたときの見た目は、子の GameObject を切り替えて出す（ロープの長さ・ランタンの火・井戸の底の光など）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Shortcut Point")]
    public sealed class ShortcutPoint : MonoBehaviour, IInteractable, IInteractableDetails
    {
        /// <summary>地上のロープの所のスポーン地点の名前（層は 0 始まり）。ボス階の井戸から降りるとそこに立つ。</summary>
        public static string SurfaceArrivalPoint(int layer) => $"Shortcut_L{layer + 1}";

        [SerializeField, Tooltip("どの層のショートカットか（0 始まり。第 1 層が 0）。ボス階の井戸には FloorBootstrap が入れる。")]
        private int layer = -1;

        [SerializeField, Tooltip("どちらへ行くか。ボス階の井戸は ToSurface、地上のロープは ToBossFloor。")]
        private ShortcutDirection direction = ShortcutDirection.ToSurface;

        [SerializeField, Tooltip("ダンジョンの道順。ToBossFloor のとき、その層のボス階のシーンを引くのに使う。")]
        private DungeonRoute route;

        [SerializeField, Tooltip("開いているときの案内。")]
        private string openLabel = "ロープを伝って降りる";

        [SerializeField, Tooltip("閉じているときの案内。空なら閉じている間は案内を出さず、触れない。")]
        private string closedLabel = "";

        [SerializeField, Tooltip("情報欄の見出し。{0} は層の番号（1 始まり）。")]
        private string title = "第 {0} 層への近道";

        [SerializeField, TextArea, Tooltip("情報欄の本文。{0} は層の番号（1 始まり）。")]
        private string description;

        [SerializeField, Tooltip("開いているときだけ出す物（下まで垂れたロープ・灯ったランタン・井戸の底の光など）。")]
        private GameObject[] openVisuals = new GameObject[0];

        [SerializeField, Tooltip("閉じているときだけ出す物（巻き上がったロープ・消えたランタンなど）。")]
        private GameObject[] closedVisuals = new GameObject[0];

        private bool open;

        public int Layer
        {
            get => layer;
            set
            {
                layer = value;
                Refresh();
            }
        }

        public ShortcutDirection Direction
        {
            get => direction;
            set => direction = value;
        }

        public DungeonRoute Route
        {
            get => route;
            set => route = value;
        }

        public bool IsOpen => open;

        private void OnEnable()
        {
            ShortcutUnlocks.Changed += OnUnlocksChanged;
            Refresh();
        }

        private void OnDisable() => ShortcutUnlocks.Changed -= OnUnlocksChanged;

        private void OnUnlocksChanged(int changed)
        {
            if (changed == layer) Refresh();
        }

        /// <summary>解放状態を読み直して、開き閉めと見た目を合わせる。</summary>
        public void Refresh()
        {
            open = layer >= 0 && ShortcutUnlocks.IsUnlocked(layer) && !string.IsNullOrEmpty(Destination());
            foreach (var visual in openVisuals)
            {
                if (visual != null) visual.SetActive(open);
            }
            foreach (var visual in closedVisuals)
            {
                if (visual != null) visual.SetActive(!open);
            }
        }

        /// <summary>行き先のシーン名。分からなければ null。</summary>
        private string Destination()
        {
            if (direction == ShortcutDirection.ToSurface) return SceneTravel.SurfaceScene;
            var floor = BossFloor();
            return floor.HasValue ? route.SceneOf(floor.Value) : null;
        }

        /// <summary>その層のボス階。道順に無ければ null。</summary>
        private DungeonFloor? BossFloor()
        {
            if (route == null || layer < 0 || layer >= route.Layers.Count) return null;
            var entry = route.Layers[layer];
            if (!entry.HasBoss) return null;
            var floor = new DungeonFloor(layer, entry.FloorCount - 1);
            return route.IsBoss(floor) ? floor : (DungeonFloor?)null;
        }

        public string PromptLabel => open ? openLabel : closedLabel;

        public bool CanInteract(GameObject interactor) => open || !string.IsNullOrEmpty(closedLabel);

        public void Interact(GameObject interactor)
        {
            if (!open) return;
            if (direction == ShortcutDirection.ToSurface)
            {
                SceneTravel.Go(SceneTravel.SurfaceScene, SurfaceArrivalPoint(layer));
                return;
            }

            var floor = BossFloor();
            if (!floor.HasValue) return;
            DungeonProgress.Pending = floor;
            DungeonProgress.PendingFromAbove = false;
            SceneTravel.Go(route.SceneOf(floor.Value), PlayerSpawnPoint.Shortcut);
        }

        public string DetailTitle => string.IsNullOrEmpty(title) ? null : string.Format(title, layer + 1);

        public string DetailBody => string.IsNullOrEmpty(description) ? null : string.Format(description, layer + 1);

        public Texture2D DetailIcon => null;

        public Color? DetailBackground => null;
    }
}
