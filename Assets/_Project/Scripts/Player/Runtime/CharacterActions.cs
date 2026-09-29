using UnityEngine;

namespace TpsDungeon.Player
{
    /// <summary>
    /// キャラの Animator の Action 層で、全身の動き（<see cref="CharacterAction"/>）を再生する窓口。
    /// Action 層は一番上に重なり、再生中は下の移動・上半身の構えより優先して見える。
    ///
    ///   1 回きりの動き（GetHit・Roll・Jump など）… 終わると勝手に元へ戻る
    ///   ループする動き（StunnedLoop・RunForward など）と Death … <see cref="Stop"/> か別の動きを再生するまで続く
    ///
    /// ルートモーションは使っていない（移動は ThirdPersonController のまま）。転がりなどで体を動かしたいなら呼ぶ側で動かすこと。
    /// Animator を持つキャラのルートか、その親に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Character Actions")]
    public sealed class CharacterActions : MonoBehaviour
    {
        // CharacterAnimatorBuilder（エディタ専用アセンブリ）が焼き込んだ値。向こうを変えたらここも揃えること。
        public const string ActionParam = "Action";
        public const string PlayActionParam = "PlayAction";
        public const string LayerName = "Action";

        private static readonly int ActionHash = Animator.StringToHash(ActionParam);
        private static readonly int PlayActionHash = Animator.StringToHash(PlayActionParam);

        [SerializeField, Tooltip("操作する Animator。未設定なら子から探す。")]
        private Animator animator;

        private int layer = -1;

        /// <summary>最後に再生を頼んだ動き。1 回きりの動きが終わっても残る。</summary>
        public CharacterAction Requested { get; private set; }

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        /// <summary>動きを頭から再生する。同じ動きの再生中でも頭から。None なら <see cref="Stop"/>。</summary>
        public void Play(CharacterAction action)
        {
            if (action == CharacterAction.None)
            {
                Stop();
                return;
            }

            if (animator == null) return;
            Requested = action;
            animator.SetInteger(ActionHash, (int)action);
            animator.SetTrigger(PlayActionHash);
        }

        /// <summary>再生中の動きをやめて、下の層（移動・構え）に戻す。</summary>
        public void Stop()
        {
            Requested = CharacterAction.None;
            if (animator == null) return;
            animator.ResetTrigger(PlayActionHash);
            animator.SetInteger(ActionHash, (int)CharacterAction.None);
        }

        /// <summary>Action 層で何かを再生している（戻りの溶け込み中も含む）か。</summary>
        public bool IsPlaying
        {
            get
            {
                if (animator == null) return false;
                if (layer < 0) layer = animator.GetLayerIndex(LayerName);
                if (layer < 0) return false;

                int none = Animator.StringToHash(CharacterAction.None.ToString());
                return animator.GetCurrentAnimatorStateInfo(layer).shortNameHash != none || animator.IsInTransition(layer);
            }
        }
    }
}
