using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using YY.Turret;

namespace YY.MainGame {
    public enum GameStateType {
        Begin,
        Over,
    }
    public partial struct GameControllerData : IComponentData {
        public GameStateType Type;

        public float GeneratorEnemyPerSeconds;
        public int MapColumn;//x
        public int MapRow;//y
    }
}
