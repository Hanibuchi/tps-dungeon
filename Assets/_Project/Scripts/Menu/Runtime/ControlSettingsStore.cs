using UnityEngine;

namespace TpsDungeon.Menu
{
    /// <summary>
    /// 操作まわりの設定（マウス感度・上下反転・なめらかスクロールの無視・キー設定）の保存先。
    /// AudioSettingsStore と同じく、PlayerPrefs の利用をこのクラスに閉じ込めてある。
    /// </summary>
    public static class ControlSettingsStore
    {
        public const string MouseSensitivityKey = "Controls.MouseSensitivity";
        public const string InvertYKey = "Controls.InvertY";
        public const string DiscreteScrollKey = "Controls.DiscreteScroll";
        public const string BindingOverridesKey = "Controls.BindingOverrides";

        public static float LoadMouseSensitivity()
        {
            return LookSettings.ClampSensitivity(PlayerPrefs.GetFloat(MouseSensitivityKey, LookSettings.DefaultSensitivity));
        }

        public static void SaveMouseSensitivity(float value)
        {
            PlayerPrefs.SetFloat(MouseSensitivityKey, LookSettings.ClampSensitivity(value));
        }

        public static bool LoadInvertY()
        {
            return PlayerPrefs.GetInt(InvertYKey, 0) != 0;
        }

        public static void SaveInvertY(bool value)
        {
            PlayerPrefs.SetInt(InvertYKey, value ? 1 : 0);
        }

        public static bool LoadDiscreteScroll()
        {
            return PlayerPrefs.GetInt(DiscreteScrollKey, 0) != 0;
        }

        public static void SaveDiscreteScroll(bool value)
        {
            PlayerPrefs.SetInt(DiscreteScrollKey, value ? 1 : 0);
        }

        /// <summary>InputActionAsset.SaveBindingOverridesAsJson の結果。未保存なら空文字。</summary>
        public static string LoadBindingOverrides()
        {
            return PlayerPrefs.GetString(BindingOverridesKey, string.Empty);
        }

        public static void SaveBindingOverrides(string json)
        {
            if (string.IsNullOrEmpty(json)) PlayerPrefs.DeleteKey(BindingOverridesKey);
            else PlayerPrefs.SetString(BindingOverridesKey, json);
        }

        /// <summary>ディスクに書き出す。設定を触るたびに呼ぶ必要はない。</summary>
        public static void Flush()
        {
            PlayerPrefs.Save();
        }

        /// <summary>保存を全部消す。初回起動の挙動を確かめたいときのデバッグ用。</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(MouseSensitivityKey);
            PlayerPrefs.DeleteKey(InvertYKey);
            PlayerPrefs.DeleteKey(DiscreteScrollKey);
            PlayerPrefs.DeleteKey(BindingOverridesKey);
            Flush();
        }
    }
}
