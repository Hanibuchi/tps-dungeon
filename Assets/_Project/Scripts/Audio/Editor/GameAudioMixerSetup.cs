using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace TpsDungeon.Audio.Editor
{
    /// <summary>
    /// GameAudioMixer.mixer を決まった構成で組み立て直すエディタ専用ツール。
    /// .mixer はエディタ GUI でしか作れず公開 API も無いので、UnityEditor 内部の
    /// AudioMixerController をリフレクション経由で叩いている。
    /// 構成を変えたくなったらこのファイルを直して作り直すのが正しいやり方。
    ///
    /// 構成:
    ///   Master
    ///   ├─ BGM     Attenuation → Duck Volume → Lowpass
    ///   ├─ SE      Attenuation → Lowpass → Send(→Reverb) → Send(→BGM の Duck Volume)
    ///   └─ Reverb  Receive → SFX Reverb → Attenuation   ※SE からのリターン専用
    ///
    /// 露出パラメータ(スライダー用): MasterVolume / BgmVolume / SeVolume
    /// スナップショット: Default / Dungeon / Paused
    ///
    /// 露出パラメータはスナップショットのブレンドを上書きするので、
    /// 「音量は露出パラメータ」「ローパスとリバーブ送りはスナップショット」と役割を分けてある。
    /// </summary>
    public static class GameAudioMixerSetup
    {
        public const string MixerPath = "Assets/_Project/Settings/Audio/GameAudioMixer.mixer";

        public const string MasterGroupName = "Master";
        public const string BgmGroupName = "BGM";
        public const string SeGroupName = "SE";
        public const string ReverbGroupName = "Reverb";

        public const string MasterVolumeParam = "MasterVolume";
        public const string BgmVolumeParam = "BgmVolume";
        public const string SeVolumeParam = "SeVolume";

        public const string DefaultSnapshotName = "Default";
        public const string DungeonSnapshotName = "Dungeon";
        public const string PausedSnapshotName = "Paused";

        /// <summary>ローパスを通過させるときのカットオフ。上限いっぱいで実質素通り。</summary>
        private const float OpenCutoff = 22000f;

        /// <summary>ポーズ中のカットオフ。ここまで落とすとはっきりこもる。</summary>
        private const float MuffledCutoff = 800f;

        /// <summary>リバーブへの送り量。-80dB は実質オフで、SFX Reverb の DSP 負荷も乗らない。</summary>
        private const float ReverbSendOff = -80f;
        private const float ReverbSendDungeon = -10f;

        /// <summary>SE から BGM のダッキングへ送る量。常時一定でよい。</summary>
        private const float DuckSendLevel = -6f;

        [MenuItem("Tools/TPS Dungeon/Audio/Generate Game Audio Mixer")]
        public static void GenerateFromMenu()
        {
            if (File.Exists(MixerPath) &&
                !EditorUtility.DisplayDialog(
                    "Game Audio Mixer を作り直す",
                    MixerPath + " を作り直します。\nエディタ上で手を入れた設定は失われます。",
                    "作り直す", "やめる"))
            {
                return;
            }

            string log = Generate();
            Debug.Log(log);
        }

        /// <summary>ミキサーを作り直して、何をしたかのログを返す。</summary>
        public static string Generate()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MixerPath));
            AssetDatabase.DeleteAsset(MixerPath);

            object controller = CreateMixerControllerAtPath.Invoke(null, new object[] { MixerPath });
            if (controller == null) throw new InvalidOperationException("AudioMixerController を作れなかった。");

            object master = MasterGroupProp.GetValue(controller);
            ((UnityEngine.Object)master).name = MasterGroupName;

            object bgm = CreateGroup(controller, master, BgmGroupName);
            object se = CreateGroup(controller, master, SeGroupName);
            object reverb = CreateGroup(controller, master, ReverbGroupName);

            // BGM: SE から送られてくるサイドチェーンで音量を下げ、ポーズ中はこもらせる。
            object duck = AddEffect(controller, bgm, "Duck Volume", AtEnd);
            object bgmLowpass = AddEffect(controller, bgm, "Lowpass", AtEnd);

            // Reverb: SE からのリターン専用。Receive を先頭に置いて信号を受け取る。
            object receive = AddEffect(controller, reverb, "Receive", 0);
            object sfxReverb = AddEffect(controller, reverb, "SFX Reverb", 1);

            // SE: 減衰 → ローパス → リバーブ送り → ダッキング送り。
            object seLowpass = AddEffect(controller, se, "Lowpass", AtEnd);
            object reverbSend = AddEffect(controller, se, "Send", AtEnd);
            object duckSend = AddEffect(controller, se, "Send", AtEnd);
            SendTargetProp.SetValue(reverbSend, receive);
            SendTargetProp.SetValue(duckSend, duck);

            SetupDefaultView(controller);

            ExposeVolume(controller, master, MasterVolumeParam);
            ExposeVolume(controller, bgm, BgmVolumeParam);
            ExposeVolume(controller, se, SeVolumeParam);

            // 既定のスナップショットを Default に仕立ててから、それを複製して他の 2 つを作る。
            object defaultSnapshot = FirstSnapshot(controller);
            ((UnityEngine.Object)defaultSnapshot).name = DefaultSnapshotName;
            TargetSnapshotProp.SetValue(controller, defaultSnapshot);
            ApplySnapshotValues(controller, defaultSnapshot,
                bgmLowpass, seLowpass, reverbSend, duckSend, sfxReverb,
                cutoff: OpenCutoff, sendLevel: ReverbSendOff);

            object dungeonSnapshot = CloneSnapshot(controller, DungeonSnapshotName);
            ApplySnapshotValues(controller, dungeonSnapshot,
                bgmLowpass, seLowpass, reverbSend, duckSend, sfxReverb,
                cutoff: OpenCutoff, sendLevel: ReverbSendDungeon);

            object pausedSnapshot = CloneSnapshot(controller, PausedSnapshotName);
            ApplySnapshotValues(controller, pausedSnapshot,
                bgmLowpass, seLowpass, reverbSend, duckSend, sfxReverb,
                cutoff: MuffledCutoff, sendLevel: ReverbSendOff);

            // 再生開始時とエディタ上の表示はどちらも Default に戻しておく。
            TargetSnapshotProp.SetValue(controller, defaultSnapshot);
            StartSnapshotProp.SetValue(controller, defaultSnapshot);

            AttachOrphanSubAssets(controller);

            EditorUtility.SetDirty((UnityEngine.Object)controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);

            return "Game Audio Mixer を生成した: " + MixerPath + "\n" + Describe();
        }

        /// <summary>生成結果を人が読める形にまとめる。検証ログ用。</summary>
        public static string Describe()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (mixer == null) return "ミキサーが見つからない: " + MixerPath;

            var lines = new List<string>();
            foreach (string groupName in new[] { MasterGroupName, BgmGroupName, SeGroupName, ReverbGroupName })
            {
                // FindMatchingGroups はパスの部分一致なので、名前が完全に一致したものだけ数える。
                int count = 0;
                foreach (AudioMixerGroup group in mixer.FindMatchingGroups(groupName))
                {
                    if (group.name == groupName) count++;
                }

                lines.Add("  group " + groupName + ": " + count + " 件");
            }

            foreach (string param in new[] { MasterVolumeParam, BgmVolumeParam, SeVolumeParam })
            {
                lines.Add("  param " + param + ": " + (mixer.GetFloat(param, out float value) ? value + "dB" : "露出していない"));
            }

            foreach (string snapshot in new[] { DefaultSnapshotName, DungeonSnapshotName, PausedSnapshotName })
            {
                lines.Add("  snapshot " + snapshot + ": " + (mixer.FindSnapshot(snapshot) != null ? "あり" : "なし"));
            }

            return string.Join("\n", lines);
        }

        // ---- 組み立ての部品 -------------------------------------------------

        /// <summary>エフェクトを末尾に足すことを表す番兵。</summary>
        private const int AtEnd = -1;

        private static object CreateGroup(object controller, object parent, string name)
        {
            object group = CreateNewGroupMethod.Invoke(controller, new object[] { name, false });
            AddChildToParentMethod.Invoke(controller, new[] { group, parent });
            ((UnityEngine.Object)group).name = name;
            return group;
        }

        private static object AddEffect(object controller, object group, string effectName, int index)
        {
            object effect = Activator.CreateInstance(EffectType, new object[] { effectName });
            var effects = (Array)EffectsProp.GetValue(group);
            InsertEffectMethod.Invoke(group, new object[] { effect, index == AtEnd ? effects.Length : index });
            ((UnityEngine.Object)effect).name = effectName;
            return effect;
        }

        /// <summary>
        /// 作りたてのミキサーはビューを 1 つも持っておらず、この状態だと
        /// AudioMixer ウィンドウがグループを表示できない。全グループを含むビューを 1 つ用意する。
        /// </summary>
        private static void SetupDefaultView(object controller)
        {
            var guids = new List<GUID>();
            var pending = new Stack<object>();
            pending.Push(MasterGroupProp.GetValue(controller));
            while (pending.Count > 0)
            {
                object group = pending.Pop();
                guids.Add((GUID)GroupIdProp.GetValue(group));
                foreach (object child in (Array)ChildrenProp.GetValue(group)) pending.Push(child);
            }

            object view = Activator.CreateInstance(ViewType);
            ViewGuidsField.SetValue(view, guids.ToArray());
            ViewNameField.SetValue(view, "View");

            var views = Array.CreateInstance(ViewType, 1);
            views.SetValue(view, 0);
            ViewsProp.SetValue(controller, views);
            CurrentViewIndexProp.SetValue(controller, 0);
        }

        private static void ExposeVolume(object controller, object group, string parameterName)
        {
            var guid = (GUID)GetGuidForVolumeMethod.Invoke(group, null);
            object path = Activator.CreateInstance(ParameterPathType, new object[] { group, guid });
            AddExposedParameterMethod.Invoke(controller, new[] { path });

            // 露出させただけでは "MyExposedParam" のような既定名なので、狙った名前に付け替える。
            var exposed = (Array)ExposedParametersProp.GetValue(controller);
            for (int i = 0; i < exposed.Length; i++)
            {
                object entry = exposed.GetValue(i);
                if (!guid.Equals((GUID)ExposedGuidField.GetValue(entry))) continue;
                ExposedNameField.SetValue(entry, parameterName);
                exposed.SetValue(entry, i);
            }

            ExposedParametersProp.SetValue(controller, exposed);
        }

        private static object FirstSnapshot(object controller)
        {
            var snapshots = (Array)SnapshotsProp.GetValue(controller);
            if (snapshots == null || snapshots.Length == 0)
            {
                throw new InvalidOperationException("既定のスナップショットが無い。");
            }

            return snapshots.GetValue(0);
        }

        /// <summary>
        /// 今の TargetSnapshot を複製して名前を付ける。
        /// CloneNewSnapshotFromTarget は戻り値を返さないので、増えた末尾を取り直す。
        /// </summary>
        private static object CloneSnapshot(object controller, string name)
        {
            CloneSnapshotMethod.Invoke(controller, new object[] { false });

            var snapshots = (Array)SnapshotsProp.GetValue(controller);
            object snapshot = snapshots.GetValue(snapshots.Length - 1);
            ((UnityEngine.Object)snapshot).name = name;
            TargetSnapshotProp.SetValue(controller, snapshot);
            return snapshot;
        }

        private static void ApplySnapshotValues(
            object controller, object snapshot,
            object bgmLowpass, object seLowpass, object reverbSend, object duckSend, object sfxReverb,
            float cutoff, float sendLevel)
        {
            SetParameter(bgmLowpass, controller, snapshot, "Cutoff freq", cutoff);
            SetParameter(seLowpass, controller, snapshot, "Cutoff freq", cutoff);
            SetMixLevel(reverbSend, controller, snapshot, sendLevel);
            SetMixLevel(duckSend, controller, snapshot, DuckSendLevel);

            // 石造りの塔らしい、やや長めで拡散の強い残響。
            SetParameter(sfxReverb, controller, snapshot, "Dry Level", 0f);
            SetParameter(sfxReverb, controller, snapshot, "Room", -800f);
            SetParameter(sfxReverb, controller, snapshot, "Room HF", -1500f);
            SetParameter(sfxReverb, controller, snapshot, "Decay Time", 2.4f);
            SetParameter(sfxReverb, controller, snapshot, "Decay HF Ratio", 0.7f);
            SetParameter(sfxReverb, controller, snapshot, "Reflections", -1000f);
            SetParameter(sfxReverb, controller, snapshot, "Reverb", 400f);
            SetParameter(sfxReverb, controller, snapshot, "Diffusion", 100f);
            SetParameter(sfxReverb, controller, snapshot, "Density", 100f);
        }

        private static void SetParameter(object effect, object controller, object snapshot, string name, float value)
        {
            SetValueForParameterMethod.Invoke(effect, new object[] { controller, snapshot, name, value });
        }

        private static void SetMixLevel(object effect, object controller, object snapshot, float value)
        {
            SetValueForMixLevelMethod.Invoke(effect, new object[] { controller, snapshot, value });
        }

        /// <summary>
        /// グループ・エフェクト・スナップショットは .mixer のサブアセットとして保存しないと、
        /// 次にアセットを読み直したときに参照が切れて消える。まだ紐付いていないものを拾って足す。
        /// </summary>
        private static void AttachOrphanSubAssets(object controller)
        {
            var mixerAsset = (UnityEngine.Object)controller;
            foreach (UnityEngine.Object sub in CollectSubAssets(controller))
            {
                if (sub == null || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sub))) continue;
                sub.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(sub, mixerAsset);
            }
        }

        private static IEnumerable<UnityEngine.Object> CollectSubAssets(object controller)
        {
            var snapshots = (Array)SnapshotsProp.GetValue(controller);
            if (snapshots != null)
            {
                foreach (object snapshot in snapshots) yield return (UnityEngine.Object)snapshot;
            }

            var pending = new Stack<object>();
            pending.Push(MasterGroupProp.GetValue(controller));
            while (pending.Count > 0)
            {
                object group = pending.Pop();
                yield return (UnityEngine.Object)group;

                foreach (object effect in (Array)EffectsProp.GetValue(group))
                {
                    yield return (UnityEngine.Object)effect;
                }

                foreach (object child in (Array)ChildrenProp.GetValue(group))
                {
                    pending.Push(child);
                }
            }
        }

        // ---- UnityEditor 内部 API へのリフレクション ------------------------

        private const BindingFlags Any =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static readonly Assembly EditorAssembly = typeof(AssetDatabase).Assembly;

        private static readonly Type ControllerType = EditorAssembly.GetType("UnityEditor.Audio.AudioMixerController", true);
        private static readonly Type GroupType = EditorAssembly.GetType("UnityEditor.Audio.AudioMixerGroupController", true);
        private static readonly Type EffectType = EditorAssembly.GetType("UnityEditor.Audio.AudioMixerEffectController", true);
        private static readonly Type ParameterPathType = EditorAssembly.GetType("UnityEditor.Audio.AudioGroupParameterPath", true);
        private static readonly Type ExposedParameterType = EditorAssembly.GetType("UnityEditor.Audio.ExposedAudioParameter", true);
        private static readonly Type ViewType = EditorAssembly.GetType("UnityEditor.Audio.MixerGroupView", true);

        private static readonly MethodInfo CreateMixerControllerAtPath = ControllerType.GetMethod("CreateMixerControllerAtPath", Any);
        private static readonly MethodInfo CreateNewGroupMethod = ControllerType.GetMethod("CreateNewGroup", Any);
        private static readonly MethodInfo AddChildToParentMethod = ControllerType.GetMethod("AddChildToParent", Any);
        private static readonly MethodInfo AddExposedParameterMethod = ControllerType.GetMethod("AddExposedParameter", Any);
        private static readonly MethodInfo CloneSnapshotMethod = ControllerType.GetMethod("CloneNewSnapshotFromTarget", Any);

        private static readonly PropertyInfo MasterGroupProp = ControllerType.GetProperty("masterGroup", Any);
        private static readonly PropertyInfo ExposedParametersProp = ControllerType.GetProperty("exposedParameters", Any);
        private static readonly PropertyInfo SnapshotsProp = ControllerType.GetProperty("snapshots", Any);
        private static readonly PropertyInfo TargetSnapshotProp = ControllerType.GetProperty("TargetSnapshot", Any);
        private static readonly PropertyInfo StartSnapshotProp = ControllerType.GetProperty("startSnapshot", Any);
        private static readonly PropertyInfo ViewsProp = ControllerType.GetProperty("views", Any);
        private static readonly PropertyInfo CurrentViewIndexProp = ControllerType.GetProperty("currentViewIndex", Any);

        private static readonly PropertyInfo EffectsProp = GroupType.GetProperty("effects", Any);
        private static readonly PropertyInfo ChildrenProp = GroupType.GetProperty("children", Any);
        private static readonly MethodInfo InsertEffectMethod = GroupType.GetMethod("InsertEffect", Any);
        private static readonly MethodInfo GetGuidForVolumeMethod = GroupType.GetMethod("GetGUIDForVolume", Any);
        private static readonly PropertyInfo GroupIdProp = GroupType.GetProperty("groupID", Any);

        private static readonly PropertyInfo SendTargetProp = EffectType.GetProperty("sendTarget", Any);
        private static readonly MethodInfo SetValueForParameterMethod = EffectType.GetMethod("SetValueForParameter", Any);
        private static readonly MethodInfo SetValueForMixLevelMethod = EffectType.GetMethod("SetValueForMixLevel", Any);

        private static readonly FieldInfo ViewGuidsField = ViewType.GetField("guids", Any);
        private static readonly FieldInfo ViewNameField = ViewType.GetField("name", Any);

        private static readonly FieldInfo ExposedGuidField = ExposedParameterType.GetField("guid", Any);
        private static readonly FieldInfo ExposedNameField = ExposedParameterType.GetField("name", Any);
    }
}
