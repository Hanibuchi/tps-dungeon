using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TpsDungeon.Interaction
{
    /// <summary>
    /// 画面中央の照準の先にある IInteractable を探し、インタラクトキーで触る。
    /// カーソルはロックされているので「カーソルを合わせる」は画面中央からのレイで判定する。
    /// 壁越しに拾わないよう、自分以外で最初に当たったものしか見ない。
    /// トリガーは IInteractable に属するもの（開いた扉の開口に張った判定など）だけを的にし、それ以外は素通しする。
    /// プレイヤーのルート（PlayerInput と同じ GameObject）に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("TPS Dungeon/Player Interactor")]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        private const int HitBufferSize = 16;

        [SerializeField, Tooltip("入力を受け取る PlayerInput。未設定ならこの GameObject から探す。")]
        private PlayerInput playerInput;

        [SerializeField, Tooltip("PlayerInput のアクションアセット内のインタラクト用アクション名。")]
        private string interactActionName = "Interact";

        [SerializeField, Tooltip("照準のレイを飛ばすカメラ。未設定なら Camera.main。")]
        private Camera aimCamera;

        [SerializeField, Tooltip("カメラから照準のレイを飛ばす最大距離（メートル）。三人称なのでカメラとキャラの距離ぶん余裕を持たせる。")]
        private float maxAimDistance = 10f;

        [SerializeField, Tooltip("キャラの胸から照準の当たり点までがこの距離（メートル）以内なら触れる。")]
        private float interactRange = 2.5f;

        [SerializeField, Tooltip("キャラの足元から胸までの高さ。届くかどうかの判定の起点。")]
        private float reachOriginHeight = 1.2f;

        [SerializeField, Tooltip("照準のレイが当たるレイヤー。")]
        private LayerMask aimMask = ~0;

        private readonly RaycastHit[] hits = new RaycastHit[HitBufferSize];
        private InputAction interactAction;

        // Door などは作り直しで破棄されるので、Unity の null 判定ができるよう Component でも持つ。
        private Component currentComponent;

        /// <summary>今照準が合っていて触れる相手。いなければ null。</summary>
        public IInteractable CurrentTarget { get; private set; }

        /// <summary>インタラクトキーの表示名（キーボードなら "E"）。操作デバイスが変わると追従する。</summary>
        public string InteractBindingDisplay { get; private set; } = string.Empty;

        private void Reset()
        {
            playerInput = GetComponent<PlayerInput>();
        }

        private void Awake()
        {
            if (playerInput == null) playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            if (playerInput == null || playerInput.actions == null)
            {
                Debug.LogWarning("PlayerInput が無いのでインタラクトできない", this);
                return;
            }

            interactAction = playerInput.actions.FindAction(interactActionName);
            if (interactAction == null)
            {
                Debug.LogWarning($"アクション '{interactActionName}' が {playerInput.actions.name} に無い", this);
            }

            playerInput.onControlsChanged += OnControlsChanged;
            InputSystem.onActionChange += OnActionChange;
            RefreshBindingDisplay();
        }

        private void OnDisable()
        {
            if (playerInput != null) playerInput.onControlsChanged -= OnControlsChanged;
            InputSystem.onActionChange -= OnActionChange;
            interactAction = null;
            SetTarget(null);
        }

        private void Update()
        {
            SetTarget(FindTarget());

            if (CurrentTarget != null && interactAction != null && interactAction.WasPressedThisFrame())
            {
                CurrentTarget.Interact(gameObject);
            }
        }

        /// <summary>照準の先で最初に当たったものが、届く範囲の IInteractable ならそれを返す。</summary>
        private Component FindTarget()
        {
            var cam = aimCamera != null ? aimCamera : Camera.main;
            if (cam == null) return null;

            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int count = Physics.RaycastNonAlloc(ray, hits, maxAimDistance, aimMask, QueryTriggerInteraction.Collide);
            Array.Sort(hits, 0, count, HitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                var hitTransform = hits[i].collider.transform;
                // 肩越しのカメラなのでレイが自分の体を掠めることがある。自分は無視して先を見る。
                if (hitTransform.IsChildOf(transform)) continue;

                var interactable = hitTransform.GetComponentInParent<IInteractable>();
                if (interactable == null)
                {
                    // 関係ないトリガー（範囲判定など）で照準を遮らない。実体に当たったらそこで打ち切る。
                    if (hits[i].collider.isTrigger) continue;
                    return null;
                }

                var reachOrigin = transform.position + Vector3.up * reachOriginHeight;
                if (!IsWithinReach(reachOrigin, hits[i].point, interactRange)) return null;
                if (!interactable.CanInteract(gameObject)) return null;

                return interactable as Component;
            }

            return null;
        }

        private void SetTarget(Component component)
        {
            // 破棄済みのものは Unity の == で null になるので、本物の null に揃えてから参照で比べる。
            // こうすると、狙っていたドアが作り直しで消えたときも「null に変わった」と通知できる。
            if (component == null) component = null;
            if (ReferenceEquals(currentComponent, component)) return;

            currentComponent = component;
            CurrentTarget = component as IInteractable;
        }

        private void OnControlsChanged(PlayerInput input)
        {
            RefreshBindingDisplay();
        }

        // キー設定でインタラクトのキーが変わったら、案内に出すキーも変える。
        private void OnActionChange(object _, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged && interactAction != null) RefreshBindingDisplay();
        }

        private void RefreshBindingDisplay()
        {
            InteractBindingDisplay = interactAction != null
                ? BindingDisplay(interactAction, playerInput.currentControlScheme)
                : string.Empty;
        }

        /// <summary>
        /// アクションに割り当たっているキーの表示。複数あれば " / " でつなぐ。
        /// キー設定の空き枠（パスが空のバインド）は飛ばす。GetBindingDisplayString に任せると "E | " のように空き枠まで数えてしまう。
        /// </summary>
        public static string BindingDisplay(InputAction action, string controlScheme)
        {
            // スキームが決まる前（デバイス未ペア）はスキームで絞ると空になるので、全バインドから拾う。
            if (!string.IsNullOrEmpty(controlScheme))
            {
                string display = JoinBindingDisplay(action, controlScheme);
                if (!string.IsNullOrEmpty(display)) return display;
            }

            return JoinBindingDisplay(action, null);
        }

        private static string JoinBindingDisplay(InputAction action, string controlScheme)
        {
            var result = new System.Text.StringBuilder();
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (binding.isComposite || binding.isPartOfComposite) continue;
                if (string.IsNullOrEmpty(binding.effectivePath)) continue;
                if (controlScheme != null && !InGroup(binding, controlScheme)) continue;

                string text = action.GetBindingDisplayString(i);
                if (string.IsNullOrEmpty(text)) continue;
                if (result.Length > 0) result.Append(" / ");
                result.Append(text);
            }

            return result.ToString();
        }

        private static bool InGroup(InputBinding binding, string group)
        {
            if (string.IsNullOrEmpty(binding.groups)) return false;
            foreach (string g in binding.groups.Split(InputBinding.Separator))
            {
                if (string.Equals(g, group, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        /// <summary>origin から point までが range 以内か。</summary>
        public static bool IsWithinReach(Vector3 origin, Vector3 point, float range)
        {
            return (point - origin).sqrMagnitude <= range * range;
        }

        private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();

            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
