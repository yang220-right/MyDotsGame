using Unity.Entities;
using Unity.Mathematics;

public partial struct BaseEnemyData : IComponentData {
    public float3 MovePos;//想要到达的位置
    public bool ForceTo;//强制位移,例如被嘲讽或者不攻击防御塔
}
