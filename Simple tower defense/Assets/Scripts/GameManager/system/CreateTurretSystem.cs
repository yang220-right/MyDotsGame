using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateBefore(typeof(InitTurretManagerSystem))]
public partial struct CreateTurretSystem : ISystem {
    [BurstCompile]
    public void OnUpdate(ref SystemState state) {
        var TurretPrefabData = SystemAPI.GetSingleton<TurretPrefabData>();
        var ecb = new EntityCommandBuffer(Allocator.TempJob);

        state.Dependency = new CreateTurretJob()
        {
            ECB = ecb.AsParallelWriter(),
            turretPrefabData = TurretPrefabData,
        }.ScheduleParallel(state.Dependency);
        state.CompleteDependency();

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
    [BurstCompile]
    private partial struct CreateTurretJob : IJobEntity {
        public EntityCommandBuffer.ParallelWriter ECB;
        public TurretPrefabData turretPrefabData;
        [BurstCompile]
        private void Execute([EntityIndexInQuery] int index,
            ref DynamicBuffer<CreateTurretBuffer> turretList) {
            if (turretList.Length <= 0) return;
            foreach (var item in turretList) {
                for (int i = 0; i < item.Num; i++) {
                    switch (item.type) {
                        case TurretType.MachineGun: {
                                var e = ECB.Instantiate(index, turretPrefabData.MachineGunBasePrefab);
                                ECB.AddComponent(index, e, new BasicAttributeData
                                {
                                    CurrentPos = item.Pos,
                                    Type = DataType.Turret,
                                });
                                ECB.AddComponent<BaseTurretData>(index, e);
                                ECB.AddComponent<TurretTag>(index, e);
                                ECB.SetEnabled(index, e, false);
                                break;
                            }
                        default:
                            break;
                    }
                }
            }
            turretList.Clear();
        }
    }
}