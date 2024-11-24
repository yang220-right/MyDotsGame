using Unity.Entities;

namespace YY.MainGame {
    public enum GameStateType {
        BeginGame,
        BeginOver,
    }
    public partial struct GameControllerData : IComponentData {
        public GameStateType gameStateType;
    }
}
