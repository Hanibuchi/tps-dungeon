using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TpsDungeon.Player.Editor
{
    /// <summary>
    /// プレイヤーの AnimatorController を組み立てる。
    /// 下半身（Base Layer）は Starter Assets の ThirdPersonController がそのまま動かせる移動とジャンプ、
    /// 上半身（UpperBody）は持っている武器ごとの構えと攻撃を担当する。
    /// 生成物なので手で編集せず、構成を変えたくなったらこのファイルを直して作り直すこと。
    ///
    /// スクリプトから触るパラメータ:
    ///   Speed / MotionSpeed / Jump / Grounded / FreeFall … ThirdPersonController が今まで通り流す
    ///   WeaponType (int) … <see cref="Weapon"/> の値。持ち替えたら入れる
    ///   Attack (trigger) … 攻撃を始めた瞬間に立てる。弓なら立てた瞬間に矢を放つ姿勢に入る
    /// </summary>
    public static class CharacterAnimatorBuilder
    {
        public const string ControllerPath = "Assets/_Project/Animation/Character/CharacterAnimation.controller";
        public const string UpperBodyMaskPath = "Assets/_Project/Animation/Character/UpperBody.mask";
        public const string BowAimClipPath = "Assets/_Project/Animation/Character/BowAim.anim";

        /// <summary>WeaponType に入れる値。数値は Animator の条件に焼き込まれるので並べ替えないこと。</summary>
        public enum Weapon
        {
            Unarmed = 0,
            OneHanded = 1,
            TwoHanded = 2,
            Bow = 3,
            Magic = 4,
        }

        public const string SpeedParam = "Speed";
        public const string MotionSpeedParam = "MotionSpeed";
        public const string JumpParam = "Jump";
        public const string GroundedParam = "Grounded";
        public const string FreeFallParam = "FreeFall";
        public const string WeaponTypeParam = "WeaponType";
        public const string AttackParam = "Attack";

        private const string StarterAnimations = "Assets/ThirdParty/Starter Assets/Runtime/ThirdPersonController/Character/Animations/";
        private const string BlinkCombat = "Assets/ThirdParty/Blink/Art/Animations/Animations_Starter_Pack/Combat/";

        // BowShot（30fps・29 フレーム）の中身。手の位置を 1 フレームずつ見て決めた。
        //   0-7F: つがえた矢を引く / 7-13F: 引き切って保持 / 14F: 放す / 22-29F: 次の矢をつがえて 0F と同じ姿勢に戻る
        private const float BowFrames = 29f;
        /// <summary>引き切って静止している区間の真ん中。ここを構えとして焼き出す。</summary>
        private const float BowFullDrawTime = 10f / 30f;
        /// <summary>引き終わる瞬間。Draw ステートはここで構えに繋ぐ。</summary>
        private const float BowDrawEnd = 7f / BowFrames;
        /// <summary>放す直前。攻撃はここから再生するので、トリガーと同時に弦が弾ける。</summary>
        private const float BowReleaseStart = 13f / BowFrames;

        // SpellCast（2.2 秒）は前半 1 秒以上が溜めで、撃ち出し（手を前に突き出す）は 1.3 秒あたり。
        // TPS で押してから 1 秒以上待たされると重いので、溜めの終わりから再生する。
        private const float SpellCastStart = 33f / 66f;
        private const float SpellCastExit = 58f / 66f;

        [MenuItem("Tools/TPS Dungeon/Player/Generate Character Animator")]
        public static void GenerateFromMenu()
        {
            Debug.Log(Generate());
        }

        /// <summary>コントローラ一式を作り直して、何をしたかのログを返す。</summary>
        public static string Generate()
        {
            var clips = new ClipSet();
            if (!clips.TryLoad(out string missing))
            {
                return "素材のクリップが見つからないので何もしていない: " + missing;
            }

            AnimationClip bowAim = BakeBowAim(clips.BowShot);
            AvatarMask upperBody = EnsureUpperBodyMask();
            AnimatorController controller = ResetController();

            AddParameters(controller);
            BuildBaseLayer(controller, clips);
            BuildUpperBodyLayer(controller, clips, bowAim, upperBody);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return "キャラクターの AnimatorController を作り直した: " + ControllerPath
                   + "\n  上半身マスク: " + UpperBodyMaskPath
                   + "\n  弓の構え（BowShot の引き切り姿勢を焼き出したもの）: " + BowAimClipPath;
        }

        /// <summary>
        /// 既存のコントローラは GUID を保ったまま中身だけ空にする。
        /// 作り直すとプレハブなどからの参照が切れるため。
        /// </summary>
        private static AnimatorController ResetController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                return AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            {
                if (sub != null && sub != controller) Object.DestroyImmediate(sub, true);
            }

            controller.layers = new AnimatorControllerLayer[0];
            controller.parameters = new AnimatorControllerParameter[0];
            return controller;
        }

        private static void AddParameters(AnimatorController controller)
        {
            controller.AddParameter(SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(MotionSpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(JumpParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(GroundedParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(FreeFallParam, AnimatorControllerParameterType.Bool);
            controller.AddParameter(WeaponTypeParam, AnimatorControllerParameterType.Int);
            controller.AddParameter(AttackParam, AnimatorControllerParameterType.Trigger);
        }

        // ---- Base Layer: 移動とジャンプ。Starter Assets の構成と遷移の値をそのまま引き継いでいる ----

        private static void BuildBaseLayer(AnimatorController controller, ClipSet clips)
        {
            controller.AddLayer("Base Layer");
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            sm.entryPosition = new Vector3(220, 330);
            sm.anyStatePosition = new Vector3(10, 330);
            sm.exitPosition = new Vector3(680, 260);

            BlendTree locomotion = CreateSpeedBlend(controller, "Locomotion",
                clips.Idle, clips.Walk, clips.Run);
            BlendTree land = CreateSpeedBlend(controller, "Land",
                clips.JumpLand, clips.WalkLand, clips.RunLand);

            AnimatorState move = sm.AddState("Idle Walk Run Blend", new Vector3(200, 400));
            move.motion = locomotion;
            move.speedParameterActive = true;
            move.speedParameter = MotionSpeedParam;
            move.iKOnFeet = true;

            AnimatorState jumpStart = sm.AddState("JumpStart", new Vector3(0, 490));
            jumpStart.motion = clips.JumpStart;
            jumpStart.iKOnFeet = true;

            AnimatorState inAir = sm.AddState("InAir", new Vector3(200, 600));
            inAir.motion = clips.InAir;
            inAir.iKOnFeet = true;

            AnimatorState jumpLand = sm.AddState("JumpLand", new Vector3(400, 490));
            jumpLand.motion = land;

            sm.defaultState = move;

            AnimatorStateTransition t;

            t = Transition(move, jumpStart, 0.07f);
            t.AddCondition(AnimatorConditionMode.If, 0, JumpParam);

            t = Transition(move, inAir, 0.0375f, offset: 0.23f);
            t.AddCondition(AnimatorConditionMode.If, 0, FreeFallParam);

            t = Transition(jumpStart, inAir, 0.47f, exitTime: 0.66f, offset: 0.61f);

            t = Transition(inAir, jumpLand, 0.098f, offset: 0.08f);
            t.AddCondition(AnimatorConditionMode.If, 0, GroundedParam);

            t = Transition(jumpLand, move, 0.434f, exitTime: 0.4f, offset: 0.363f);
            t.interruptionSource = TransitionInterruptionSource.Destination;
        }

        private static BlendTree CreateSpeedBlend(AnimatorController controller, string name,
            Motion idle, Motion walk, Motion run)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = SpeedParam,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            // ThirdPersonController の歩き 2 m/s・走り 6 m/s に合わせた閾値。
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 2f);
            tree.AddChild(run, 6f);
            return tree;
        }

        // ---- UpperBody: 武器ごとの構えと攻撃 ----

        private static void BuildUpperBodyLayer(AnimatorController controller, ClipSet clips,
            AnimationClip bowAim, AvatarMask mask)
        {
            controller.AddLayer("UpperBody");
            AnimatorControllerLayer[] layers = controller.layers;
            layers[1].avatarMask = mask;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[1].defaultWeight = 1f;
            controller.layers = layers;

            AnimatorStateMachine sm = layers[1].stateMachine;
            sm.entryPosition = new Vector3(20, 200);
            sm.anyStatePosition = new Vector3(20, 60);
            sm.exitPosition = new Vector3(20, 400);

            // Motion の無いステートは何も書かないので、下の層（移動の腕振り）がそのまま透ける。
            // 近接と魔法は構えを持たず、振るときだけ上半身を奪う。
            AnimatorState free = sm.AddState("Free", new Vector3(300, 200));
            sm.defaultState = free;

            AddOneShotAttack(sm, free, "Unarmed Attack", clips.Punch, Weapon.Unarmed,
                new Vector3(600, 0), startAt: 0f, exitAt: 0.8f);
            AddOneShotAttack(sm, free, "OneHanded Attack", clips.OneHanded, Weapon.OneHanded,
                new Vector3(600, 80), startAt: 0f, exitAt: 0.85f);
            AddOneShotAttack(sm, free, "TwoHanded Attack", clips.TwoHanded, Weapon.TwoHanded,
                new Vector3(600, 160), startAt: 0f, exitAt: 0.85f);
            AddOneShotAttack(sm, free, "Magic Attack", clips.SpellCast, Weapon.Magic,
                new Vector3(600, 240), startAt: SpellCastStart, exitAt: SpellCastExit);

            BuildBow(sm, free, clips, bowAim);
        }

        private static void AddOneShotAttack(AnimatorStateMachine sm, AnimatorState free, string name,
            AnimationClip clip, Weapon weapon, Vector3 position, float startAt, float exitAt)
        {
            AnimatorState attack = sm.AddState(name, position);
            attack.motion = clip;

            AnimatorStateTransition t = Transition(free, attack, 0.08f, offset: startAt);
            t.AddCondition(AnimatorConditionMode.If, 0, AttackParam);
            t.AddCondition(AnimatorConditionMode.Equals, (int)weapon, WeaponTypeParam);

            Transition(attack, free, 0.25f, exitTime: exitAt);
        }

        /// <summary>
        /// 弓は持っている間ずっと引き切って構え、Attack で放す瞬間から再生する。
        /// 放したあとは次の矢をつがえ（クリップ末尾）、引き直して（Draw）また構えに戻る。
        /// </summary>
        private static void BuildBow(AnimatorStateMachine sm, AnimatorState free, ClipSet clips,
            AnimationClip bowAim)
        {
            AnimatorState draw = sm.AddState("Bow Draw", new Vector3(300, 380));
            draw.motion = clips.BowShot;

            AnimatorState aim = sm.AddState("Bow Aim", new Vector3(600, 380));
            aim.motion = bowAim;

            AnimatorState release = sm.AddState("Bow Release", new Vector3(450, 500));
            release.motion = clips.BowShot;

            AnimatorStateTransition t;

            t = Transition(free, draw, 0.2f);
            t.AddCondition(AnimatorConditionMode.Equals, (int)Weapon.Bow, WeaponTypeParam);

            Transition(draw, aim, 0.05f, exitTime: BowDrawEnd);

            // 溶け込み時間を短くしないと、弦を放すはずの瞬間がブレンドでぼやける。
            t = Transition(aim, release, 0.03f, offset: BowReleaseStart);
            t.AddCondition(AnimatorConditionMode.If, 0, AttackParam);
            // 持ち替えと攻撃が同じフレームに来たとき、弓で放たずに持ち替え先の攻撃へトリガーを譲る。
            t.AddCondition(AnimatorConditionMode.Equals, (int)Weapon.Bow, WeaponTypeParam);

            // クリップ末尾は 0F と同じ「つがえただけ」の姿勢なので、Draw の頭へ段差なく繋がる。
            Transition(release, draw, 0.05f, exitTime: 0.97f);

            foreach (AnimatorState state in new[] { draw, aim, release })
            {
                t = Transition(state, free, 0.2f);
                t.AddCondition(AnimatorConditionMode.NotEqual, (int)Weapon.Bow, WeaponTypeParam);
            }
        }

        /// <summary>
        /// exitTime を渡したときだけ終了時刻で抜ける遷移になる。
        /// 時間は全部秒で持つ（ステートの長さが変わっても溶け込みの体感が揃うように）。
        /// </summary>
        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to,
            float duration, float? exitTime = null, float offset = 0f)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasFixedDuration = true;
            t.duration = duration;
            t.offset = offset;
            t.hasExitTime = exitTime.HasValue;
            t.exitTime = exitTime ?? 0f;
            return t;
        }

        // ---- 付随アセット ----

        /// <summary>
        /// BowShot の引き切った 1 フレームを、ループする静止クリップとして書き出す。
        /// 元クリップのステートを速度 0 で止める手もあるが、それだと構えの姿勢がステート設定に埋もれて見えない。
        /// </summary>
        private static AnimationClip BakeBowAim(AnimationClip source)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(BowAimClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "BowAim" };
                AssetDatabase.CreateAsset(clip, BowAimClipPath);
            }
            clip.ClearCurves();
            clip.frameRate = source.frameRate;

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                float value = curve.Evaluate(BowFullDrawTime);
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, 1f, value));
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            // 元クリップの歩きのズレを持ち込まないよう、ルートは姿勢基準で固定する。
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>背骨から上と両腕・手の IK。脚と足の IK、ルートは移動側に任せる。</summary>
        private static AvatarMask EnsureUpperBodyMask()
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = "UpperBody" };
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            var upper = new HashSet<AvatarMaskBodyPart>
            {
                AvatarMaskBodyPart.Body,
                AvatarMaskBodyPart.Head,
                AvatarMaskBodyPart.LeftArm,
                AvatarMaskBodyPart.RightArm,
                AvatarMaskBodyPart.LeftFingers,
                AvatarMaskBodyPart.RightFingers,
                AvatarMaskBodyPart.LeftHandIK,
                AvatarMaskBodyPart.RightHandIK,
            };
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                mask.SetHumanoidBodyPartActive(part, upper.Contains(part));
            }

            EditorUtility.SetDirty(mask);
            return mask;
        }

        private sealed class ClipSet
        {
            public AnimationClip Idle, Walk, Run;
            public AnimationClip JumpStart, InAir, JumpLand, WalkLand, RunLand;
            public AnimationClip Punch, OneHanded, TwoHanded, SpellCast, BowShot;

            private readonly List<string> _missing = new List<string>();

            public bool TryLoad(out string missing)
            {
                Idle = Load(StarterAnimations + "Stand--Idle.anim.fbx", "Idle");
                Walk = Load(StarterAnimations + "Locomotion--Walk_N.anim.fbx", "Walk_N");
                Run = Load(StarterAnimations + "Locomotion--Run_N.anim.fbx", "Run_N");
                JumpStart = Load(StarterAnimations + "Jump--Jump.anim.fbx", "JumpStart");
                InAir = Load(StarterAnimations + "Jump--InAir.anim.fbx", "InAir");
                JumpLand = Load(StarterAnimations + "Jump--Jump.anim.fbx", "JumpLand");
                WalkLand = Load(StarterAnimations + "Locomotion--Walk_N_Land.anim.fbx", "Walk_N_Land");
                RunLand = Load(StarterAnimations + "Locomotion--Run_N_Land.anim.fbx", "Run_N_Land");

                Punch = Load(BlinkCombat + "PunchRight.fbx", "PunchRight");
                OneHanded = Load(BlinkCombat + "MeleeAttack_OneHanded.fbx", "MeleeAttack_OneHanded");
                TwoHanded = Load(BlinkCombat + "MeleeAttack_TwoHanded.fbx", "MeleeAttack_TwoHanded");
                SpellCast = Load(BlinkCombat + "SpellCast.fbx", "SpellCast");
                BowShot = Load(BlinkCombat + "BowShot.fbx", "BowShot");

                missing = string.Join(", ", _missing);
                return _missing.Count == 0;
            }

            private AnimationClip Load(string path, string clipName)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is AnimationClip clip && clip.name == clipName) return clip;
                }
                _missing.Add(path + " の " + clipName);
                return null;
            }
        }
    }
}
