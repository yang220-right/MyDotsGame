using Unity.Entities;
using UnityEngine;

namespace YY.MainGame {
    public class GameManager : MonoBehaviour {
        public GameSettings Settings;
    }
    public class GameManagerBaker : Baker<GameManager> {
        public override void Bake(GameManager authoring) {
            var e = GetEntity(TransformUsageFlags.None);
            DependsOn(authoring.Settings);
            AddComponent(e, new GameControllerData
            {
                Type = GameStateType.Begin,

                GeneratorEnemyPerSeconds = authoring.Settings.GeneratorEnemyPerSeconds,
                MapColumn = authoring.Settings.MapColumn,
                MapRow = authoring.Settings.MapRow,
            });
        }
    }
}