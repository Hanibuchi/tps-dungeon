using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TpsDungeon.Player.Editor
{
    /// <summary>
    /// プレイヤーの AnimatorController を組み立てる。
    /// 下半身（Base Layer）は Starter Assets の ThirdPersonController がそのまま動かせる移動とジャンプ、
    /// 上半身（UpperBody）は持っている武器ごとの構えと攻撃、
    /// 全身（Action、一番上）は Blink の Animations_Starter_Pack のクリップ全部を、呼ばれたときだけ再生する。
    /// 生成物なので手で編集せず、構成を変えたくなったらこのファイルを直して作り直すこと。
    ///
    /// スクリプトから触るパラメータ:
    ///   Speed / MotionSpeed / Jump / Grounded / FreeFall … ThirdPersonController が今まで通り流す
    ///   WeaponType (int) … <see cref="Weapon"/> の値。持ち替えたら入れる
    ///   Attack (trigger) … 攻撃を始めた瞬間に立てる。弓なら立てた瞬間に矢を放つ姿勢に入る
    ///   ComboStep (int) … 近接武器のコンボの段（0 始まり）。Attack より先に入れる
    ///   AttackSpeed (float) … 近接武器の振りの再生速度（速射のエンチャント）。既定 1
    ///   Action (int) / PlayAction (trigger) … <see cref="CharacterAction"/> の値を入れてトリガーを立てると全身で再生する（CharacterActions が叩く）
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
            DashThrust = 5,
            Hammer = 6,
        }

        public const string SpeedParam = "Speed";
        public const string MotionSpeedParam = "MotionSpeed";
        public const string JumpParam = "Jump";
        public const string GroundedParam = "Grounded";
        public const string FreeFallParam = "FreeFall";
        public const string WeaponTypeParam = "WeaponType";
        public const string AttackParam = "Attack";
        public const string ComboStepParam = "ComboStep";
        public const string AttackSpeedParam = "AttackSpeed";
        public const string ActionParam = CharacterActions.ActionParam;
        public const string PlayActionParam = CharacterActions.PlayActionParam;

        /// <summary>片手武器のコンボの段数。MeleeAttacker が武器種の段数ぶん ComboStep を回す。</summary>
        public const int OneHandedComboSteps = 4;

        /// <summary>素手のコンボの段数（右・左のパンチ）。</summary>
        public const int UnarmedComboSteps = 2;

        /// <summary>両手武器のコンボの段数。4 段目が締めの重い振り。</summary>
        public const int TwoHandedComboSteps = 4;

        // 両手・ダッシュ突き・ハンマーの再生速度。判定の瞬間（武器種の hitTime）に刃や槌が当たる所へ来るよう合わせる。
        // 右手の骨の軌跡を 2F ごとに見て決めた（2026-09-29）。
        // Kevin の両手の横薙ぎ（Attack2H01、30fps・48F）は 17F（0.57 秒）で手が正面を横切る → 1.35 倍で 0.42 秒。
        private const float TwoHandedSweepSpeed = 1.35f;
        // Blink の両手の振り下ろし（MeleeAttack_TwoHanded）は 0.8 秒で正面へ振り下ろし、0.83 秒で下まで下りきる。
        // 締めは 1.2 倍で 0.67 秒、ハンマーは 1.1 倍で地面に着く 0.76 秒。
        private const float TwoHandedFinisherSpeed = 1.2f;
        private const float HammerSlamSpeed = 1.1f;
        // Kevin の長柄の突き（AttackPolearm01、30fps・41F）は 12F（0.4 秒）で伸び切る → 2 倍で 0.2 秒。走り出しに合わせて速く突く。
        // 同じクリップは全身の動き（CharacterAction.KevinAttackPolearm）でも再生できる。
        private const float DashThrustSpeed = 2f;

        private const string StarterAnimations = "Assets/ThirdParty/3D Model/Starter Assets/Runtime/ThirdPersonController/Character/Animations/";
        private const string BlinkPack = "Assets/ThirdParty/3D Model/Blink/Character/Animations/Animations_Starter_Pack/";
        private const string BlinkCombat = BlinkPack + "Combat/";
        private const string KevinCombat = "Assets/ThirdParty/Animation/Kevin Iglesias/Human Animations/Animations/Male/Combat/";

        /// <summary>
        /// Action 層に置く動きと、その元のクリップ（ファイルとクリップ名）。
        /// Blink のパックは tpose 以外全部。Kevin の Male/Combat は、武器の攻撃で使う Attack1H01_R・Attack2H01 以外。
        /// </summary>
        private static readonly (CharacterAction action, string file, string clip)[] ActionClips =
        {
            (CharacterAction.BlockingLoop, BlinkPack + "Combat/BlockingLoop.fbx", "BlockingLoop"),
            (CharacterAction.BowShot, BlinkPack + "Combat/BowShot.fbx", "BowShot"),
            (CharacterAction.Buff, BlinkPack + "Combat/Buff.fbx", "Buff"),
            (CharacterAction.CastingLoop, BlinkPack + "Combat/CastingLoop.fbx", "CastingLoop"),
            (CharacterAction.Death, BlinkPack + "Combat/Death.fbx", "Death"),
            (CharacterAction.GetHit, BlinkPack + "Combat/GetHit.fbx", "GetHit"),
            (CharacterAction.IdleCombat, BlinkPack + "Combat/IdleCombat.fbx", "IdleCombat"),
            (CharacterAction.MeleeAttackOneHanded, BlinkPack + "Combat/MeleeAttack_OneHanded.fbx", "MeleeAttack_OneHanded"),
            (CharacterAction.MeleeAttackTwoHanded, BlinkPack + "Combat/MeleeAttack_TwoHanded.fbx", "MeleeAttack_TwoHanded"),
            (CharacterAction.PunchLeft, BlinkPack + "Combat/PunchLeft.fbx", "PunchLeft"),
            (CharacterAction.PunchRight, BlinkPack + "Combat/PunchRight.fbx", "PunchRight"),
            (CharacterAction.SpellCast, BlinkPack + "Combat/SpellCast.fbx", "SpellCast"),
            (CharacterAction.SpellCastStart, BlinkPack + "Combat/SpellCast.fbx", "SpellCast_Start"),
            (CharacterAction.SpellCastEnd, BlinkPack + "Combat/SpellCast.fbx", "SpellCast_End"),
            (CharacterAction.StunnedLoop, BlinkPack + "Combat/StunnedLoop.fbx", "StunnedLoop"),
            (CharacterAction.Gathering, BlinkPack + "Gathering/Gathering.fbx", "Gathering"),
            (CharacterAction.MiningLoop, BlinkPack + "Gathering/MiningLoop.fbx", "MiningLoop"),
            (CharacterAction.FallingLoop, BlinkPack + "Movement/FallingLoop.fbx", "FallingLoop"),
            (CharacterAction.Idle, BlinkPack + "Movement/Idle.fbx", "Idle"),
            (CharacterAction.Jump, BlinkPack + "Movement/Jumps.fbx", "Jump"),
            (CharacterAction.JumpUp, BlinkPack + "Movement/Jumps.fbx", "Jump_Up"),
            (CharacterAction.JumpDown, BlinkPack + "Movement/Jumps.fbx", "Jump_Down"),
            (CharacterAction.JumpWhileRunning, BlinkPack + "Movement/JumpWhileRunning.fbx", "JumpWhileRunning"),
            (CharacterAction.RollBackward, BlinkPack + "Movement/RollBackward.fbx", "RollBackward"),
            (CharacterAction.RollForward, BlinkPack + "Movement/RollForward.fbx", "RollForward"),
            (CharacterAction.RollLeft, BlinkPack + "Movement/RollLeft.fbx", "RollLeft"),
            (CharacterAction.RollRight, BlinkPack + "Movement/RollRight.fbx", "RollRight"),
            (CharacterAction.RunBackward, BlinkPack + "Movement/RunBackward.fbx", "RunBackward"),
            (CharacterAction.RunBackwardLeft, BlinkPack + "Movement/RunBackwardLeft.fbx", "RunBackwardLeft"),
            (CharacterAction.RunBackwardRight, BlinkPack + "Movement/RunBackwardRight.fbx", "RunBackwardRight"),
            (CharacterAction.RunForward, BlinkPack + "Movement/RunForward.fbx", "RunForward"),
            (CharacterAction.RunLeft, BlinkPack + "Movement/RunLeft.fbx", "RunLeft"),
            (CharacterAction.RunRight, BlinkPack + "Movement/RunRight.fbx", "RunRight"),
            (CharacterAction.Sprint, BlinkPack + "Movement/Sprint.fbx", "Sprint"),
            (CharacterAction.StrafeLeft, BlinkPack + "Movement/StrafeLeft.fbx", "StrafeLeft"),
            (CharacterAction.StrafeRight, BlinkPack + "Movement/StrafeRight.fbx", "StrafeRight"),
            (CharacterAction.KevinCombatIdle, KevinCombat + "HumanM@CombatIdle01.fbx", "HumanM@CombatIdle01"),
            (CharacterAction.KevinGetHit, KevinCombat + "HumanM@CombatDamage01.fbx", "HumanM@CombatDamage01"),
            (CharacterAction.KevinDeath, KevinCombat + "HumanM@Death01.fbx", "HumanM@Death01"),
            (CharacterAction.KevinAttackShield, KevinCombat + "Shield/HumanM@AttackShield01.fbx", "HumanM@AttackShield01"),
            (CharacterAction.KevinAttack1HLeft, KevinCombat + "1H/HumanM@Attack1H01_L.fbx", "HumanM@Attack1H01_L"),
            (CharacterAction.KevinCombatIdle1H, KevinCombat + "1H/HumanM@CombatIdle1H01.fbx", "HumanM@CombatIdle1H01"),
            (CharacterAction.KevinCombatIdle2H, KevinCombat + "2H/HumanM@CombatIdle2H01.fbx", "HumanM@CombatIdle2H01"),
            (CharacterAction.KevinCombatIdlePolearm, KevinCombat + "Polearm/HumanM@CombatIdlePolearm01.fbx", "HumanM@CombatIdlePolearm01"),
            (CharacterAction.KevinAttackPolearm, KevinCombat + "Polearm/HumanM@AttackPolearm01.fbx", "HumanM@AttackPolearm01"),
        };

        /// <summary>ループしないのに、終わっても戻らず最後の姿勢のまま止める動き。</summary>
        private static readonly HashSet<CharacterAction> HoldAtEnd = new HashSet<CharacterAction> { CharacterAction.Death, CharacterAction.KevinDeath };

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
            BuildActionLayer(controller, clips);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return "キャラクターの AnimatorController を作り直した: " + ControllerPath
                   + "\n  上半身マスク: " + UpperBodyMaskPath
                   + "\n  弓の構え（BowShot の引き切り姿勢を焼き出したもの）: " + BowAimClipPath
                   + $"\n  全身の動き（Action 層）: {ActionClips.Length} 本";
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
            controller.AddParameter(ComboStepParam, AnimatorControllerParameterType.Int);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = AttackSpeedParam,
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });
            controller.AddParameter(ActionParam, AnimatorControllerParameterType.Int);
            controller.AddParameter(PlayActionParam, AnimatorControllerParameterType.Trigger);
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

            AddOneShotAttack(sm, free, "Magic Attack", clips.SpellCast, Weapon.Magic,
                new Vector3(600, 240), startAt: SpellCastStart, exitAt: SpellCastExit);

            BuildBow(sm, free, clips, bowAim);
            BuildOneHandedCombo(sm, free, clips);
            BuildUnarmedCombo(sm, free, clips);
            BuildTwoHandedCombo(sm, free, clips);
            BuildCombo(sm, free, "DashThrust", Weapon.DashThrust,
                new (Motion, bool, float)[] { (clips.PolearmThrust, false, DashThrustSpeed) }, new Vector3(1500, -40));
            BuildCombo(sm, free, "Hammer", Weapon.Hammer,
                new (Motion, bool, float)[] { (clips.TwoHanded, false, HammerSlamSpeed) }, new Vector3(1500, 80));
        }

        /// <summary>
        /// 両手武器の 4 段コンボ。1〜3 段目は Kevin の両手の横薙ぎ（2 段目は左右反転で返し斬り）、
        /// 4 段目は Blink の両手の振り下ろしで締める。
        /// </summary>
        private static void BuildTwoHandedCombo(AnimatorStateMachine sm, AnimatorState free, ClipSet clips)
        {
            var steps = new (Motion motion, bool mirror, float speed)[TwoHandedComboSteps];
            for (int i = 0; i < steps.Length; i++)
            {
                bool finisher = i == steps.Length - 1;
                steps[i] = finisher ? (clips.TwoHanded, false, TwoHandedFinisherSpeed) : (clips.TwoHandedSweep, i % 2 == 1, TwoHandedSweepSpeed);
            }

            BuildCombo(sm, free, "TwoHanded", Weapon.TwoHanded, steps, new Vector3(1200, -40));
        }

        /// <summary>
        /// 片手武器の 4 段コンボ。1・3 段目は右上からの振り下ろし、2 段目は Kevin Iglesias の右手の突き（0.30 秒で伸び切る）、
        /// 4 段目は両手武器の重い振りで代用する。剣は右手に持たせるので、左右反転（左手で振る）は使わない。
        /// 両手武器の振りは振り下ろしが 0.8 秒と遅いので 1.6 倍で回し、0.5 秒で当たるようにする（武器種の hitTime と揃える）。
        /// </summary>
        private static void BuildOneHandedCombo(AnimatorStateMachine sm, AnimatorState free, ClipSet clips)
        {
            var steps = new (Motion motion, bool mirror, float speed)[OneHandedComboSteps];
            for (int i = 0; i < steps.Length; i++)
            {
                bool finisher = i == steps.Length - 1;
                if (finisher) steps[i] = (clips.TwoHanded, false, 1.6f);
                else steps[i] = (i % 2 == 1 ? clips.OneHandedThrust : clips.OneHanded, false, 1f);
            }

            BuildCombo(sm, free, "OneHanded", Weapon.OneHanded, steps, new Vector3(900, -40));
        }

        /// <summary>素手の 2 段コンボ。右・左のパンチを交互に出す。素材は腕が伸び切るまで 0.42 秒かかるので 1.4 倍で回す（0.3 秒で当たる）。</summary>
        private static void BuildUnarmedCombo(AnimatorStateMachine sm, AnimatorState free, ClipSet clips)
        {
            BuildCombo(sm, free, "Unarmed", Weapon.Unarmed,
                new (Motion, bool, float)[] { (clips.Punch, false, 1.4f), (clips.PunchLeft, false, 1.4f) }, new Vector3(600, -40));
        }

        /// <summary>
        /// 段ごとのステートを並べる。どの段からでも、次の段の ComboStep で Attack が立てば途中から切り替わる（先行入力で繋がる）。
        /// 最後の段のあとは 1 段目へ戻る。
        /// </summary>
        private static void BuildCombo(AnimatorStateMachine sm, AnimatorState free, string name, Weapon weapon,
            (Motion motion, bool mirror, float speed)[] steps, Vector3 position)
        {
            var states = new AnimatorState[steps.Length];
            for (int i = 0; i < states.Length; i++)
            {
                AnimatorState state = sm.AddState($"{name} Attack {i + 1}", position + new Vector3(0, 80 * i));
                state.motion = steps[i].motion;
                state.mirror = steps[i].mirror;
                state.speed = steps[i].speed; // AttackSpeed はこれに掛かる
                state.speedParameterActive = true;
                state.speedParameter = AttackSpeedParam;
                states[i] = state;

                Transition(state, free, 0.25f, exitTime: 0.85f);
            }

            for (int i = 0; i < states.Length; i++)
            {
                AddComboEntry(free, states[i], weapon, i);
                int previous = (i + states.Length - 1) % states.Length;
                if (previous != i) AddComboEntry(states[previous], states[i], weapon, i);
            }
        }

        // ---- Action: パックのクリップを全身で再生する ----

        /// <summary>
        /// 一番上に重ねる全身の層。None（Motion 無し）の間は下の層がそのまま見える。
        /// どこからでも「PlayAction かつ Action=n」で n の動きへ入り直す（同じ動きなら頭から）。
        /// 抜け方: Action が n でなくなったら抜ける。ループしない動き（Death を除く）は終わり際にも勝手に抜ける。
        /// </summary>
        private static void BuildActionLayer(AnimatorController controller, ClipSet clips)
        {
            controller.AddLayer("Action");
            AnimatorControllerLayer[] layers = controller.layers;
            int index = layers.Length - 1;
            layers[index].blendingMode = AnimatorLayerBlendingMode.Override;
            layers[index].defaultWeight = 1f;
            controller.layers = layers;

            AnimatorStateMachine sm = layers[index].stateMachine;
            sm.entryPosition = new Vector3(20, 200);
            sm.anyStatePosition = new Vector3(20, 60);
            sm.exitPosition = new Vector3(20, 400);

            AnimatorState none = sm.AddState(CharacterAction.None.ToString(), new Vector3(300, 200));
            sm.defaultState = none;

            for (int i = 0; i < ActionClips.Length; i++)
            {
                (CharacterAction action, _, _) = ActionClips[i];
                AnimationClip clip = clips.Actions[i];
                AnimatorState state = sm.AddState(action.ToString(), new Vector3(650 + 260 * (i / 12), -100 + 60 * (i % 12)));
                state.motion = clip;

                AnimatorStateTransition enter = sm.AddAnyStateTransition(state);
                enter.hasFixedDuration = true;
                enter.duration = 0.12f;
                enter.hasExitTime = false;
                enter.canTransitionToSelf = true;
                enter.AddCondition(AnimatorConditionMode.If, 0, PlayActionParam);
                enter.AddCondition(AnimatorConditionMode.Equals, (int)action, ActionParam);

                AnimatorStateTransition cancel = Transition(state, none, 0.2f);
                cancel.AddCondition(AnimatorConditionMode.NotEqual, (int)action, ActionParam);

                if (!clip.isLooping && !HoldAtEnd.Contains(action)) Transition(state, none, 0.2f, exitTime: 0.9f);
            }
        }

        private static void AddComboEntry(AnimatorState from, AnimatorState to, Weapon weapon, int step)
        {
            AnimatorStateTransition t = Transition(from, to, 0.08f);
            t.AddCondition(AnimatorConditionMode.If, 0, AttackParam);
            t.AddCondition(AnimatorConditionMode.Equals, (int)weapon, WeaponTypeParam);
            t.AddCondition(AnimatorConditionMode.Equals, step, ComboStepParam);
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
            public AnimationClip Punch, PunchLeft, OneHanded, OneHandedThrust, TwoHanded, TwoHandedSweep, PolearmThrust, SpellCast, BowShot;
            public AnimationClip[] Actions;

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
                PunchLeft = Load(BlinkCombat + "PunchLeft.fbx", "PunchLeft");
                OneHanded = Load(BlinkCombat + "MeleeAttack_OneHanded.fbx", "MeleeAttack_OneHanded");
                OneHandedThrust = Load(KevinCombat + "1H/HumanM@Attack1H01_R.fbx", "HumanM@Attack1H01_R");
                TwoHanded = Load(BlinkCombat + "MeleeAttack_TwoHanded.fbx", "MeleeAttack_TwoHanded");
                TwoHandedSweep = Load(KevinCombat + "2H/HumanM@Attack2H01.fbx", "HumanM@Attack2H01");
                PolearmThrust = Load(KevinCombat + "Polearm/HumanM@AttackPolearm01.fbx", "HumanM@AttackPolearm01");
                SpellCast = Load(BlinkCombat + "SpellCast.fbx", "SpellCast");
                BowShot = Load(BlinkCombat + "BowShot.fbx", "BowShot");

                Actions = new AnimationClip[ActionClips.Length];
                for (int i = 0; i < ActionClips.Length; i++) Actions[i] = Load(ActionClips[i].file, ActionClips[i].clip);

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
