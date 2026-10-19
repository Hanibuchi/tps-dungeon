using TpsDungeon.Enemies;
using UnityEngine;

namespace TpsDungeon.Items
{
    /// <summary>
    /// 落とし物（倒れた敵・壊した箱から出た武器）を床に出す。
    /// 拾う物のプレハブは照準用のトリガーしか持たないので、同じ大きさの物理の箱を死体・落とし物のレイヤー（<see cref="DebrisLayer"/>）で足し、
    /// Rigidbody で床まで落とす（<see cref="DroppedItemSettle"/> が止まったら固める）。このレイヤーは床や壁とだけ当たるので、パーティーの邪魔にならない。
    /// </summary>
    public static class ItemDropper
    {
        /// <summary>item を position に出して落とす。出せなければ null。</summary>
        public static ItemPickup Drop(ItemInstance item, Vector3 position)
        {
            ItemPickup prefab = item.WorldPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"{item.DisplayName} には床に出す物が無いので落とせない", item.Definition);
                return null;
            }

            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            ItemPickup pickup = Object.Instantiate(prefab, position, rotation);
            pickup.name = prefab.name;
            pickup.Instance = item;
            MakeFall(pickup);
            pickup.PlayDropSound();
            return pickup;
        }

        /// <summary>
        /// 照準用のトリガーと同じ大きさの物理の箱を足し、Rigidbody で床へ落とす。向きはプレハブのまま（回転は止める）。
        /// </summary>
        private static void MakeFall(ItemPickup pickup)
        {
            BoxCollider trigger = null;
            foreach (BoxCollider box in pickup.GetComponentsInChildren<BoxCollider>())
            {
                if (!box.isTrigger) continue;
                trigger = box;
                break;
            }
            if (trigger == null)
            {
                Debug.LogWarning($"{pickup.name} にトリガーの BoxCollider が無いので、床へ落とせない", pickup);
                return;
            }

            var physics = new GameObject("Physics");
            int debris = DebrisLayer.Index;
            if (debris >= 0) physics.layer = debris;
            physics.transform.SetParent(trigger.transform, false);
            var solid = physics.AddComponent<BoxCollider>();
            solid.center = trigger.center;
            solid.size = trigger.size;

            var body = pickup.gameObject.AddComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            pickup.gameObject.AddComponent<DroppedItemSettle>();
        }
    }
}
