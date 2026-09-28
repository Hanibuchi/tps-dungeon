using System.Collections.Generic;
using System.Linq;
using TpsDungeon.Progression;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TpsDungeon.Enemies.Editor
{
    /// <summary>
    /// Quaternius のモンスターから、置けば動く敵プレハブを作る。
    ///
    /// 体のつくりで 3 グループ（Big / Flying / Blob）に分かれていて、同じグループならクリップ名がそろっている。
    /// そこでグループごとに基本の AnimatorController を 1 つ組み（見本のモンスターのクリップを入れる）、
    /// モンスターごとに AnimatorOverrideController で同じ名前の自分のクリップへ差し替える。
    /// 生成物なので手で編集せず、構成を変えたくなったらこのファイルを直して作り直すこと。
    ///
    /// スクリプトから触るパラメータ（全グループ共通）:
    ///   Speed (float)    … 移動の速さ（m/s）。0 で待機、上げると歩き→走り（飛ぶものは速く飛ぶ）
    ///   Attack (trigger) … 攻撃を 1 回（グループの <see cref="Group.Attack"/> のクリップ）
    ///   Hit (trigger)    … 被弾のひるみ。続けて当たると頭から流し直す
    ///   Dead (bool)      … true で倒れて、最後のポーズのまま止まる
    ///
    /// 上の 4 つで使わないクリップも、見本のモンスターが持っているものは全部ステートにしてある（ステート名＝クリップ名）:
    ///   ループしないクリップ（Wave / Yes / Jump …）… 同じ名前の trigger。1 回流して移動に戻る
    ///   ループするクリップ（Dance / Jump_Idle …）  … 同じ名前の bool。true の間ずっと流す
    /// ステート名がクリップ名なので、animator.CrossFade("Wave", 0.1f) のように直接流してもよい。
    /// 死んでいる間（Dead が true）は Dead を戻すまでどれも流れない。
    ///
    /// プレハブは「ルート（CapsuleCollider・EnemyHealth・EnemyDamageReaction・EnemyRagdoll・EnemyDeath・EnemyExpReward）
    /// ＋子の Model（FBX と Animator）」。足元がルートの原点、背丈はグループの <see cref="Group.Height"/> にそろえる。
    /// ルートモーションは使わない。
    ///
    /// 気絶で倒れ込むためのラグドールも骨に焼き込む（<see cref="Group.Ragdoll"/>）。
    /// 骨ごとに Rigidbody（普段は kinematic）・CapsuleCollider（普段は無効）・親の骨への CharacterJoint。
    /// </summary>
    public static class MonsterBuilder
    {
        private const string AnimationFolder = "Assets/_Project/Animation/Enemy";
        private const string OverrideFolder = AnimationFolder + "/Overrides";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Enemies";

        // 仮の効果音（効果音ラボ）。見つからなければ空のまま。
        private const string StunClipPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/弓矢が刺さる (1).mp3";
        private const string FaintClipPath = "Assets/ThirdParty/Sound/SoundEffect-Lab/翼竜の鳴き声1.mp3";

        public const string SpeedParam = "Speed";
        public const string AttackParam = "Attack";
        public const string HitParam = "Hit";
        public const string DeadParam = "Dead";

        /// <summary>
        /// ラグドールの 1 本の骨。Parent が null なら腰（押し出しと起き上がりの基準、1 本だけ）。
        /// End の骨の位置までをカプセルにする。End が null なら骨の位置の上に Radius の球を置く。
        /// Radius は見た目の大きさ（m、プレハブに置いたときの）。
        /// </summary>
        private readonly struct RagdollBone
        {
            public readonly string Bone;
            public readonly string Parent;
            public readonly string End;
            public readonly float Radius;
            public readonly float Mass;

            public RagdollBone(string bone, string parent, string end, float radius, float mass = 1f)
            {
                Bone = bone;
                Parent = parent;
                End = end;
                Radius = radius;
                Mass = mass;
            }
        }

        private sealed class Group
        {
            public string Name;
            /// <summary>基本のコントローラにクリップを入れる見本のモンスター。</summary>
            public string ReferenceModel;
            /// <summary>Speed で切り替える移動のクリップと、その速さ（m/s）。先頭が待機。</summary>
            public (string clip, float speed)[] Locomotion;
            public string Attack;
            public string Hit;
            public string Death;
            /// <summary>待機の最初のポーズでの背丈（m）。プレイヤーはおよそ 1.8 m。</summary>
            public float Height;
            public float Radius;
            /// <summary>ラグドールにする骨。先頭が腰。</summary>
            public RagdollBone[] Ragdoll;
            /// <summary>倒れている間、Target の骨にくっついていく骨（物理の骨の子になっていない IK 用の足首など）。</summary>
            public (string follower, string target)[] Followers = new (string, string)[0];

            /// <summary>見本に必ず要るクリップ。これ以外に見本が持っているクリップも全部ステートになる。</summary>
            public IEnumerable<string> RequiredClips =>
                Locomotion.Select(l => l.clip).Append(Attack).Append(Hit).Append(Death);

            public string ControllerPath =>$"{AnimationFolder}/{Name}Monster.controller";
            public string ModelPath(string model) => $"{QuaterniusModelPostprocessor.Root}{Name}/FBX/{model}.fbx";
        }

        private static readonly Group Big = new Group
        {
            Name = "Big",
            ReferenceModel = "Orc",
            Locomotion = new[] { ("Idle", 0f), ("Walk", 1.5f), ("Run", 4f) },
            Attack = "Punch",
            Hit = "HitReact",
            Death = "Death",
            Height = 2.0f,
            Radius = 0.45f,
            // 脚は Hips ではなく Body の子。足首（Foot）は IK 用に Root の直下にある。
            Ragdoll = new[]
            {
                new RagdollBone("Body", null, "Abdomen", 0.28f, 3f),
                new RagdollBone("Torso", "Body", "Neck", 0.32f, 2f),
                new RagdollBone("Head", "Torso", "Head_end", 0.22f),
                new RagdollBone("UpperArm.L", "Torso", "LowerArm.L", 0.11f),
                new RagdollBone("LowerArm.L", "UpperArm.L", "Middle1.L", 0.1f),
                new RagdollBone("UpperArm.R", "Torso", "LowerArm.R", 0.11f),
                new RagdollBone("LowerArm.R", "UpperArm.R", "Middle1.R", 0.1f),
                new RagdollBone("UpperLeg.L", "Body", "LowerLeg.L", 0.13f),
                new RagdollBone("LowerLeg.L", "UpperLeg.L", "LowerLeg.L_end", 0.11f),
                new RagdollBone("UpperLeg.R", "Body", "LowerLeg.R", 0.13f),
                new RagdollBone("LowerLeg.R", "UpperLeg.R", "LowerLeg.R_end", 0.11f),
            },
            Followers = new[] { ("Foot.L", "LowerLeg.L"), ("Foot.R", "LowerLeg.R") },
        };

        private static readonly Group Flying = new Group
        {
            Name = "Flying",
            ReferenceModel = "Ghost",
            Locomotion = new[] { ("Flying_Idle", 0f), ("Fast_Flying", 4f) },
            Attack = "Headbutt",
            Hit = "HitReact",
            Death = "Death",
            Height = 1.2f,
            Radius = 0.45f,
            // 胴（Torso）と尾（Body1〜4）は Root の下で兄弟になっている。
            Ragdoll = new[]
            {
                new RagdollBone("Torso", null, "Neck", 0.25f, 2f),
                new RagdollBone("Head", "Torso", "Head_end", 0.24f),
                new RagdollBone("Body1", "Torso", "Body4", 0.16f),
                new RagdollBone("UpperArm.L", "Torso", "LowerArm.L", 0.07f),
                new RagdollBone("LowerArm.L", "UpperArm.L", "Middle1.L", 0.06f),
                new RagdollBone("UpperArm.R", "Torso", "LowerArm.R", 0.07f),
                new RagdollBone("LowerArm.R", "UpperArm.R", "Middle1.R", 0.06f),
            },
        };

        private static readonly Group Blob = new Group
        {
            Name = "Blob",
            ReferenceModel = "GreenBlob",
            Locomotion = new[] { ("Idle", 0f), ("Walk", 1.5f) },
            Attack = "Bite_Front",
            Hit = "HitRecieve",
            Death = "Death",
            Height = 0.9f,
            Radius = 0.4f,
            Ragdoll = new[]
            {
                new RagdollBone("Body", null, null, 0.3f, 2f),
                new RagdollBone("Head", "Body", "Head3_end", 0.25f),
            },
        };

        private static readonly Group[] Groups = { Big, Flying, Blob };

        /// <summary>
        /// プレハブにするモンスター。FBX のファイル名（拡張子なし）で足していけばよい。
        /// hp は最大 HP（<see cref="EnemyHealth"/>）、exp は倒したときの経験値（<see cref="EnemyExpReward"/>）。1 層を想定した仮の値。
        /// </summary>
        private static readonly (Group group, string model, int hp, int exp)[] Monsters =
        {
            (Big, "Orc", 60, 12),
            (Flying, "Ghost", 30, 6),
            (Blob, "GreenBlob", 20, 3),
        };

        [MenuItem("Tools/TPS Dungeon/Enemies/モンスターの Animator とプレハブを作る")]
        public static void GenerateFromMenu()
        {
            Debug.Log(Generate());
        }

        public static string Generate()
        {
            EnsureFolder(AnimationFolder);
            EnsureFolder(OverrideFolder);
            EnsureFolder(PrefabFolder);

            var log = new List<string>();
            var controllers = new Dictionary<Group, AnimatorController>();
            var referenceClips = new Dictionary<Group, string[]>();
            foreach (Group group in Groups)
            {
                if (!TryLoadClips(group.ModelPath(group.ReferenceModel), group.RequiredClips, out var clips, out string missing))
                {
                    return $"{group.Name} の見本 {group.ReferenceModel} のクリップが見つからないので何もしていない: {missing}";
                }
                controllers[group] = BuildController(group, clips);
                referenceClips[group] = clips.Keys.ToArray();
                log.Add($"コントローラ: {group.ControllerPath}（{clips.Count} クリップ）");
            }

            var stunClip = LoadClip(StunClipPath, log);
            var faintClip = LoadClip(FaintClipPath, log);

            foreach (var (group, model, hp, exp) in Monsters)
            {
                // 差し替えは名前で引くので、見本と同じ名前のクリップが全部そろっていないと骨組みの違う動きが残ってしまう。
                string path = group.ModelPath(model);
                if (!TryLoadClips(path, referenceClips[group], out var clips, out string missing))
                {
                    log.Add($"飛ばした {path}: {missing}");
                    continue;
                }
                var overrides = BuildOverride(group, model, controllers[group], clips);
                log.Add("プレハブ: " + BuildPrefab(group, model, hp, exp, overrides, clips[group.Locomotion[0].clip],
                    stunClip, faintClip));
            }

            AssetDatabase.SaveAssets();
            return "モンスターを作り直した\n  " + string.Join("\n  ", log);
        }

        private static AudioClip LoadClip(string path, List<string> log)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) log.Add($"効果音が見つからないので空のままにした: {path}");
            return clip;
        }

        // ---- クリップ ----

        private static bool TryLoadClips(string modelPath, IEnumerable<string> required,
            out Dictionary<string, AnimationClip> clips, out string missing)
        {
            clips = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name);

            var known = clips;
            missing = string.Join(", ", required.Where(n => !known.ContainsKey(n)));
            return missing.Length == 0;
        }

        // ---- コントローラ ----

        private static AnimatorController BuildController(Group group, Dictionary<string, AnimationClip> clips)
        {
            AnimatorController controller = ResetController(group.ControllerPath);
            controller.AddParameter(SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(AttackParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(HitParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(DeadParam, AnimatorControllerParameterType.Bool);
            controller.AddLayer("Base Layer");

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            sm.entryPosition = new Vector3(0, 0);
            sm.anyStatePosition = new Vector3(0, 200);
            sm.exitPosition = new Vector3(700, 0);

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = SpeedParam,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            foreach (var (clip, speed) in group.Locomotion) tree.AddChild(clips[clip], speed);

            AnimatorState move = sm.AddState("Locomotion", new Vector3(250, 0));
            move.motion = tree;
            sm.defaultState = move;

            // 移動以外のクリップは全部ステートにする（名前はクリップ名のまま）。
            var states = new Dictionary<string, AnimatorState>();
            var locomotion = new HashSet<string>(group.Locomotion.Select(l => l.clip));
            int row = 0;
            foreach (AnimationClip clip in clips.Values.Where(c => !locomotion.Contains(c.name)).OrderBy(c => c.name))
            {
                AnimatorState state = sm.AddState(clip.name, new Vector3(500 + (row / 8) * 250, (row % 8) * 70 - 100));
                state.motion = clip;
                states[clip.name] = state;
                row++;
            }

            AnimatorStateTransition t;

            // 共通の呼び名。続けて呼ばれたら頭から流し直す。
            AddOneShot(sm, states[group.Attack], move, AttackParam);
            AddOneShot(sm, states[group.Hit], move, HitParam);

            // Death はループしないので、流し終えたら最後のポーズで止まる。
            t = sm.AddAnyStateTransition(states[group.Death]);
            Configure(t, 0.1f);
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, DeadParam);

            // 残りはクリップと同じ名前のパラメータで流す。共通の呼び名で流せるものには作らない。
            var covered = new HashSet<string> { group.Attack, group.Hit, group.Death };
            foreach (var pair in states.Where(p => !covered.Contains(p.Key)))
            {
                if (pair.Value.motion is AnimationClip clip && clip.isLooping)
                {
                    controller.AddParameter(pair.Key, AnimatorControllerParameterType.Bool);
                    AddLoop(sm, pair.Value, move, pair.Key);
                }
                else
                {
                    controller.AddParameter(pair.Key, AnimatorControllerParameterType.Trigger);
                    AddOneShot(sm, pair.Value, move, pair.Key);
                }
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>trigger でどこからでも入り、1 回流したら移動に戻る。倒れている間は入らない。</summary>
        private static void AddOneShot(AnimatorStateMachine sm, AnimatorState state, AnimatorState back, string trigger)
        {
            // 自分自身へも移れるようにしておく。移れないと、流している最中の trigger が消費されずに残り、戻った直後にもう 1 回流れてしまう。
            AnimatorStateTransition t = sm.AddAnyStateTransition(state);
            Configure(t, 0.1f);
            t.canTransitionToSelf = true;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, DeadParam);

            t = state.AddTransition(back);
            Configure(t, 0.15f, exitTime: 0.85f);
        }

        /// <summary>bool が true の間ずっと流し、false に戻したら移動に戻る。</summary>
        private static void AddLoop(AnimatorStateMachine sm, AnimatorState state, AnimatorState back, string flag)
        {
            // bool は立ったままなので、自分自身へ移れると毎フレーム頭から流し直してしまう。
            AnimatorStateTransition t = sm.AddAnyStateTransition(state);
            Configure(t, 0.15f);
            t.canTransitionToSelf = false;
            t.AddCondition(AnimatorConditionMode.If, 0, flag);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, DeadParam);

            t = state.AddTransition(back);
            Configure(t, 0.15f);
            t.AddCondition(AnimatorConditionMode.IfNot, 0, flag);
        }

        private static void Configure(AnimatorStateTransition t, float duration, float? exitTime = null)
        {
            t.hasExitTime = exitTime.HasValue;
            t.exitTime = exitTime ?? 0f;
            t.duration = duration;
            t.offset = 0f;
        }

        /// <summary>既存のコントローラは GUID を保ったまま中身だけ空にする（上書き側からの参照が切れないように）。</summary>
        private static AnimatorController ResetController(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) return AnimatorController.CreateAnimatorControllerAtPath(path);

            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub != null && sub != controller) Object.DestroyImmediate(sub, true);
            }
            controller.layers = new AnimatorControllerLayer[0];
            controller.parameters = new AnimatorControllerParameter[0];
            return controller;
        }

        /// <summary>基本のコントローラのクリップを、同じ名前のこのモンスターのクリップに差し替える。</summary>
        private static AnimatorOverrideController BuildOverride(Group group, string model,
            AnimatorController controller, Dictionary<string, AnimationClip> clips)
        {
            string path = $"{OverrideFolder}/{group.Name}_{model}.overrideController";
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (overrides == null)
            {
                overrides = new AnimatorOverrideController(controller);
                AssetDatabase.CreateAsset(overrides, path);
            }
            overrides.runtimeAnimatorController = controller;

            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrides.GetOverrides(pairs);
            overrides.ApplyOverrides(pairs
                .Select(p => new KeyValuePair<AnimationClip, AnimationClip>(p.Key, clips[p.Key.name]))
                .ToList());

            EditorUtility.SetDirty(overrides);
            return overrides;
        }

        // ---- プレハブ ----

        private static string BuildPrefab(Group group, string model, int hp, int exp, AnimatorOverrideController overrides,
            AnimationClip idle, AudioClip stunClip, AudioClip faintClip)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(group.ModelPath(model));
            string prefabName = $"{group.Name}_{model}";
            string path = $"{PrefabFolder}/{prefabName}.prefab";

            // 背丈と足元は、待機の最初のポーズを取らせた使い捨ての複製で測る（プレハブに骨の上書きを残さないため）。
            Bounds pose = MeasurePose(source, idle);
            float scale = group.Height / pose.size.y;

            var root = new GameObject(prefabName);
            try
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                body.name = "Model";
                body.transform.localScale = Vector3.one * scale;
                body.transform.localPosition = new Vector3(-pose.center.x, -pose.min.y, -pose.center.z) * scale;

                var animator = body.GetComponent<Animator>();
                if (animator == null) animator = body.AddComponent<Animator>();
                animator.runtimeAnimatorController = overrides;
                animator.applyRootMotion = false;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.height = group.Height;
                capsule.radius = group.Radius;
                capsule.center = new Vector3(0f, group.Height / 2f, 0f);

                var reward = new SerializedObject(root.AddComponent<EnemyExpReward>());
                reward.FindProperty("exp").intValue = exp;
                reward.ApplyModifiedPropertiesWithoutUndo();

                var health = new SerializedObject(root.AddComponent<EnemyHealth>());
                health.FindProperty("maxHp").intValue = hp;
                health.ApplyModifiedPropertiesWithoutUndo();

                var reaction = new SerializedObject(root.AddComponent<EnemyDamageReaction>());
                reaction.FindProperty("stunClip").objectReferenceValue = stunClip;
                reaction.FindProperty("faintClip").objectReferenceValue = faintClip;
                reaction.ApplyModifiedPropertiesWithoutUndo();

                BuildRagdoll(group, root, animator);
                root.AddComponent<EnemyDeath>();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return path;
        }

        /// <summary>骨に Rigidbody・コライダー・関節を付け、EnemyRagdoll に渡す。プレハブは原点・バインドポーズのまま測る。</summary>
        private static void BuildRagdoll(Group group, GameObject root, Animator animator)
        {
            var bones = animator.GetComponentsInChildren<Transform>()
                .GroupBy(t => t.name)
                .ToDictionary(g => g.Key, g => g.First());
            Transform Bone(string name) =>
                bones.TryGetValue(name, out var t) ? t : throw new System.InvalidOperationException($"{group.Name}: 骨 {name} が無い");

            var bodies = new List<Rigidbody>();
            var byName = new Dictionary<string, Rigidbody>();
            foreach (RagdollBone spec in group.Ragdoll)
            {
                Transform bone = Bone(spec.Bone);
                var body = bone.gameObject.AddComponent<Rigidbody>();
                body.mass = spec.Mass;
                body.isKinematic = true;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                bodies.Add(body);
                byName[spec.Bone] = body;

                // 骨は大きく拡縮されている（FBX の 100 倍 × 背丈合わせ）ので、ワールドで決めてから骨の座標へ直す。
                float scale = bone.lossyScale.x;
                float radius = spec.Radius / scale;
                var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                capsule.radius = radius;
                if (spec.End != null)
                {
                    Vector3 end = bone.InverseTransformPoint(Bone(spec.End).position);
                    int axis = LongestAxis(end);
                    capsule.direction = axis;
                    capsule.center = end / 2f;
                    capsule.height = Mathf.Max(Mathf.Abs(end[axis]) + radius, radius * 2f);
                }
                else
                {
                    capsule.direction = 1;
                    capsule.center = bone.InverseTransformPoint(bone.position + Vector3.up * spec.Radius);
                    capsule.height = radius * 2f;
                }
                capsule.enabled = false;

                if (spec.Parent == null) continue;

                var joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = byName[spec.Parent];
                int twist = spec.End != null ? LongestAxis(bone.InverseTransformPoint(Bone(spec.End).position)) : 1;
                joint.axis = AxisVector(twist);
                joint.swingAxis = AxisVector((twist + 1) % 3);
                joint.lowTwistLimit = new SoftJointLimit { limit = -20f };
                joint.highTwistLimit = new SoftJointLimit { limit = 20f };
                joint.swing1Limit = new SoftJointLimit { limit = 40f };
                joint.swing2Limit = new SoftJointLimit { limit = 30f };
                joint.enableProjection = true;
            }

            var followers = group.Followers.Select(f => Bone(f.follower)).ToArray();
            var targets = group.Followers.Select(f => Bone(f.target)).ToArray();
            root.AddComponent<EnemyRagdoll>().Configure(animator, bodies.ToArray(), followers, targets);
        }

        private static int LongestAxis(Vector3 v)
        {
            Vector3 a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
        }

        private static Vector3 AxisVector(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        /// <summary>原点に置いてクリップの 0 秒のポーズを取らせたときの、見た目の外接箱（モデルのルートから見た座標）。</summary>
        private static Bounds MeasurePose(GameObject source, AnimationClip clip)
        {
            var go = Object.Instantiate(source);
            var mesh = new Mesh();
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                clip.SampleAnimation(go, 0f);

                Bounds? bounds = null;
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    // useScale: false だと拡縮込み（見た目どおりの大きさ）で焼かれるので、残りの位置と回転だけかける。
                    // true にすると拡縮が打ち消され、この FBX（ノードに 100 倍がかかっている）では 1/100 の大きさになる。
                    smr.BakeMesh(mesh, false);
                    var toRoot = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                    foreach (Vector3 v in mesh.vertices)
                    {
                        Vector3 p = toRoot.MultiplyPoint3x4(v);
                        if (bounds == null) bounds = new Bounds(p, Vector3.zero);
                        else
                        {
                            Bounds b = bounds.Value;
                            b.Encapsulate(p);
                            bounds = b;
                        }
                    }
                }
                return bounds ?? new Bounds(Vector3.zero, Vector3.one);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(go);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
