using TpsDungeon.Interaction;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 死後の場所（Afterlife）の、燭台の輪の真ん中の台。死んで得たメタ通貨で永続強化をする所。
    /// メタ通貨と永続強化はまだ無いので、今はインタラクトできて情報欄が出るところまで。強化の画面はここから開くようにつなぐ。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Upgrade Altar")]
    public sealed class UpgradeAltar : MonoBehaviour, IInteractable, IInteractableDetails
    {
        [SerializeField, Tooltip("プロンプトに出す動詞。")]
        private string promptLabel = "強化する";

        [SerializeField, Tooltip("情報欄の見出し。")]
        private string title = "魂の燭台";

        [SerializeField, TextArea, Tooltip("情報欄の本文。")]
        private string description = "死んで得た魂の欠片で、次の生を強くする。（準備中）";

        public string PromptLabel => promptLabel;

        public bool CanInteract(GameObject interactor) => true;

        public void Interact(GameObject interactor)
        {
            Debug.Log("強化の画面はまだ無い（UpgradeAltar）", this);
        }

        public string DetailTitle => title;

        public string DetailBody => description;

        public Texture2D DetailIcon => null;

        public Color? DetailBackground => null;
    }
}
