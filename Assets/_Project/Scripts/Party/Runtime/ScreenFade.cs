using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 画面全体を黒で覆って暗転・明転する。シーンを移るときに GameFlow が使う（GameFlow と一緒にシーンをまたいで残る）。
    /// 一番手前に出す Canvas（Screen Space - Overlay）に黒い板を 1 枚置き、その透明度を動かす。時間は Time.timeScale に左右されない。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Screen Fade")]
    public sealed class ScreenFade : MonoBehaviour
    {
        // HUD やメニューより手前に出す。
        private const int SortingOrder = 30000;

        private CanvasGroup group;

        /// <summary>今の暗さ（0 で見える、1 で真っ暗）。</summary>
        public float Alpha => group != null ? group.alpha : 0f;

        /// <summary>parent の子に暗転用の Canvas を作る。初めは透明。</summary>
        public static ScreenFade Create(Transform parent)
        {
            var holder = new GameObject("[Screen Fade]");
            holder.transform.SetParent(parent, false);

            var canvas = holder.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var fade = holder.AddComponent<ScreenFade>();
            fade.group = holder.AddComponent<CanvasGroup>();
            fade.group.alpha = 0f;
            // 暗い間もクリックを奪わない（メニューなどの邪魔をしない）。
            fade.group.blocksRaycasts = false;
            fade.group.interactable = false;

            var curtain = new GameObject("Curtain");
            curtain.transform.SetParent(holder.transform, false);
            var rect = curtain.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = curtain.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            return fade;
        }

        /// <summary>今の暗さから target まで seconds 秒で動かす。</summary>
        public IEnumerator FadeTo(float target, float seconds)
        {
            float from = Alpha;
            if (seconds <= 0f)
            {
                group.alpha = target;
                yield break;
            }

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, target, t / seconds);
                yield return null;
            }
            group.alpha = target;
        }
    }
}
