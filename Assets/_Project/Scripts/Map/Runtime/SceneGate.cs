using System;
using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>地上の地図に出す印。None なら地図には出さない。</summary>
    public enum MapIcon
    {
        None = 0,
        /// <summary>ダンジョンの入口（城の大門）。</summary>
        DungeonEntrance = 1,
        /// <summary>武器屋。</summary>
        WeaponShop = 2,
        /// <summary>銀行。</summary>
        Bank = 3,
    }

    /// <summary>
    /// インタラクトで別のシーンへ移る入口（城の門・ショートカットの門・拠点の建物など）。
    /// 閉じている間も照準を合わせれば案内（closedLabel）と情報欄は出すが、押しても何も起きない。
    /// closedLabel が空なら、閉じている間は案内も出さない（触れない物として扱う）。
    /// 当たり判定はこの GameObject か子のコライダ（トリガーでもよい）。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Scene Gate")]
    public sealed class SceneGate : MonoBehaviour, IInteractable, IInteractableDetails
    {
        [SerializeField, Tooltip("行き先のシーン名（Build Settings に入っている名前）。")]
        private string sceneName;

        [SerializeField, Tooltip("通れるか。閉じている間は押しても移らない。")]
        private bool open = true;

        [SerializeField, Tooltip("通れるときの案内。")]
        private string openLabel = "入る";

        [SerializeField, Tooltip("閉じているときの案内。")]
        private string closedLabel = "まだ開いていない";

        [SerializeField, Tooltip("情報欄の見出し（門や建物の名前）。")]
        private string title;

        [SerializeField, TextArea, Tooltip("情報欄の本文。")]
        private string description;

        [SerializeField, Tooltip("地上の地図に出す印。None なら出さない。")]
        private MapIcon mapIcon;

        [SerializeField, Tooltip("地上の地図で印の下に出す名前。空なら情報欄の見出し。")]
        private string mapLabel;

        public MapIcon Icon
        {
            get => mapIcon;
            set => mapIcon = value;
        }

        /// <summary>地上の地図で印の下に出す名前。</summary>
        public string MapLabel => string.IsNullOrEmpty(mapLabel) ? title : mapLabel;

        /// <summary>行き先。生成した階段のように、置いてから決まるものはあとから入れる。</summary>
        public string SceneName
        {
            get => sceneName;
            set => sceneName = value;
        }

        public bool IsOpen
        {
            get => open;
            set => open = value;
        }

        /// <summary>通れるときの案内。生成した階段のように、置いてから決まるものはあとから入れる。</summary>
        public string OpenLabel
        {
            get => openLabel;
            set => openLabel = value;
        }

        /// <summary>閉じているときの案内。空なら閉じている間は案内を出さない。</summary>
        public string ClosedLabel
        {
            get => closedLabel;
            set => closedLabel = value;
        }

        /// <summary>通るとき、シーンを移る頼みを出す直前に呼ばれる。</summary>
        public event Action Entering;

        public string PromptLabel => open ? openLabel : closedLabel;

        public bool CanInteract(GameObject interactor) => open || !string.IsNullOrEmpty(closedLabel);

        public void Interact(GameObject interactor)
        {
            if (!open) return;
            Entering?.Invoke();
            SceneTravel.Go(sceneName);
        }

        public string DetailTitle => title;

        public string DetailBody => description;

        public Texture2D DetailIcon => null;

        public Color? DetailBackground => null;
    }
}
