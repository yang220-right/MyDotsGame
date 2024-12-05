using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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
    /// <summary>
    /// 先创建然后再初始化
    /// </summary>
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
                        basicAttributeData.MaxHP = 100000;
                        basicAttributeData.Type = DataType.Core;
                        basicAttributeData.BaseAttackInterval = 0.3f;
                        basicAttributeData.CurrentAttackCircle = 15;
                        basicAttributeData.BaseAttack = 2;

                        ECB.AddComponent(index, e, basicAttributeData);
                        ECB.AddComponent<TurretBaseCoreData>(index, e);
                        ECB.AddComponent<BaseCoreTag>(index, e);
                    } else {
                        var baseTurretData = new BaseTurretData();

                        switch (item.type) {
                            case TurretType.GunTowers:
                                basicAttributeData.BaseAttackInterval = 0.3f;
                                basicAttributeData.BaseAttack = 2;

                                baseTurretData.AttackType = AttackRangeType.Single;
                                e = ECB.Instantiate(index, turretPrefabData.MachineGunBasePrefab);
                                break;
                            case TurretType.FireTowers:
                                basicAttributeData.BaseAttackInterval = 2;
                                basicAttributeData.BaseAttack = 2;
                                basicAttributeData.AttackAngle = 60;

                                baseTurretData.AttackType = AttackRangeType.Fans;
                                e = ECB.Instantiate(index, turretPrefabData.FireTowersPrefab);
                                break;
                            case TurretType.MortorTowers:
                                basicAttributeData.BaseAttack = 30;
                                basicAttributeData.BaseAttackInterval = 2f;

                                baseTurretData.AttackType = AttackRangeType.Single;
                                baseTurretData.BulletCircle = 5;
                                e = ECB.Instantiate(index, turretPrefabData.MortorTowersPrefab);
                                break;
                            case TurretType.SniperTowers:
                                basicAttributeData.BaseAttack = 300;
                                basicAttributeData.BaseAttackInterval = 5f;

                                baseTurretData.AttackType = AttackRangeType.Single;
                                e = ECB.Instantiate(index, turretPrefabData.SniperTowersPrefab);
                                break;
                            case TurretType.GuideTowers:
                                basicAttributeData.BaseAttack = 200;
                                basicAttributeData.BaseAttackInterval = 3;

                                baseTurretData.AttackType = AttackRangeType.Single;
                                baseTurretData.BulletCircle = 2;
                                e = ECB.Instantiate(index, turretPrefabData.GuideTowerPrefab);
                                break;
                            case TurretType.PlagueTowers:
                                basicAttributeData.BaseAttack = 1f;
                                basicAttributeData.BaseAttackInterval = 0.3f;

                                baseTurretData.AttackType = AttackRangeType.Circle;
                                e = ECB.Instantiate(index, turretPrefabData.GuideTowerPrefab);
                                break;
                        }
                        //共同属性
                        basicAttributeData.MaxHP = 1000;
                        basicAttributeData.CurrentAttackCircle = 20;

                        baseTurretData.Type = item.type;
                        ECB.AddComponent(index, e, baseTurretData);
                        ECB.AddComponent<TurretTag>(index, e);
                    }

                    basicAttributeData.CurrentHP = basicAttributeData.MaxHP;
                    basicAttributeData.Type = DataType.Turret;
                    basicAttributeData.CurrentAttack = basicAttributeData.BaseAttack;
                    basicAttributeData.CurrentAttackInterval = basicAttributeData.BaseAttackInterval;
                    basicAttributeData.RemainAttackIntervalTime = basicAttributeData.CurrentAttackInterval;
                    ECB.AddComponent(index, e, basicAttributeData);
                    ECB.AddBuffer<CreateProjectBuffer>(index, e);
                    ECB.AddBuffer<ReduceHPBuffer>(index, e);
                    ECB.SetEnabled(index, e, false);
                }
            }
            turretList.Clear();
        }
    }
}