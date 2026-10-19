using System.Collections;
using System.Collections.Generic;
using TpsDungeon.Audio.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 攻撃で壊せる置物（地上の草花、ダンジョンの木箱・宝箱）。同じ GameObject の <see cref="EnemyHealth"/>（置物の印付き、HP 1）が尽きたら壊れる。
    /// - 壊れ方：草花は当たった向きの反対へ倒れながら潰れて消える（<see cref="BreakStyle.Squash"/>）。箱はその場で砕けて消える（<see cref="BreakStyle.Shatter"/>）。
    ///   どちらもかけら（置物の色）が飛び散り、音が鳴る。
    /// - 出てくるもの：
    ///   - 草花：<see cref="PlantSpawnTable"/> を 1 回振り、当たった敵を出す。
    ///   - 箱：<see cref="BreakableDropTable"/> の自分の種類の行で、敵・武器・何も出ないを選ぶ。敵は差し込まれたその層の敵から重みで選ぶ（<see cref="SetEnemyPool"/>）。
    ///     武器は差し込まれたその層のランクの重みで選び（<see cref="SetWeaponRanks"/>）、<see cref="WeaponRolled"/> で知らせる（出すのは Items の BreakableWeaponDrop）。
    ///   敵は少し間を置いて、土煙と音と一緒に跳ね出るように出す（小さく出て膨らむ）。
    /// 当たりは物理では誰とも当たらないレイヤー（<see cref="BreakableLayer"/>）の箱なので、草花は歩いてすり抜けられる。
    /// 箱は体で当たる子（Default レイヤーの当たり）と NavMeshObstacle も持ち、壊れたら一緒に切る（通れるようになる）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    [AddComponentMenu("TPS Dungeon/Breakable Prop")]
    public sealed class BreakableProp : MonoBehaviour
    {
        public enum BreakStyle
        {
            /// <summary>当たった向きへ倒れながら潰れて消える（草花）。</summary>
            Squash = 0,
            /// <summary>その場で砕けて、すぐ消える（箱）。</summary>
            Shatter = 1,
        }

        [Header("壊れ方")]
        [SerializeField, Tooltip("飛び散るかけら（ParticleSystem）。色は下の 2 色のあいだで粒ごとに決める。")]
        private GameObject chipEffect;

        [SerializeField, Tooltip("かけらの色（花びらなど、いちばん目立つ色）。")]
        private Color primaryColor = new Color(0.38f, 0.5f, 0.19f);

        [SerializeField, Tooltip("かけらの色（葉や茎）。")]
        private Color secondaryColor = new Color(0.44f, 0.5f, 0.19f);

        [SerializeField, Tooltip("壊れたときの音。")]
        private AudioClip breakSound;

        [SerializeField, Range(0f, 1f)]
        private float breakSoundVolume = 0.7f;

        [SerializeField, Tooltip("壊れ方。草花は潰れる、箱は砕ける。")]
        private BreakStyle breakStyle = BreakStyle.Squash;

        [SerializeField, Min(0.01f), Tooltip("倒れて潰れきるまでの秒数（潰れるとき）。")]
        private float squashTime = 0.16f;

        [SerializeField, Min(0.1f), Tooltip("かけらの大きさ・散らばりの倍率。")]
        private float chipScale = 1f;

        [Header("出てくるもの")]
        [SerializeField, Tooltip("草花：壊したときに出てくる敵の表。差してあれば下の箱の表より先に使う。")]
        private PlantSpawnTable spawnTable;

        [SerializeField, Tooltip("箱：敵・武器・何も出ないの表（全部の層で同じ）。")]
        private BreakableDropTable dropTable;

        [SerializeField, Tooltip("箱：表のどの行を使うか。")]
        private BreakableDropKind dropKind = BreakableDropKind.Crate;

        [SerializeField, Min(0f), Tooltip("壊れてから敵が出てくるまでの秒数。")]
        private float spawnDelay = 0.45f;

        [SerializeField, Tooltip("敵が出てくるときの土煙。")]
        private GameObject spawnEffect;

        [SerializeField, Min(0f)]
        private float spawnEffectScale = 1f;

        [SerializeField, Tooltip("敵が出てくるときの音。")]
        private AudioClip spawnSound;

        [SerializeField, Range(0f, 1f)]
        private float spawnSoundVolume = 0.8f;

        [SerializeField, Min(0.01f), Tooltip("敵が小さく出てから元の大きさに膨らむまでの秒数。")]
        private float popTime = 0.32f;

        private EnemyHealth health;
        private Vector3 lastDirection = Vector3.forward;
        private bool broken;
        private readonly List<GameObject> enemyPool = new List<GameObject>();
        private readonly List<float> enemyWeights = new List<float>();
        private readonly List<float> weaponRanks = new List<float>();

        /// <summary>壊れた。出てくる敵が決まっていればその敵のプレハブ（まだ出ていない）、無ければ null。</summary>
        public event System.Action<BreakableProp, GameObject> Broken;

        /// <summary>敵が出てきた（出した実体）。</summary>
        public event System.Action<BreakableProp, GameObject> Spawned;

        /// <summary>武器が出ることになった（ランク。WeaponRank の 0〜5）。</summary>
        public event System.Action<BreakableProp, int> WeaponRolled;

        public PlantSpawnTable SpawnTable => spawnTable;
        public BreakableDropTable DropTable => dropTable;
        public BreakableDropKind DropKind => dropKind;
        public bool IsBroken => broken;

        /// <summary>出てきた敵を置く親。null ならシーンの直下。</summary>
        public Transform SpawnParent { get; set; }

        /// <summary>箱から武器が出るときのランク（E〜S、WeaponRank の 0〜5）の重み。その層の表。入っていなければ武器は出ない。</summary>
        public void SetWeaponRanks(IReadOnlyList<float> weights)
        {
            weaponRanks.Clear();
            if (weights != null) weaponRanks.AddRange(weights);
        }

        /// <summary>箱から出てくる敵の候補（その層の敵）と重み。prefabs と weights は同じ並び。</summary>
        public void SetEnemyPool(IReadOnlyList<GameObject> prefabs, IReadOnlyList<float> weights)
        {
            enemyPool.Clear();
            enemyWeights.Clear();
            for (int i = 0; i < prefabs.Count; i++)
            {
                if (prefabs[i] == null) continue;
                enemyPool.Add(prefabs[i]);
                enemyWeights.Add(i < weights.Count ? weights[i] : 1f);
            }
        }

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        private void OnDamaged(EnemyHealth _, DamageInfo damage, int dealt)
        {
            Vector3 flat = new Vector3(damage.Direction.x, 0f, damage.Direction.z);
            if (flat.sqrMagnitude > 1e-4f) lastDirection = flat.normalized;
        }

        private void OnDied(EnemyHealth _)
        {
            if (broken) return;
            broken = true;
            StartCoroutine(Break());
        }

        private IEnumerator Break()
        {
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
            // 箱が切っていた NavMesh の道を開ける。
            foreach (NavMeshObstacle obstacle in GetComponentsInChildren<NavMeshObstacle>()) obstacle.enabled = false;

            Vector3 center = VisualCenter();
            GameAudio.Instance?.PlaySeAt(breakSound, center, breakSoundVolume);
            SpawnChips(center);

            GameObject enemy = Roll(out int weaponRank);
            Broken?.Invoke(this, enemy);
            if (weaponRank >= 0) WeaponRolled?.Invoke(this, weaponRank);

            float shown = 0f;
            if (breakStyle == BreakStyle.Squash)
            {
                yield return Squash();
                shown = squashTime;
            }
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;

            if (enemy != null)
            {
                float wait = Mathf.Max(0f, spawnDelay - shown);
                if (wait > 0f) yield return new WaitForSeconds(wait);
                yield return SpawnEnemy(enemy);
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 出てくるものを選ぶ。草花の表があればそれで敵を、無ければ箱の表で敵・武器・何も出ないを選ぶ。
        /// 出てくる敵のプレハブ（無ければ null）を返し、武器ならそのランクを weaponRank に入れる（武器でなければ -1）。
        /// </summary>
        private GameObject Roll(out int weaponRank)
        {
            weaponRank = -1;
            if (spawnTable != null) return spawnTable.Roll(Random.value);
            if (dropTable == null || !dropTable.TryGet(dropKind, out BreakableDropTable.Entry entry)) return null;

            switch (BreakableDropRoller.RollOutcome(entry.enemy, entry.weapon, entry.nothing, Random.value))
            {
                case BreakableOutcome.Enemy:
                    int index = BreakableDropRoller.PickWeighted(enemyWeights, Random.value);
                    return index >= 0 ? enemyPool[index] : null;
                case BreakableOutcome.Weapon:
                    weaponRank = BreakableDropRoller.PickWeighted(weaponRanks, Random.value);
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>当たった向きへ倒れながら、横に広がって潰れる。</summary>
        private IEnumerator Squash()
        {
            Transform t = transform;
            Vector3 startScale = t.localScale;
            Quaternion startRotation = t.rotation;
            Vector3 axis = Vector3.Cross(Vector3.up, lastDirection);
            if (axis.sqrMagnitude < 1e-4f) axis = Vector3.right;

            for (float time = 0f; time < squashTime; time += Time.deltaTime)
            {
                float k = time / squashTime;
                float ease = 1f - (1f - k) * (1f - k);
                t.localScale = Vector3.Scale(startScale, new Vector3(1f + 0.3f * ease, Mathf.Lerp(1f, 0.05f, ease), 1f + 0.3f * ease));
                t.rotation = Quaternion.AngleAxis(35f * ease, axis) * startRotation;
                yield return null;
            }
        }

        private void SpawnChips(Vector3 center)
        {
            if (chipEffect == null) return;

            // 噴き上がる向きを、当たった向きへ少し傾ける。
            Quaternion rotation = Quaternion.LookRotation(Vector3.up + lastDirection * 0.6f, -lastDirection);
            GameObject chips = Instantiate(chipEffect, center, rotation);
            chips.transform.localScale *= chipScale;
            var colors = new ParticleSystem.MinMaxGradient(primaryColor, secondaryColor);
            foreach (ParticleSystem system in chips.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule main = system.main;
                main.startColor = colors;
            }
            Destroy(chips, 4f);
        }

        /// <summary>土煙と音を出し、敵を小さく出して膨らませる。向きは壊した相手のほう。</summary>
        private IEnumerator SpawnEnemy(GameObject prefab)
        {
            Vector3 at = transform.position;
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) at = hit.position;
            Quaternion facing = Quaternion.LookRotation(-lastDirection, Vector3.up);

            if (spawnEffect != null)
            {
                GameObject dust = Instantiate(spawnEffect, at, Quaternion.identity);
                dust.transform.localScale *= spawnEffectScale;
                Destroy(dust, 4f);
            }
            GameAudio.Instance?.PlaySeAt(spawnSound, at, spawnSoundVolume);
            SpawnChips(at + Vector3.up * 0.3f);

            GameObject enemy = Instantiate(prefab, at, facing, SpawnParent);
            Spawned?.Invoke(this, enemy);

            Transform t = enemy.transform;
            Vector3 full = t.localScale;
            for (float time = 0f; time < popTime; time += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = time / popTime;
                // 小さく出て、少し大きくはみ出してから戻る。
                float s = k < 0.7f ? Mathf.Lerp(0.2f, 1.12f, k / 0.7f) : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
                t.localScale = full * s;
                yield return null;
            }
            if (t != null) t.localScale = full;
        }

        private Vector3 VisualCenter()
        {
            bool any = false;
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any ? bounds.center : transform.position + Vector3.up * 0.4f;
        }
    }
}
