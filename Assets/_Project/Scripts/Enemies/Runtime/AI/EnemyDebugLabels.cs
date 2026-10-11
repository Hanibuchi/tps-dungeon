using UnityEngine;

namespace TpsDungeon.Enemies
{
    /// <summary>
    /// Game 画面の敵の頭の上に、名前・今していること・HP・狙っている相手を出す（確認用）。
    /// 敵の挙動の試験の窓（EnemyBehaviourTestWindow）が Play 中に付け外しする。シーンに置く物ではない。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class EnemyDebugLabels : MonoBehaviour
    {
        private GUIStyle style;

        private void OnGUI()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
                style.normal.textColor = Color.white;
            }
            // 画面の高さに合わせる（Retina の画面で小さくなりすぎないように）。
            style.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height / 50f));

            foreach (EnemyBrain brain in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            {
                var health = brain.GetComponent<EnemyHealth>();
                if (health == null || health.IsDead) continue;

                var body = brain.GetComponent<Collider>();
                Vector3 top = body != null && body.enabled ? new Vector3(brain.transform.position.x, body.bounds.max.y + 0.3f, brain.transform.position.z)
                    : brain.transform.position + Vector3.up * 2f;
                Vector3 screen = camera.WorldToScreenPoint(top);
                if (screen.z <= 0f) continue;

                string target = brain.HasTarget ? $"→ {brain.Target.Name}{(brain.TargetVisible ? "" : "（見失い）")}" : "";
                string text = $"{brain.name}  {health.CurrentHp}/{health.MaxHp}\n{(brain.enabled ? brain.StateLabel : "止めている")} {target}";
                Vector2 size = style.CalcSize(new GUIContent(text));
                GUI.Box(new Rect(screen.x - size.x / 2f, Screen.height - screen.y - size.y, size.x, size.y), text, style);
            }
        }
    }
}
