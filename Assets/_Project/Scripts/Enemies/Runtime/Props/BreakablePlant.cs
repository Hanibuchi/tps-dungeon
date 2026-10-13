using System.Collections;
using TpsDungeon.Audio.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// 攻撃で壊せる草花。同じ GameObject の <see cref="EnemyHealth"/>（置物の印付き、HP 1）が尽きたら壊れる。
    /// - 壊れ方：当たった向きの反対へ倒れながら潰れて消え、葉や花びらのかけら（植物の色）が飛び散り、葉の擦れる音が鳴る。
    /// - 出てくる敵：<see cref="PlantSpawnTable"/> を 1 回振り、当たったら少し間を置いて、土煙と音と一緒に跳ね出るように出す（小さく出て膨らむ）。
    /// 当たりは物理では誰とも当たらないレイヤー（<see cref="BreakableLayer"/>）の箱なので、歩いてすり抜けられる。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyHealth))]
    [AddComponentMenu("TPS Dungeon/Breakable Plant")]
    public sealed class BreakablePlant : MonoBehaviour
    {
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

        [SerializeField, Min(0.01f), Tooltip("倒れて潰れきるまでの秒数。")]
        private float squashTime = 0.16f;

        [Header("出てくる敵")]
        [SerializeField, Tooltip("壊したときに出てくる敵の表。空なら何も出ない。")]
        private PlantSpawnTable spawnTable;

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

        /// <summary>壊れた。出てくる敵が決まっていればその敵のプレハブ（まだ出ていない）、無ければ null。</summary>
        public event System.Action<BreakablePlant, GameObject> Broken;

        /// <summary>敵が出てきた（出した実体）。</summary>
        public event System.Action<BreakablePlant, GameObject> Spawned;

        public PlantSpawnTable SpawnTable => spawnTable;

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

            Vector3 center = VisualCenter();
            GameAudio.Instance?.PlaySeAt(breakSound, center, breakSoundVolume);
            SpawnChips(center);

            GameObject enemy = spawnTable != null ? spawnTable.Roll(Random.value) : null;
            Broken?.Invoke(this, enemy);

            yield return Squash();
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = false;

            if (enemy != null)
            {
                float wait = Mathf.Max(0f, spawnDelay - squashTime);
                if (wait > 0f) yield return new WaitForSeconds(wait);
                yield return SpawnEnemy(enemy);
            }

            Destroy(gameObject);
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

            GameObject enemy = Instantiate(prefab, at, facing);
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
