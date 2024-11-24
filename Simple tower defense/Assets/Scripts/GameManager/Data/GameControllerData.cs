using Unity.Entities;

namespace YY.MainGame {
    public enum GameStateType {
        Begin,
        Over,
    }
    public partial struct GameControllerData : IComponentData {
        public GameStateType Type;
    }
}
