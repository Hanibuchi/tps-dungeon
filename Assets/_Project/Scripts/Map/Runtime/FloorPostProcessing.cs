using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 階のシーンにいる間だけ、Main Camera の post-processing を有効にする（階の Global Volume を効かせるため）。
    /// カメラは地上から持ち越されるので、階を出るとき（このシーンが閉じるとき）に元へ戻し、地上の見た目は変えない。
    /// FloorBuilder が階と一緒に生成する Global Volume に付く。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Floor Post Processing")]
    public sealed class FloorPostProcessing : MonoBehaviour
    {
        private UniversalAdditionalCameraData target;
        private bool previous;

        // OnEnable ではなく Start で取る。階のシーンにも MainCamera タグの俯瞰カメラがあり、
        // GameFlow がそれを止めるのは読み込み直後（sceneLoaded）なので、それより後でないとそちらを掴んでしまう。
        private void Start()
        {
            if (!isActiveAndEnabled || target != null) return;

            // パーティーのカメラは地上から持ち越されて居る。居なければ（確認用シーンなど）、その時のメインカメラに効かせる。
            var camera = Camera.main;
            if (camera == null) return;

            target = camera.GetUniversalAdditionalCameraData();
            previous = target.renderPostProcessing;
            target.renderPostProcessing = true;
        }

        private void OnDisable()
        {
            if (target != null) target.renderPostProcessing = previous;
            target = null;
        }
    }
}
