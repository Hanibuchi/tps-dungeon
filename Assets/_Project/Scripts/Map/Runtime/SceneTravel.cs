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

        /// <summary>
        /// 着いたシーンで立ってほしいスポーン地点の名前（PlayerSpawnPoint.PointId）。空なら既定の地点。
        /// 受け手が着いたときに <see cref="TakeArrivalPoint"/> で受け取る。
        /// </summary>
        public static string ArrivalPoint { get; set; }

        public static string TakeArrivalPoint()
        {
            string point = ArrivalPoint;
            ArrivalPoint = null;
            return point;
        }

        /// <param name="arrivalPoint">着いたシーンで立つスポーン地点の名前。null なら頼まない（先に ArrivalPoint に入っていればそれを使う）。</param>
        public static void Go(string sceneName, string arrivalPoint = null)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            if (arrivalPoint != null) ArrivalPoint = arrivalPoint;

            if (Handler != null) Handler(sceneName);
            else Load(sceneName);
        }

        /// <summary>
        /// sceneName を読み込んで今のシーンと入れ替える。受け手もこれで読む。
        /// エディタでは、Build Settings に入っていない確認用のシーン（Player_Debug など）でも、今のシーンを読み直すときはパスで読む。
        /// </summary>
        public static void Load(string sceneName)
        {
#if UNITY_EDITOR
            Scene active = SceneManager.GetActiveScene();
            if (active.name == sceneName && active.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(active.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ArrivalPoint = null;
        }
    }
}
