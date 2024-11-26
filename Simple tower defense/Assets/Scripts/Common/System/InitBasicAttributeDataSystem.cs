using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using YY.Enemy;
using YY.Turret;
using static UnityEditor.Progress;

namespace YY.MainGame {
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct InitBasicAttributeDataSystem : ISystem {
        private EntityQuery AllDataQuery;
        private EntityQuery AllCoreQuery;
        private EntityQuery AllTurretQuery;
        private EntityQuery AllEnemyQuery;
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            AllDataQuery =
                new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithAll<LocalTransform>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(state.EntityManager);
            AllCoreQuery =
                new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithAll<TurretBaseCoreData>()
                .Build(state.EntityManager);
            AllTurretQuery =
                new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithAll<BaseTurretData>()
                .Build(state.EntityManager);
            AllEnemyQuery =
                new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithAll<BaseEnemyData>()
                .Build(state.EntityManager);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            state.Dependency = new InitBasicAttributeDataJob()
            {
                ECB = ecb.AsParallelWriter(),
            }.Schedule(AllDataQuery, state.Dependency);
            state.CompleteDependency();
            state.Dependency = new InitCoreAttributeDatajob
            {
                ECB = ecb.AsParallelWriter(),
            }.Schedule(AllCoreQuery, state.Dependency);
            state.CompleteDependency();
            state.Dependency = new InitTurretAttributeDataJob
            {
                ECB = ecb.AsParallelWriter(),
            }.Schedule(AllTurretQuery, state.Dependency);
            state.CompleteDependency();
            state.Dependency = new InitEnemyAttributeDatajob
            {
                ECB = ecb.AsParallelWriter(),
            }.Schedule(AllEnemyQuery, state.Dependency);
            state.CompleteDependency();

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            AllDataQuery.Dispose();
            AllCoreQuery.Dispose();
            AllTurretQuery.Dispose();
            AllEnemyQuery.Dispose();
        }

        [BurstCompile]
        public partial struct InitBasicAttributeDataJob : IJobEntity {
            public EntityCommandBuffer.ParallelWriter ECB;
            [BurstCompile]
            private void Execute([EntityIndexInQuery] int index,
                Entity e,
                ref BasicAttributeData data,
                in LocalTransform trans) {

                if (data.Init) return;
                if (data.Type == DataType.Core) {
                    data.MaxHP = 100000;
                    data.BaseAttackInterval = 0.1f;
                    data.BaseAttack = 2;
                    data.CurrentAttackCircle = 2;
                } else if (data.Type == DataType.Turret) {
                    data.MaxHP = 1000;
                    data.BaseAttack = 2;
                    data.CurrentAttackCircle = 20;
                } else {
                    data.MaxHP = 20;
                    data.BaseAttackInterval = 2;
                    data.BaseAttack = 2;
                    data.CurrentAttackCircle = 2;
                }

                data.CurrentHP = data.MaxHP;
                data.CurrentAttackInterval = data.BaseAttackInterval;
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                data.CurrentAttack = data.BaseAttack;

                var posY = trans.Position.y;
                var tempTrans = LocalTransform.FromPosition(new float3(data.CurrentPos.x,posY,data.CurrentPos.z));

                ECB.SetComponent(index, e, tempTrans);
            }
        }
        [BurstCompile]
        public partial struct InitCoreAttributeDatajob : IJobEntity {
            public EntityCommandBuffer.ParallelWriter ECB;
            [BurstCompile]
            private void Execute([EntityIndexInQuery] int index, Entity e,
                 ref BasicAttributeData data,
                 in TurretBaseCoreData coreData
                ) {
                if (data.Init) return;
                data.Init = true;
                ECB.SetEnabled(index, e, true);
            }
        }
        [BurstCompile]
        public partial struct InitTurretAttributeDataJob : IJobEntity {
            public EntityCommandBuffer.ParallelWriter ECB;
            [BurstCompile]
            private void Execute([EntityIndexInQuery] int index, Entity e,
                 ref BasicAttributeData data,
                ref BaseTurretData turretData) {
                if (data.Init) return;
                //初始化
                data.Type = DataType.Turret;
                switch (turretData.Type) {
                    case TurretType.GunTowers:
                        data.BaseAttackInterval = 0.1f;
                        data.AttackAngle = 0;
                        turretData.AttackType = AttackRangeType.Single;
                        break;
                    case TurretType.FireTowers:
                        data.BaseAttackInterval = 0.5f;
                        data.AttackAngle = 90;
                        turretData.AttackType = AttackRangeType.Fans;
                        break;
                }

                data.CurrentAttackInterval = data.BaseAttackInterval;
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;

                data.Init = true;
                ECB.SetEnabled(index, e, true);
            }
        }
        [BurstCompile]
        public partial struct InitEnemyAttributeDatajob : IJobEntity {
            public EntityCommandBuffer.ParallelWriter ECB;
            [BurstCompile]
            private void Execute([EntityIndexInQuery] int index, Entity e,
                 ref BasicAttributeData data,
                 ref BaseEnemyData enemyData
                ) {
                if (data.Init) return;
                enemyData.Speed = 5;
                data.Init = true;
                ECB.SetEnabled(index, e, true);
            }
        }
    }
}