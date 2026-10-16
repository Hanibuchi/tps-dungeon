using System;
using System.Collections.Generic;
using UnityEngine;
using TpsDungeon.Map.Data;

namespace TpsDungeon.Map.Authoring
{
    /// <summary>
    /// ダンジョンの道順。層を下から順に並べ、各層は「ランダムな階を randomFloorCount 回 → ボス階」と上る。
    /// 同じ層のランダムな階は 1 つのシーンを読み直して使い回す（毎回シードが変わるので形は変わる）。
    /// 層の数・ランダムな階の数はここを変えればよい。階の規模（広さ・部屋数）や高さは各層の FloorConfig で変える。
    /// 階のシーンの FloorBootstrap がこれを見て、上り階段の行き先を決める。下へは戻れない（デバッグ用の入力を除く）。
    /// </summary>
    [CreateAssetMenu(fileName = "DungeonRoute", menuName = "TPS Dungeon/Dungeon Route")]
    public sealed class DungeonRoute : ScriptableObject
    {
        [Serializable]
        public sealed class Layer
        {
            [Tooltip("表示名（例: 第 1 層）。")]
            public string name;

            [Tooltip("ランダムな階のシーン名（Build Settings に入っている名前。例: L1_Random）。")]
            public string randomScene;
            [Tooltip("ランダムな階の生成設定。広さ・部屋数・高さはこれで変える。")]
            public FloorConfig randomConfig;
            [Min(0), Tooltip("ボス階の前に上るランダムな階の数。")]
            public int randomFloorCount = 4;

            [Tooltip("ボス階のシーン名（例: L1_Boss）。空ならこの層にボス階は無い。")]
            public string bossScene;
            [Tooltip("ボス階の生成設定。")]
            public FloorConfig bossConfig;

            public bool HasBoss => !string.IsNullOrEmpty(bossScene);

            /// <summary>この層の階の数（ランダムな階 + ボス階）。</summary>
            public int FloorCount => Mathf.Max(0, randomFloorCount) + (HasBoss ? 1 : 0);

            public bool IsBoss(int floor) => HasBoss && floor == Mathf.Max(0, randomFloorCount);
        }

        [SerializeField, Tooltip("層の並び（下から順に）。")]
        private List<Layer> layers = new List<Layer>();

        [SerializeField, Tooltip("下り階段で 1 つ下の階へ降りられるようにする（1 層 1 階からは地上へ）。普段は切っておく（下へは戻れない）。デバッグ用だが本編で使ってもよい。")]
        private bool allowGoingDown;

        /// <summary>下り階段で 1 つ下の階へ降りられるか。</summary>
        public bool AllowGoingDown
        {
            get => allowGoingDown;
            set => allowGoingDown = value;
        }

        public IReadOnlyList<Layer> Layers => layers;

        public void SetLayers(IEnumerable<Layer> newLayers) => layers = new List<Layer>(newLayers);

        /// <summary>ダンジョンの最初の階（1 層の 1 階）。階が 1 つも無ければ null。</summary>
        public DungeonFloor? First => FirstFrom(0);

        public bool Contains(DungeonFloor floor) =>
            floor.Layer >= 0 && floor.Layer < layers.Count && floor.Floor >= 0 && floor.Floor < layers[floor.Layer].FloorCount;

        public bool IsBoss(DungeonFloor floor) => Contains(floor) && layers[floor.Layer].IsBoss(floor.Floor);

        /// <summary>1 つ上の階。最後の階なら null。階の無い層は飛ばす。</summary>
        public DungeonFloor? Next(DungeonFloor floor)
        {
            if (!Contains(floor)) return null;
            if (floor.Floor + 1 < layers[floor.Layer].FloorCount) return new DungeonFloor(floor.Layer, floor.Floor + 1);
            return FirstFrom(floor.Layer + 1);
        }

        /// <summary>1 つ下の階。最初の階なら null（デバッグで下へ移るときに使う）。</summary>
        public DungeonFloor? Previous(DungeonFloor floor)
        {
            if (!Contains(floor)) return null;
            if (floor.Floor > 0) return new DungeonFloor(floor.Layer, floor.Floor - 1);
            for (int layer = floor.Layer - 1; layer >= 0; layer--)
            {
                int count = layers[layer].FloorCount;
                if (count > 0) return new DungeonFloor(layer, count - 1);
            }
            return null;
        }

        /// <summary>その階のシーン名。</summary>
        public string SceneOf(DungeonFloor floor)
        {
            if (!Contains(floor)) return null;
            var layer = layers[floor.Layer];
            return layer.IsBoss(floor.Floor) ? layer.bossScene : layer.randomScene;
        }

        /// <summary>その階の生成設定。</summary>
        public FloorConfig ConfigOf(DungeonFloor floor)
        {
            if (!Contains(floor)) return null;
            var layer = layers[floor.Layer];
            return layer.IsBoss(floor.Floor) ? layer.bossConfig : layer.randomConfig;
        }

        /// <summary>そのシーンで始まる最初の階（ランダムな階のシーンなら 1 階、ボス階のシーンならボス階）。載っていなければ null。</summary>
        public DungeonFloor? FirstFloorOfScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return null;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (layer.randomScene == sceneName && layer.randomFloorCount > 0) return new DungeonFloor(i, 0);
                if (layer.HasBoss && layer.bossScene == sceneName) return new DungeonFloor(i, Mathf.Max(0, layer.randomFloorCount));
            }
            return null;
        }

        /// <summary>表示名（例:「第 2 層 3 階」「第 2 層 ボス」）。</summary>
        public string Label(DungeonFloor floor)
        {
            if (!Contains(floor)) return floor.ToString();
            var layer = layers[floor.Layer];
            string name = string.IsNullOrEmpty(layer.name) ? $"第 {floor.Layer + 1} 層" : layer.name;
            return layer.IsBoss(floor.Floor) ? $"{name} ボス" : $"{name} {floor.Floor + 1} 階";
        }

        /// <summary>載っているシーン名を全部（重複なし、下の層から順）。</summary>
        public IEnumerable<string> SceneNames()
        {
            var seen = new HashSet<string>();
            foreach (var layer in layers)
            {
                if (layer.randomFloorCount > 0 && !string.IsNullOrEmpty(layer.randomScene) && seen.Add(layer.randomScene)) yield return layer.randomScene;
                if (layer.HasBoss && seen.Add(layer.bossScene)) yield return layer.bossScene;
            }
        }

        private DungeonFloor? FirstFrom(int layer)
        {
            for (int i = Mathf.Max(0, layer); i < layers.Count; i++)
            {
                if (layers[i].FloorCount > 0) return new DungeonFloor(i, 0);
            }
            return null;
        }
    }
}
