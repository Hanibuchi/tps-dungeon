using System;
using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>地上（と死後の場所）の地図に出す印。None なら地図には出さない。</summary>
    public enum MapIcon
    {
        None = 0,
        /// <summary>ダンジョンの入口（城の大門）。</summary>
        DungeonEntrance = 1,
        /// <summary>武器屋。</summary>
        WeaponShop = 2,
        /// <summary>銀行。</summary>
        Bank = 3,
        /// <summary>死後の場所の強化の台。</summary>
        Upgrade = 4,
        /// <summary>死後の場所の転生の渦。</summary>
        Rebirth = 5,
        /// <summary>ガチャ屋。</summary>
        Gacha = 6,
    }

    /// <summary>
    /// インタラクトで別のシーンへ移る入口（城の門・ショートカットの門・拠点の建物など）。
    /// 閉じている間も照準を合わせれば案内（closedLabel）と情報欄は出すが、押しても何も起きない。
    /// closedLabel が空なら、閉じている間は案内も出さない（触れない物として扱う）。
    /// 当たり判定はこの GameObject か子のコライダ（トリガーでもよい）。
    /// 案内・見出し・本文は、文字の ID（textId）の行がインタラクトの文字の表（Settings/Tables/InteractionTexts.csv）にあればそちらを使い、無ければ自分のフィールドを使う。
    /// 実行中にコードが案内を入れ直したとき（階段の繋ぎ直しなど）は、その案内が表より勝つ。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Scene Gate")]
    public sealed class SceneGate : MonoBehaviour, IInteractable, IInteractableDetails
    {
        [SerializeField, Tooltip("インタラクトの文字の表（InteractionTexts.csv）の ID。行があれば案内・見出し・本文はそちらを使う。")]
        private string textId;

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

        // 実行中にコードが入れ直した案内は、表より勝つ。
        private bool openLabelSet;
        private bool closedLabelSet;

        /// <summary>インタラクトの文字の表の ID。</summary>
        public string TextId
        {
            get => textId;
            set => textId = value;
        }

        /// <summary>地上の地図で印の下に出す名前。表に行があればその見出し。</summary>
        public string MapLabel => !string.IsNullOrEmpty(mapLabel) ? mapLabel : DetailTitle;

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
            get => openLabelSet || !InteractionTexts.TryGet(textId, out var entry) ? openLabel : entry.prompt;
            set
            {
                openLabel = value;
                openLabelSet = true;
            }
        }

        /// <summary>閉じているときの案内。空なら閉じている間は案内を出さない。</summary>
        public string ClosedLabel
        {
            get => closedLabelSet || !InteractionTexts.TryGet(textId, out var entry) ? closedLabel : entry.closedPrompt;
            set
            {
                closedLabel = value;
                closedLabelSet = true;
            }
        }

        /// <summary>通るとき、シーンを移る頼みを出す直前に呼ばれる。</summary>
        public event Action Entering;

        public string PromptLabel => open ? OpenLabel : ClosedLabel;

        public bool CanInteract(GameObject interactor) => open || !string.IsNullOrEmpty(ClosedLabel);

        public void Interact(GameObject interactor)
        {
            if (!open) return;
            Entering?.Invoke();
            SceneTravel.Go(sceneName);
        }

        public string DetailTitle => InteractionTexts.TryGet(textId, out var entry) ? entry.title : title;

        public string DetailBody => InteractionTexts.TryGet(textId, out var entry) ? entry.description : description;

        public Texture2D DetailIcon => null;

        public Color? DetailBackground => null;
    }
}
