using Unity.Entities;

namespace YY.Turret {
    public partial struct CreateCoreSystem : ISystem {
        private void OnCreate(ref SystemState state) {
            state.RequireForUpdate<CreateTurretBuffer>();
        }
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
