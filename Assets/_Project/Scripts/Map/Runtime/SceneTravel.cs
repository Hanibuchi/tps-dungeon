using System;
using UnityEngine.SceneManagement;

namespace TpsDungeon.Map.Runtime
{
    /// <summary>
    /// 別のシーン（地上・階）へ移ってほしいという頼みの窓口。門や階段はここへ頼むだけで、実際に移すのは受け手（パーティーの GameFlow）。
    /// パーティーを次のシーンへ持ち越すのは受け手の役目なので、Map からはパーティーを知らずに済む。
    /// 受け手が居ない（パーティーの無い確認用シーンなど）ときは、そのままシーンを読み込む。
    /// </summary>
    public static class SceneTravel
    {
        /// <summary>地上のシーン名。死んだときもここへ戻る。</summary>
        public const string SurfaceScene = "Surface";

        /// <summary>移ってほしいシーン名が届く。受け手は <see cref="Handler"/> に 1 つだけ入れる。</summary>
        public static Action<string> Handler { get; set; }

        public static void Go(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            if (Handler != null) Handler(sceneName);
            else SceneManager.LoadScene(sceneName);
        }
    }
}
