using UnityEngine;

namespace TpsDungeon.Interaction
{
    /// <summary>
    /// 照準を合わせている間、画面右側に情報を出したい IInteractable が一緒に実装する。
    /// 落ちているアイテムの名前や説明など。実装しないもの（ドアなど）は情報欄を出さない。
    /// </summary>
    public interface IInteractableDetails
    {
        /// <summary>情報欄の見出し（アイテム名など）。</summary>
        string DetailTitle { get; }

        /// <summary>情報欄の本文。</summary>
        string DetailBody { get; }

        /// <summary>情報欄に出す絵。無ければ null。</summary>
        Texture2D DetailIcon { get; }
    }
}
