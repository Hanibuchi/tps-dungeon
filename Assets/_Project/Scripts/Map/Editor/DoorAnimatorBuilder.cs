using System.IO;
using TpsDungeon.Map.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TpsDungeon.Map.Editor
{
    /// <summary>
    /// ドアの開閉アニメーション一式を作る。
    ///
    /// クリップ（Door_Open / Door_Close）は動きの中身なので、無いときだけ作り、既にあれば触らない。
    /// 開き方・速さ・イージングを変えたければ Animation ウィンドウで直接直してよい（作り直しても消えない）。
    /// 回すのは Door ルートから見た "Hinge" の Y 回転なので、別のパスや別のプロパティを動かすと扉は回らない。
    ///
    /// コントローラは構成なので毎回作り直す（GUID は保つ）。Closed と Open の 2 ステートを bool の Open で
    /// 行き来し、どちらのクリップもループしない（最後のポーズで止まる）。名前は Door の定数と揃えてある。
    /// </summary>
    public static class DoorAnimatorBuilder
    {
        private const string Folder = "Assets/_Project/Animation/Door";
        public const string ControllerPath = Folder + "/Door.controller";
        public const string OpenClipPath = Folder + "/Door_Open.anim";
        public const string CloseClipPath = Folder + "/Door_Close.anim";

        private const string HingePath = "Hinge";

        // 初期値。作った後はクリップ側が正なので、ここを変えても既存のクリップには効かない。
        private const float OpenAngle = 100f;
        private const float OpenDuration = 0.45f;
        private const float CloseDuration = 0.35f;

        [MenuItem("Tools/TPS Dungeon/Map/ドアの Animator を作る")]
        public static void BuildFromMenu()
        {
            Build();
            AssetDatabase.SaveAssets();
            Debug.Log($"ドアの Animator を作った: {ControllerPath}");
        }

        /// <summary>クリップを（無ければ）作り、コントローラを組み直して返す。</summary>
        public static AnimatorController Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Animation", "Door");

            var open = EnsureClip(OpenClipPath, 0f, OpenAngle, OpenDuration);
            var close = EnsureClip(CloseClipPath, OpenAngle, 0f, CloseDuration);

            var controller = ResetController();
            controller.AddParameter(Door.OpenParam, AnimatorControllerParameterType.Bool);
            controller.AddLayer("Base Layer");

            var sm = controller.layers[0].stateMachine;
            var closedState = sm.AddState(Door.ClosedState, new Vector3(300f, 0f, 0f));
            closedState.motion = close;
            var openState = sm.AddState(Door.OpenState, new Vector3(300f, 120f, 0f));
            openState.motion = open;
            sm.defaultState = closedState;

            AddTransition(closedState, openState, AnimatorConditionMode.If);
            AddTransition(openState, closedState, AnimatorConditionMode.IfNot);

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>行き先のクリップを頭からすぐ流す。ブレンドすると閉じきる前に当たり判定の判断がずれるので 0 秒。</summary>
        private static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = 0f;
            t.offset = 0f;
            t.AddCondition(mode, 0f, Door.OpenParam);
        }

        private static AnimationClip EnsureClip(string path, float fromAngle, float toAngle, float duration)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing;

            var clip = new AnimationClip { name = Path.GetFileNameWithoutExtension(path) };

            // Y だけでなく X / Z も 0 で持たせる。欠けた軸は Animator の既定値に任せることになり、読み違えやすいので。
            SetCurve(clip, "localEulerAnglesRaw.x", AnimationCurve.Constant(0f, duration, 0f));
            SetCurve(clip, "localEulerAnglesRaw.y", AnimationCurve.EaseInOut(0f, fromAngle, duration, toAngle));
            SetCurve(clip, "localEulerAnglesRaw.z", AnimationCurve.Constant(0f, duration, 0f));

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void SetCurve(AnimationClip clip, string property, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(HingePath, typeof(Transform), property), curve);
        }

        /// <summary>既存のコントローラは GUID を保ったまま中身だけ空にする（プレハブからの参照が切れないように）。</summary>
        private static AnimatorController ResetController()
        {
            // 新しく作ったものも既定の Base Layer を持っているので、同じように空にしてから組む。
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (sub != null && sub != controller) Object.DestroyImmediate(sub, true);
            }

            controller.layers = new AnimatorControllerLayer[0];
            controller.parameters = new AnimatorControllerParameter[0];
            return controller;
        }
    }
}
