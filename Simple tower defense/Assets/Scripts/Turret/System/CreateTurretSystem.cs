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
                    Entity e = default;
                    var basicAttributeData = new BasicAttributeData()
                    {
                        CurrentPos = item.Pos
                    };

                    if (item.type == TurretType.Core) {
                        e = ECB.Instantiate(index, turretPrefabData.CorePrefab);
                        basicAttributeData.Type = DataType.Core;
                        basicAttributeData.BaseAttack = 2;
                        basicAttributeData.CurrentAttack = basicAttributeData.BaseAttack;

                        ECB.AddComponent(index, e, basicAttributeData);
                        ECB.AddComponent<TurretBaseCoreData>(index, e);
                        ECB.AddComponent<BaseCoreTag>(index, e);
                    } else {
                        var baseTurretData = new BaseTurretData();
                        switch (item.type) {
                            case TurretType.GunTowers:
                                basicAttributeData.BaseAttack = 2;
                                e = ECB.Instantiate(index, turretPrefabData.MachineGunBasePrefab);
                                break;
                            case TurretType.FireTowers:
                                basicAttributeData.BaseAttack = 2;
                                e = ECB.Instantiate(index, turretPrefabData.FireTowersPrefab);
                                break;
                            case TurretType.MortorTowers:
                                basicAttributeData.BaseAttack = 30;
                                baseTurretData.BulletCircle = 2;
                                e = ECB.Instantiate(index, turretPrefabData.MortorTowersPrefab);
                                break;
                            case TurretType.SniperTowers:
                                basicAttributeData.BaseAttack = 300;
                                e = ECB.Instantiate(index, turretPrefabData.SniperTowersPrefab);
                                break;
                        }
                        //共同属性
                        basicAttributeData.Type = DataType.Turret;
                        basicAttributeData.CurrentAttack = basicAttributeData.BaseAttack;
                        baseTurretData.Type = item.type;
                        ECB.AddComponent(index, e, basicAttributeData);
                        ECB.AddComponent(index, e, baseTurretData);
                        ECB.AddComponent<TurretTag>(index, e);
                    }
                    ECB.AddBuffer<CreateProjectBuffer>(index, e);
                    ECB.AddBuffer<ReduceHPBuffer>(index, e);
                    ECB.SetEnabled(index, e, false);
                }
            }
            turretList.Clear();
        }
    }
}