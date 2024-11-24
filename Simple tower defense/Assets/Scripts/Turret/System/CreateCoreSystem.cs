using Unity.Burst;
using Unity.Entities;
using YY.MainGame;

namespace YY.Turret {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct CreateCoreSystem : ISystem {
        [BurstCompile]
        private void OnCreate(ref SystemState state) {
            state.RequireForUpdate<CreateTurretBuffer>();
        }
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            state.Enabled = false;

            var baseData = SystemAPI.GetSingletonBuffer<CreateTurretBuffer>();
            baseData.Add(new CreateTurretBuffer
            {
                type = TurretType.Core,
                Num = 1,
                Pos = new Unity.Mathematics.float3(0, 0, 0),
            });
        }
    }
}
