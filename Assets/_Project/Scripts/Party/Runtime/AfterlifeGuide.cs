using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 死後の場所の道しるべ（足場の淡い光の粒）の後半を、先頭が強化の台に近づくまで隠しておく。
    /// 着いたときに見えるのは着地点から強化の台までの道だけなので、初めて来た人も転生の渦より先に強化の台へ寄る。
    /// 転生の渦そのものは光っていて見えるので、慣れた人はそのまま渦へ向かえる。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Afterlife Guide")]
    public sealed class AfterlifeGuide : MonoBehaviour
    {
        [SerializeField, Tooltip("強化の台。先頭がここへ近づいたら後半の道を出す。")]
        private Transform altar;

        [SerializeField, Tooltip("強化の台から転生の渦までの道しるべ。近づくまで隠す。")]
        private GameObject laterPath;

        [SerializeField, Min(0f), Tooltip("強化の台からこの距離（水平、m）まで近づいたら後半の道を出す。")]
        private float revealRadius = 4.5f;

        private void Start()
        {
            if (laterPath != null) laterPath.SetActive(false);
        }

        private void Update()
        {
            if (laterPath == null || altar == null || laterPath.activeSelf) return;

            PartyMember leader = Party.Current != null ? Party.Current.Leader : null;
            if (leader == null) return;

            Vector3 offset = leader.transform.position - altar.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= revealRadius * revealRadius) laterPath.SetActive(true);
        }
    }
}
