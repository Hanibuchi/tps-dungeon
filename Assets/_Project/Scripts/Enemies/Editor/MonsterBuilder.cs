using System.Collections.Generic;
using System.Linq;
using TpsDungeon.Items;
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
    /// プレハブは「ルート（CapsuleCollider・EnemyHealth・EnemyDamageReaction・EnemyRagdoll・EnemyDeath・EnemyExpReward・
    /// EnemyIdentity（id はプレハブ名の snake_case）・EnemyWeaponDrop・NavMeshAgent・EnemyMotor・敵種ごとの EnemyBrain）＋子の Model（FBX と Animator）」。
    /// プレハブ名は敵の名前（Goblin など、<see cref="Monsters"/>）。
    /// 足元がルートの原点、背丈はグループの <see cref="Group.Height"/> に敵ごとの倍率を掛けた高さにそろえる。動きの付け方は <see cref="EnemyBehaviourSetup"/>。
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

        // 敵の声（効果音ラボ）。見つからなければ空のまま。
        private const string Voice = EnemyBehaviourSetup.MonsterSounds;

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
            /// <summary>スタン（被弾のたびにも）・気絶・死亡の声。体のつくりが同じなら声も揃える。</summary>
            public string StunSound;
            public string FaintSound;
            public string DeathSound;
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
            StunSound = Voice + "ゴブリンの鳴き声1.mp3",
            FaintSound = Voice + "ゴブリンの鳴き声3.mp3",
            DeathSound = Voice + "ゾンビの断末魔.mp3",
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
            StunSound = Voice + "翼竜の鳴き声2.mp3",
            FaintSound = Voice + "翼竜の鳴き声1.mp3",
            DeathSound = Voice + "ゾンビの声2.mp3",
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
            StunSound = Voice + "スライムの攻撃.mp3",
            FaintSound = Voice + "スライムの攻撃.mp3",
            DeathSound = Voice + "打撃8.mp3",
            Ragdoll = new[]
            {
                new RagdollBone("Body", null, null, 0.3f, 2f),
                new RagdollBone("Head", "Body", "Head3_end", 0.25f),
            },
        };

        private static readonly Group[] Groups = { Big, Flying, Blob };

        private const string Arms = "Assets/ThirdParty/Sound/SoundEffect-Lab/Arms/";

        /// <summary>
        /// 層ごとの強さの倍率（1 層 = 1）。どれもスプレッドシート「バランス」から取った仮の値。
        /// HP と経験値は「攻撃力」シートの層ごとの標準総 DPS（154・359・660・1056・1502）の比、
        /// ダメージは層のレベル帯の真ん中（Lv5・15・25・35・45）の MaxHP（<see cref="GrowthCurve.Default"/>）の比。
        /// </summary>
        private static readonly float[] HpScale = { 1f, 2.3f, 4.3f, 6.9f, 9.8f };
        private static readonly float[] DamageScale = { 1f, 1.3f, 1.7f, 2f, 2.4f };

        /// <summary>
        /// 1 体ぶんの作り方。Name はプレハブ名で、id はその snake_case（<see cref="EnemyIdentitySetup.IdFromPrefabName"/>。GiantRat → giant_rat）。
        /// Model は Group のフォルダの FBX のファイル名（拡張子なし）。Layer は出る層（Notion「敵一覧」の出現層）。
        /// Hp は最大 HP（<see cref="EnemyHealth"/>）、Exp は倒したときの経験値（<see cref="EnemyExpReward"/>）、
        /// Behaviour のダメージと合わせて 1 層での値で書き、<see cref="HpScale"/>・<see cref="DamageScale"/> で層の強さにする。
        /// Size は背丈と当たり判定の太さの倍率（グループの <see cref="Group.Height"/>・<see cref="Group.Radius"/> に掛ける）。
        /// </summary>
        private sealed class Monster
        {
            public readonly string Name;
            public readonly Group Group;
            public readonly string Model;
            public readonly int Layer;
            public readonly int Hp;
            public readonly int Exp;
            public readonly float Size;
            public readonly EnemyBehaviourSetup.Behaviour Behaviour;

            public Monster(string name, Group group, string model, int layer, int hp, int exp,
                EnemyBehaviourSetup.Behaviour behaviour, float size = 1f)
            {
                Name = name;
                Group = group;
                Model = model;
                Layer = layer;
                Hp = Mathf.RoundToInt(hp * HpScale[layer - 1]);
                Exp = Mathf.RoundToInt(exp * HpScale[layer - 1]);
                Size = size;
                behaviour.Damage = Mathf.Max(1, Mathf.RoundToInt(behaviour.Damage * DamageScale[layer - 1]));
                Behaviour = behaviour;
            }

            public string PrefabPath => $"{PrefabFolder}/{Name}.prefab";
            public float Height => Group.Height * Size;
            public float Radius => Group.Radius * Size;
        }

        /// <summary>
        /// プレハブにするモンスター（Notion「敵一覧」の通常敵 20 体。ボスは別）。名前と見た目は揃っていなくてよい。
        /// behaviour は敵種（Notion「敵種」の 01〜07）と、その動きの数（速さ・ダメージなど）。どれも仮の値。
        /// 敵種によって使えるグループが決まる: 02 飛びつきは Duck・Jump_Land を流すので Big、04 自爆は Dance を流すので Blob、03・06 は Flying。
        /// 音は敵種ごとの既定に、気付いたときの声（noticeClip）などをモンスターごとに With で重ねる。
        /// </summary>
        private static readonly Monster[] Monsters =
        {
            // ---- 1 層 ----
            // 棍棒を振る（剣の素振り 1 は大剣を振る音）。重く当たる。
            new Monster("Goblin", Big, "Orc", 1, 40, 8,
                EnemyBehaviourSetup.Melee(speed: 3f, damage: 10, range: 1.8f, hitRadius: 1f, attackAnimation: "Weapon")
                    .With("noticeClip", Voice + "ゴブリンの鳴き声2.mp3")
                    .With("attackClip", Arms + "剣の素振り1.mp3")
                    .With("hitClip", Voice + "打撃5.mp3")),
            new Monster("GiantRat", Big, "Bunny", 1, 30, 7,
                EnemyBehaviourSetup.Leap(speed: 3f, damage: 10, leapRange: 5f)
                    .With("noticeClip", Voice + "ゴブリンの鳴き声3.mp3"), size: 0.7f),
            new Monster("GiantBat", Flying, "Glub", 1, 25, 6,
                EnemyBehaviourSetup.FloatingMelee(speed: 4f, hover: 1.5f, damage: 7)
                    .With("noticeClip", Voice + "翼竜の鳴き声2.mp3")),
            // 矢を射る。弓を引き絞って放つ音。
            new Monster("GoblinArcher", Big, "Tribal", 1, 25, 8,
                EnemyBehaviourSetup.Ranged(speed: 2.5f, damage: 8, color: EnemyEffectsBuilder.FxColor.Blue)
                    .With("noticeClip", Voice + "ゴブリンの鳴き声2.mp3")
                    .With("windupClip", Arms + "弓を引き絞る1.mp3")
                    .With("attackClip", Arms + "弓矢を放つ.mp3")
                    .With("hitClip", Arms + "弓矢が刺さる.mp3")),

            // ---- 2 層 ----
            new Monster("GoblinWarrior", Big, "Orc_Skull", 2, 60, 12,
                EnemyBehaviourSetup.Melee(speed: 3f, damage: 12, range: 1.8f, hitRadius: 1f, attackAnimation: "Weapon")
                    .With("noticeClip", Voice + "ゴブリンの鳴き声1.mp3")
                    .With("attackClip", Arms + "剣の素振り1.mp3")
                    .With("hitClip", Voice + "打撃5.mp3")),
            new Monster("FangWolf", Big, "Dino", 2, 45, 10,
                EnemyBehaviourSetup.Leap(speed: 3.5f, damage: 15, leapRange: 6f)
                    .With("noticeClip", Voice + "ドラゴンの鳴き声2.mp3")),
            new Monster("WillOWisp", Flying, "Hywirl", 2, 25, 8,
                EnemyBehaviourSetup.FloatingRanged(speed: 3f, hover: 1.7f, damage: 8, color: EnemyEffectsBuilder.FxColor.Blue)
                    .With("noticeClip", Voice + "翼竜の鳴き声2.mp3")),
            new Monster("BombMushroom", Blob, "Mushnub", 2, 15, 5,
                EnemyBehaviourSetup.Bomber(speed: 3.5f, damage: 35, blastRadius: 3.5f)
                    .With("noticeClip", Voice + "スライムの攻撃.mp3")),

            // ---- 3 層 ----
            // 剣で斬る。
            new Monster("SkeletonSwordsman", Big, "Ninja", 3, 60, 12,
                EnemyBehaviourSetup.Melee(speed: 3.2f, damage: 12, range: 1.8f, hitRadius: 1f, attackAnimation: "Weapon")
                    .With("noticeClip", Voice + "ゾンビの声1.mp3")
                    .With("attackClip", Arms + "剣で斬る1.mp3")
                    .With("hitClip", Arms + "刀で斬る5.mp3")),
            new Monster("SkeletonMage", Blob, "Wizard", 3, 25, 8,
                EnemyBehaviourSetup.Ranged(speed: 2.5f, damage: 8, color: EnemyEffectsBuilder.FxColor.Pink)
                    .With("noticeClip", Voice + "ゾンビの声2.mp3")),
            new Monster("CursedLantern", Flying, "Ghost", 3, 30, 6,
                EnemyBehaviourSetup.FloatingMelee(speed: 3.5f, hover: 1.5f, damage: 8)
                    .With("noticeClip", Voice + "ゾンビの声1.mp3")),
            new Monster("PoisonSlime", Blob, "GreenBlob", 3, 35, 8,
                EnemyBehaviourSetup.PoisonGas(speed: 1.8f, damage: 3, cloudRadius: 3f)
                    .With("noticeClip", Voice + "スライムの攻撃.mp3")),

            // ---- 4 層 ----
            new Monster("CaveGhoul", Big, "Monkroose", 4, 45, 10,
                EnemyBehaviourSetup.Leap(speed: 3f, damage: 15, leapRange: 7f)
                    .With("noticeClip", Voice + "ゾンビの声2.mp3")),
            // 目がたくさんある。氷の弾を撃つ。
            new Monster("StaringEye", Flying, "Glub_Evolved", 4, 25, 8,
                EnemyBehaviourSetup.FloatingRanged(speed: 2.5f, hover: 2f, damage: 8, color: EnemyEffectsBuilder.FxColor.Pink)
                    .With("noticeClip", Voice + "翼竜の鳴き声1.mp3")
                    .With("attackClip", Arms + "氷魔法2.mp3")),
            new Monster("MiasmaFrog", Big, "Frog", 4, 35, 8,
                EnemyBehaviourSetup.PoisonGas(speed: 2f, damage: 3, cloudRadius: 3.5f)
                    .With("noticeClip", Voice + "ドラゴンの鳴き声2.mp3")),
            new Monster("GunpowderGoblin", Blob, "Orc", 4, 15, 5,
                EnemyBehaviourSetup.Bomber(speed: 4.5f, damage: 35, blastRadius: 4f)
                    .With("noticeClip", Voice + "ゴブリンの鳴き声3.mp3")),

            // ---- 5 層 ----
            new Monster("Wraith", Flying, "Ghost_Skull", 5, 30, 6,
                EnemyBehaviourSetup.FloatingMelee(speed: 4f, hover: 1.5f, damage: 8)
                    .With("noticeClip", Voice + "ゾンビの断末魔.mp3")),
            new Monster("LavaSlime", Blob, "PinkBlob", 5, 15, 5,
                EnemyBehaviourSetup.Bomber(speed: 4f, damage: 35, blastRadius: 4f)
                    .With("noticeClip", Voice + "スライムの攻撃.mp3")),
            // 砲弾を撃つ。遅いが硬い。
            new Monster("ArtilleryGolem", Big, "Yeti", 5, 40, 8,
                EnemyBehaviourSetup.Ranged(speed: 2f, damage: 8, color: EnemyEffectsBuilder.FxColor.Pink)
                    .With("noticeClip", Voice + "ドラゴンの鳴き声2.mp3")
                    .With("attackClip", Arms + "爆発2.mp3"), size: 1.3f),
            new Monster("RotTreant", Big, "MushroomKing", 5, 35, 8,
                EnemyBehaviourSetup.PoisonGas(speed: 1.8f, damage: 3, cloudRadius: 4f)
                    .With("noticeClip", Voice + "ゾンビの声2.mp3"), size: 1.15f),
        };

        /// <summary>
        /// 仮の 8 体から作り替えた敵（元のプレハブ名 → 今の名前）。名前だけ変えて GUID を保ち、シーンや草むらの表からの参照を切らない。
        /// </summary>
        private static readonly (string from, string to)[] Renames =
        {
            ("Big_Orc", "Goblin"), ("Blob_GreenBlob", "PoisonSlime"), ("Big_Frog", "MiasmaFrog"), ("Flying_Ghost", "CursedLantern"),
            ("Blob_PinkBlob", "LavaSlime"), ("Blob_Mushnub", "BombMushroom"), ("Flying_Hywirl", "WillOWisp"), ("Blob_Wizard", "SkeletonMage"),
        };

        /// <summary>元の名前のプレハブが残っていて今の名前が無ければ、名前を変える。</summary>
        private static void RenameLegacyPrefabs(List<string> log)
        {
            foreach (var (from, to) in Renames)
            {
                string oldPath = $"{PrefabFolder}/{from}.prefab";
                string newPath = $"{PrefabFolder}/{to}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(oldPath) == null || AssetDatabase.LoadAssetAtPath<GameObject>(newPath) != null) continue;

                string error = AssetDatabase.MoveAsset(oldPath, newPath);
                log.Add(string.IsNullOrEmpty(error) ? $"名前を変えた: {from} → {to}" : $"名前を変えられなかった {from} → {to}: {error}");
            }
        }

        /// <summary>
        /// 作ってあるプレハブに、声・攻撃の音・攻撃の見た目だけを入れ直す（見た目のプレハブは作り直す。Animator も動きの数も作り直さない）。
        /// 音や見た目を差し替えたいときはこのファイル（グループの声・<see cref="Monsters"/> の With）・EnemyBehaviourSetup・
        /// EnemyEffectsBuilder を直してからこれを回す。開いているシーンには触らないので、敵の居るシーンを開いたままでもよい。
        /// </summary>
        [MenuItem("Tools/TPS Dungeon/敵/モンスターの音と見た目だけ入れ直す", false, 401)]
        public static void ApplyPresentationFromMenu()
        {
            Debug.Log(ApplyPresentation());
        }

        public static string ApplyPresentation()
        {
            var log = new List<string>();
            RenameLegacyPrefabs(log);
            EnemyBehaviourSetup.WriteEffects(log);
            foreach (Monster monster in Monsters)
            {
                string path = monster.PrefabPath;
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    log.Add($"飛ばした（プレハブが無い）: {path}");
                    continue;
                }

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    WriteVoices(root, monster.Group, log);
                    EnemyBehaviourSetup.WritePresentation(root, monster.Behaviour, log);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Add($"音と見た目を入れ直した: {path}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return "モンスターの音と見た目を入れ直した\n  " + string.Join("\n  ", log);
        }

        /// <summary>グループの声（スタン・気絶・死亡）を入れる。</summary>
        private static void WriteVoices(GameObject root, Group group, List<string> log)
        {
            var reaction = new SerializedObject(root.GetComponent<EnemyDamageReaction>());
            reaction.FindProperty("stunClip").objectReferenceValue = LoadClip(group.StunSound, log);
            reaction.FindProperty("faintClip").objectReferenceValue = LoadClip(group.FaintSound, log);
            reaction.ApplyModifiedPropertiesWithoutUndo();

            var death = new SerializedObject(root.GetComponent<EnemyDeath>());
            death.FindProperty("deathClip").objectReferenceValue = LoadClip(group.DeathSound, log);
            death.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Tools/TPS Dungeon/敵/モンスターの Animator とプレハブを作る", false, 400)]
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
            RenameLegacyPrefabs(log);
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

            EnemyBehaviourSetup.WriteEffects(log);


            foreach (Monster monster in Monsters)
            {
                // 差し替えは名前で引くので、見本と同じ名前のクリップが全部そろっていないと骨組みの違う動きが残ってしまう。
                Group group = monster.Group;
                string path = group.ModelPath(monster.Model);
                if (!TryLoadClips(path, referenceClips[group], out var clips, out string missing))
                {
                    log.Add($"飛ばした {path}: {missing}");
                    continue;
                }
                var overrides = BuildOverride(group, monster.Model, controllers[group], clips);
                log.Add($"プレハブ（{monster.Layer} 層・{monster.Behaviour.Label}・HP {monster.Hp}・ダメージ {monster.Behaviour.Damage}）: " +
                    BuildPrefab(monster, overrides, clips[group.Locomotion[0].clip], log));
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

        private static string BuildPrefab(Monster monster, AnimatorOverrideController overrides, AnimationClip idle, List<string> log)
        {
            Group group = monster.Group;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(group.ModelPath(monster.Model));
            string path = monster.PrefabPath;

            // 背丈と足元は、待機の最初のポーズを取らせた使い捨ての複製で測る（プレハブに骨の上書きを残さないため）。
            Bounds pose = MeasurePose(source, idle);
            float scale = monster.Height / pose.size.y;

            var root = new GameObject(monster.Name);
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
                capsule.height = monster.Height;
                capsule.radius = monster.Radius;
                capsule.center = new Vector3(0f, monster.Height / 2f, 0f);

                var reward = new SerializedObject(root.AddComponent<EnemyExpReward>());
                reward.FindProperty("exp").intValue = monster.Exp;
                reward.ApplyModifiedPropertiesWithoutUndo();

                var health = new SerializedObject(root.AddComponent<EnemyHealth>());
                health.FindProperty("maxHp").intValue = monster.Hp;
                health.ApplyModifiedPropertiesWithoutUndo();

                root.AddComponent<EnemyDamageReaction>();
                BuildRagdoll(group, root, animator, monster.Size);
                EnemyDeathSetup.Assign(root.AddComponent<EnemyDeath>(), EnemyDeathSetup.EnsureFadeTemplate());
                EnemyIdentitySetup.Assign(root.AddComponent<EnemyIdentity>(), EnemyIdentitySetup.IdFromPrefabName(monster.Name));
                root.AddComponent<EnemyWeaponDrop>();
                WriteVoices(root, group, log);
                EnemyBehaviourSetup.Assign(root, monster.Behaviour, monster.Radius, monster.Height, log);

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            return path;
        }

        /// <summary>骨に Rigidbody・コライダー・関節を付け、EnemyRagdoll に渡す。プレハブは原点・バインドポーズのまま測る。size は敵の大きさの倍率。</summary>
        private static void BuildRagdoll(Group group, GameObject root, Animator animator, float size)
        {
            var bones = animator.GetComponentsInChildren<Transform>()
                .GroupBy(t => t.name)
                .ToDictionary(g => g.Key, g => g.First());
            Transform Bone(string name) =>
                bones.TryGetValue(name, out var t) ? t : throw new System.InvalidOperationException($"{group.Name}: 骨 {name} が無い");

            // 同じグループでも、モデルによって骨が無いことがある（Bunny の Head_end、腕の無い Glub など）。
            // 先の骨が無ければ自分の骨の先（名前_end）までにし、それも無ければ球にする。腰より先の骨そのものが無ければ、その骨から先を作らない。
            Transform End(RagdollBone spec)
            {
                if (spec.End == null) return null;
                if (bones.TryGetValue(spec.End, out var end)) return end;
                return bones.TryGetValue(spec.Bone + "_end", out end) ? end : null;
            }

            var bodies = new List<Rigidbody>();
            var byName = new Dictionary<string, Rigidbody>();
            foreach (RagdollBone spec in group.Ragdoll)
            {
                if (spec.Parent != null && (!bones.ContainsKey(spec.Bone) || !byName.ContainsKey(spec.Parent))) continue;
                Rigidbody parent = spec.Parent != null ? byName[spec.Parent] : null;
                Rigidbody body = RagdollBaker.AddBone(Bone(spec.Bone), End(spec), parent, spec.Radius * size, spec.Mass);
                bodies.Add(body);
                byName[spec.Bone] = body;
            }

            var followers = group.Followers.Select(f => Bone(f.follower)).ToArray();
            var targets = group.Followers.Select(f => Bone(f.target)).ToArray();
            root.AddComponent<EnemyRagdoll>().Configure(animator, bodies.ToArray(), followers, targets);
        }

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
