using System;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>重ねて出すエフェクトの 1 枚。着弾のように、いくつかの素材を重ねて派手にするときに使う。</summary>
    [Serializable]
    public struct EffectLayer
    {
        [Tooltip("出す見た目。")]
        public GameObject prefab;

        [Tooltip("大きさの倍率。素材ごとに大きさがまちまちなので、ここで揃える。")]
        public float scale;

        [Tooltip("出す位置のずらし（出す向きから見たローカル、m）。")]
        public Vector3 offset;

        public EffectLayer(GameObject prefab, float scale, Vector3 offset = default)
        {
            this.prefab = prefab;
            this.scale = scale;
            this.offset = offset;
        }
    }
}
