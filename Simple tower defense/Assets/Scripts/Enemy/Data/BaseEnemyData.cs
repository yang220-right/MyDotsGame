using Unity.Entities;
using Unity.Mathematics;

namespace YY.Enemy {
    public partial struct BaseEnemyData : IComponentData {
        public float3 MovePos;//想要到达的位置
        public bool ForceTo;//强制位移,例如被嘲讽或者不攻击防御塔
    }

    public readonly partial struct EnemyAspect : IAspect {
        public readonly RefRW<BaseEnemyData> enemyData;
        public readonly RefRW<BasicAttributeData> baseData;
    }
}