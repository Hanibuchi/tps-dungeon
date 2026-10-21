using System.Collections.Generic;
using TpsDungeon.Party;
using TpsDungeon.Player;
using UnityEngine;

namespace TpsDungeon.Menu.UI
{
    /// <summary>
    /// 仲間の顔を、そのキャラ自身を小さなカメラで斜め前から撮って RenderTexture に出す（インベントリの説明欄の絵）。
    /// 体は全員同じなので、角・耳（<see cref="CharacterAppearance"/>）まで入るよう頭のまわりを撮る。
    /// インベントリを開いている間はゲームが止まっているので、相手が替わったときだけ 1 回撮る。
    /// 体のメッシュは Default レイヤーなので、撮る間だけそのキャラの体と頭の小物を味方のレイヤーに移し、カメラは味方のレイヤーだけを写す。
    /// 手に持った武器などは味方のレイヤーから外しておく（弓などが顔の前に被るため）。
    /// 遠くも切る（後ろに居るほかの仲間の小物を写さない）。背景は透明にして窪みの色を見せる。
    /// 主カメラに映っていなかったキャラは体の形（スキン）が前に映ったときのままなので、撮ったあと次のフレームでもう 1 回撮り直す（<see cref="RetakePending"/>）。
    /// ダンジョンは暗いので、撮る間だけ顔の前に光を点ける（同じフレームの本描画には効かない）。
    /// </summary>
    internal sealed class MemberPortrait
    {
        private const int Size = 168;
        private const float FieldOfView = 30f;
        private const float Distance = 0.95f;
        private const float LookAbove = 0.13f;
        private const float LookAhead = 0.09f;
        private const float CameraAbove = 0.05f;
        // 真正面だと平たく見えるので、顔の正面からこの角度だけ横へ回り込んで撮る（キャラから見て右）。
        private const float SideAngle = 30f;
        private const float FarClip = 1.5f;

        private GameObject rig;
        private Camera camera;
        private Light light;
        private RenderTexture texture;
        private PartyMember captured;
        private PartyMember retaken;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private readonly List<int> layers = new List<int>();

        /// <summary>今撮った絵の体の形が古かったか。そのときは次のフレームでもう一度 <see cref="Capture"/> を呼ぶと撮り直す。</summary>
        public bool RetakePending { get; private set; }

        /// <summary>member の顔を撮った絵。見た目（頭の骨）か味方のレイヤーが無ければ null。同じ相手が続く間は撮り直さない。</summary>
        public RenderTexture Capture(PartyMember member)
        {
            if (member == null || AllyLayer.Index < 0) return null;
            RetakePending = false;
            if (member == captured && texture != null) return texture;

            var appearance = member.GetComponent<CharacterAppearance>();
            if (appearance == null || !appearance.TryGetFace(out Vector3 head, out Vector3 forward, out Vector3 up)) return null;

            EnsureRig();
            camera.targetTexture = texture;

            // 頭が体と別の向きを向いていても、顔の正面から少し斜めに回った所から撮る。
            float scale = Mathf.Max(1e-4f, member.transform.lossyScale.y);
            // 頭の骨は首寄りにあるので、見る点は顔の中心（少し上・少し前）に寄せる。
            Vector3 look = head + up * (LookAbove * scale) + forward * (LookAhead * scale);
            Vector3 side = Quaternion.AngleAxis(SideAngle, up) * forward;
            Vector3 eye = look + side * (Distance * scale) + up * (CameraAbove * scale);

            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, up));
            camera.farClipPlane = FarClip * scale;
            light.transform.position = eye + up * (0.2f * scale);

            var animator = member.GetComponent<Animator>();
            Transform headBone = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            int ally = AllyLayer.Index;
            member.GetComponentsInChildren(renderers);
            layers.Clear();
            bool stale = false;
            foreach (Renderer r in renderers)
            {
                bool shown = r is SkinnedMeshRenderer || (headBone != null && r.transform.IsChildOf(headBone));
                if (r is SkinnedMeshRenderer && !r.isVisible) stale = true;
                layers.Add(r.gameObject.layer);
                r.gameObject.layer = shown ? ally : 0;
            }

            light.enabled = true;
            camera.Render();
            light.enabled = false;

            for (int i = 0; i < renderers.Count; i++)
            {
                if (renderers[i] != null) renderers[i].gameObject.layer = layers[i];
            }
            renderers.Clear();

            // 撮り直しは 1 回だけ（撮り直しでも映っていない扱いのままなら、それで諦める）。
            RetakePending = stale && retaken != member;
            retaken = RetakePending ? member : null;
            captured = RetakePending ? null : member;
            return texture;
        }

        /// <summary>撮った相手を忘れる（次は必ず撮り直す）。閉じている間に姿勢や見た目が変わるので、開くたびに呼ぶ。</summary>
        public void Invalidate()
        {
            captured = null;
            retaken = null;
        }

        public void Dispose()
        {
            captured = null;
            if (rig != null) Object.Destroy(rig);
            if (texture != null)
            {
                texture.Release();
                Object.Destroy(texture);
            }
            rig = null;
            camera = null;
            light = null;
            texture = null;
        }

        private void EnsureRig()
        {
            if (texture == null)
            {
                texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
                {
                    name = "MemberPortrait",
                    antiAliasing = 4,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                texture.Create();
            }

            if (rig != null) return;

            rig = new GameObject("MemberPortrait") { hideFlags = HideFlags.HideAndDontSave };

            camera = rig.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = AllyLayer.Mask;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            var lightObject = new GameObject("MemberPortraitLight") { hideFlags = HideFlags.HideAndDontSave };
            lightObject.transform.SetParent(rig.transform, false);
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
            light.enabled = false;
        }
    }
}
