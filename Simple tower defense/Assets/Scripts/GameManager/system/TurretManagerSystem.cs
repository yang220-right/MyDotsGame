using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public partial struct TurretManagerSystem : ISystem {
    [BurstCompile]
    private void OnCreate(ref SystemState state) {
        state.RequireForUpdate<TurretManagerTag>();
    }
    [BurstCompile]
    private void OnUpdate(ref SystemState state) {

    }
}