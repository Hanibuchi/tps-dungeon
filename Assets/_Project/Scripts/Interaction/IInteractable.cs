using UnityEngine;

namespace TpsDungeon.Interaction
{
    /// <summary>
    /// プレイヤーが照準を合わせてインタラクトキーで触れるもの。
    /// コライダを持つ GameObject か、その親のどこかに付けておけば PlayerInteractor が拾う。
    /// </summary>
    public interface IInteractable
    {
        /// <summary>プロンプトに出す動詞（「開ける」など）。状態で変わってよい。</summary>
        string PromptLabel { get; }

        /// <summary>今この相手が触れるか。偽ならプロンプトも出さない。</summary>
        bool CanInteract(GameObject interactor);

        /// <summary>インタラクトキーが押されたときに呼ばれる。</summary>
        void Interact(GameObject interactor);
    }
}
