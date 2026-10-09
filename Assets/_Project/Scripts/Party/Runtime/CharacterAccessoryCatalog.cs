using System;
using UnityEngine;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 見た目の個性に使う小物（角・耳・しっぽ）のプレハブと、付ける位置。<see cref="CharacterAppearance"/> が読む。
    /// 位置と向きは、キャラのルートから見た値（顔が +Z、上が +Y）で、骨（頭・腰）の初めの姿勢を基準に骨の下へ置く。
    /// 左の値を書き、右は左右反転（X を逆、Y・Z 回りの回転を逆）して使う。
    /// 合わせるときは、シーンのキャラを選んでインスペクタの「小物の置き方を合わせる」からシーンビューで動かせる。
    /// </summary>
    [CreateAssetMenu(menuName = "TPS Dungeon/Party/Character Accessory Catalog", fileName = "CharacterAccessoryCatalog")]
    public sealed class CharacterAccessoryCatalog : ScriptableObject
    {
        [Serializable]
        public struct Placement
        {
            [Tooltip("付ける骨。")]
            public HumanBodyBones bone;

            [Tooltip("キャラのルートから見た位置（m）。左右のある物は左の位置。")]
            public Vector3 position;

            [Tooltip("キャラのルートから見た向き（度）。左右のある物は左の向き。")]
            public Vector3 euler;

            [Tooltip("大きさ。")]
            public float scale;

            public Placement(HumanBodyBones bone, Vector3 position, Vector3 euler, float scale)
            {
                this.bone = bone;
                this.position = position;
                this.euler = euler;
                this.scale = scale;
            }

            /// <summary>左右反転した置き方。</summary>
            public Placement Mirrored => new Placement(bone, new Vector3(-position.x, position.y, position.z), new Vector3(euler.x, -euler.y, -euler.z), scale);
        }

        [Header("角（色の番号順）")]
        [SerializeField] private GameObject[] hornsLeft = new GameObject[CharacterLook.HornColors];
        [SerializeField] private GameObject[] hornsRight = new GameObject[CharacterLook.HornColors];
        [SerializeField] private Placement hornPlacement = new Placement(HumanBodyBones.Head, new Vector3(0f, 1.72f, -0.02f), new Vector3(-15f, 0f, 0f), 0.8f);

        [Header("耳（色の番号順）")]
        [SerializeField] private GameObject[] earsLeft = new GameObject[CharacterLook.EarColors];
        [SerializeField] private GameObject[] earsRight = new GameObject[CharacterLook.EarColors];
        [SerializeField] private Placement earPlacement = new Placement(HumanBodyBones.Head, new Vector3(-0.085f, 1.74f, -0.03f), new Vector3(0f, 0f, 25f), 0.75f);

        [Header("色見本（組み込みのときに小物のパレットから読む）")]
        [SerializeField] private Color[] hornColors = new Color[CharacterLook.HornColors];
        [SerializeField] private Color[] earColors = new Color[CharacterLook.EarColors];
        [SerializeField] private Color[] tailColors = new Color[CharacterLook.TailColors];

        [Header("しっぽ（色の番号順）")]
        [SerializeField] private GameObject[] tails = new GameObject[CharacterLook.TailColors];
        [SerializeField] private Placement tailPlacement = new Placement(HumanBodyBones.Hips, new Vector3(0f, 0.95f, -0.12f), new Vector3(-15f, 0f, 0f), 0.65f);

        public Placement HornPlacement => hornPlacement;
        public Placement EarPlacement => earPlacement;
        public Placement TailPlacement => tailPlacement;

        /// <summary>color 番（1 始まり）の左の角。無ければ null。</summary>
        public GameObject HornLeft(int color) => Pick(hornsLeft, color);
        public GameObject HornRight(int color) => Pick(hornsRight, color);
        public GameObject EarLeft(int color) => Pick(earsLeft, color);
        public GameObject EarRight(int color) => Pick(earsRight, color);
        public GameObject Tail(int color) => Pick(tails, color);

        /// <summary>color 番（1 始まり）の角・耳・しっぽの主な色（仲間の一覧の色見本に使う）。無ければ null。</summary>
        public Color? HornColor(int color) => PickColor(hornColors, color);
        public Color? EarColor(int color) => PickColor(earColors, color);
        public Color? TailColor(int color) => PickColor(tailColors, color);

        private static Color? PickColor(Color[] list, int color)
        {
            int index = color - 1;
            return list != null && index >= 0 && index < list.Length ? list[index] : (Color?)null;
        }

        private static GameObject Pick(GameObject[] list, int color)
        {
            int index = color - 1;
            return list != null && index >= 0 && index < list.Length ? list[index] : null;
        }

#if UNITY_EDITOR
        /// <summary>値が変わった（インスペクタやシーンビューの調整の道具から）。付けている人が付け直す。</summary>
        public static event Action<CharacterAccessoryCatalog> Edited;

        private void OnValidate() => Edited?.Invoke(this);

        /// <summary>プレハブを差し込む（エディタの組み込みから）。</summary>
        public void SetPrefabs(GameObject[] newHornsLeft, GameObject[] newHornsRight, GameObject[] newEarsLeft, GameObject[] newEarsRight, GameObject[] newTails)
        {
            hornsLeft = newHornsLeft;
            hornsRight = newHornsRight;
            earsLeft = newEarsLeft;
            earsRight = newEarsRight;
            tails = newTails;
        }

        /// <summary>色見本を差し込む（エディタの組み込みから）。</summary>
        public void SetColors(Color[] newHornColors, Color[] newEarColors, Color[] newTailColors)
        {
            hornColors = newHornColors;
            earColors = newEarColors;
            tailColors = newTailColors;
        }
#endif
    }
}
