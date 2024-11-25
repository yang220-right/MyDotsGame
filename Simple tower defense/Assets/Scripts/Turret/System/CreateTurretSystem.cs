using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using YY.MainGame;
using YY.Projectile;

namespace YY.Turret {
    [UpdateInGroup(typeof(CreateBasicAttributeSystemGroup))]
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
    }
    [BurstCompile]
    public partial struct CreateTurretJob : IJobEntity {
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly]public TurretPrefabData turretPrefabData;
        [BurstCompile]
        private void Execute([EntityIndexInQuery] int index,
            ref DynamicBuffer<CreateTurretBuffer> turretList) {
            if (turretList.Length <= 0) return;
            foreach (var item in turretList) {
                for (int i = 0; i < item.Num; i++) {
                    switch (item.type) {
                        case TurretType.Core: {
                                var e = ECB.Instantiate(index, turretPrefabData.CorePrefab);
                                ECB.AddComponent(index, e, new BasicAttributeData
                                {
                                    CurrentPos = item.Pos,
                                    Type = DataType.Core,
                                });
                                ECB.AddComponent<TurretBaseCoreData>(index, e);
                                ECB.AddComponent<BaseCoreTag>(index, e);
                                ECB.AddBuffer<ReduceHPBuffer>(index, e);
                                ECB.AddBuffer<CreateProjectBuffer>(index, e);
                                ECB.SetEnabled(index, e, false);
                                break;
                            }
                        case TurretType.MachineGun: {
                                var e = ECB.Instantiate(index, turretPrefabData.MachineGunBasePrefab);
                                ECB.AddComponent(index, e, new BasicAttributeData
                                {
                                    CurrentPos = item.Pos,
                                    Type = DataType.Turret,
                                });
                                ECB.AddComponent<BaseTurretData>(index, e);
                                ECB.AddComponent<TurretTag>(index, e);
                                ECB.AddBuffer<ReduceHPBuffer>(index, e);
                                ECB.AddBuffer<CreateProjectBuffer>(index, e);
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