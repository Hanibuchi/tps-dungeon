using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace TpsDungeon.Menu
{
    /// <summary>
    /// マウスで視点を回すときの感度と上下反転。
    /// Look アクションのマウス用バインドには元から ScaleVector2 と InvertVector2 のプロセッサが付いているので、
    /// その引数を実行時に上書きして効かせる。キャラ側（Starter Assets）のコードには手を入れない。
    /// </summary>
    public static class LookSettings
    {
        public const float MinSensitivity = 0.1f;
        public const float MaxSensitivity = 3f;
        public const float DefaultSensitivity = 1f;

        /// <summary>マウスのバインドが属するコントロールスキーム。</summary>
        public const string MouseGroup = "KeyboardMouse";

        private const string ScaleProcessor = "ScaleVector2";
        private const string InvertProcessor = "InvertVector2";

        public static float ClampSensitivity(float value)
        {
            if (float.IsNaN(value)) return DefaultSensitivity;
            return Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        }

        /// <summary>
        /// Look アクションのマウス用バインドに感度と上下反転を当てる。
        /// 感度は入力アセットに書いてある倍率への掛け算、上下反転は入力アセットの向きからの反転。
        /// マウス用のバインドが無ければ何もせず false。
        /// </summary>
        public static bool Apply(InputAction look, float sensitivity, bool invertY)
        {
            if (look == null) return false;

            InputBinding mask = InputBinding.MaskByGroup(MouseGroup);
            int index = look.GetBindingIndex(mask);
            if (index < 0) return false;

            ReadDefaults(look.bindings[index].processors, out Vector2 baseScale, out bool baseInvertY);
            float scale = ClampSensitivity(sensitivity);
            look.ApplyParameterOverride(ScaleProcessor + ":x", baseScale.x * scale, mask);
            look.ApplyParameterOverride(ScaleProcessor + ":y", baseScale.y * scale, mask);
            look.ApplyParameterOverride(InvertProcessor + ":invertY", baseInvertY != invertY, mask);
            return true;
        }

        /// <summary>
        /// "InvertVector2(invertX=false),ScaleVector2(x=0.05,y=0.05)" のようなプロセッサ指定から、
        /// 元の倍率と上下の向きを読む。InvertVector2 は引数を省くと上下とも反転する。
        /// </summary>
        public static void ReadDefaults(string processors, out Vector2 scale, out bool invertY)
        {
            scale = Vector2.one;
            invertY = false;
            if (string.IsNullOrEmpty(processors)) return;

            foreach (NameAndParameters processor in NameAndParameters.ParseMultiple(processors))
            {
                if (Is(processor.name, ScaleProcessor))
                {
                    foreach (NamedValue parameter in processor.parameters)
                    {
                        if (Is(parameter.name, "x")) scale.x = parameter.value.ToSingle();
                        else if (Is(parameter.name, "y")) scale.y = parameter.value.ToSingle();
                    }
                }
                else if (Is(processor.name, InvertProcessor))
                {
                    invertY = true;
                    foreach (NamedValue parameter in processor.parameters)
                    {
                        if (Is(parameter.name, "invertY")) invertY = parameter.value.ToBoolean();
                    }
                }
            }
        }

        private static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
