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
        private EntityQuery AllTurretQuery;
        private EntityQuery AllEnemyQuery;
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            AllTurretQuery =
                new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithAll<LocalTransform>()
                .Build(state.EntityManager);
            AllEnemyQuery =
                new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithAll<BaseEnemyData>()
                .WithAll<LocalTransform>()
                .Build(state.EntityManager);

            var ecbEnemy = new EntityCommandBuffer(Allocator.TempJob);
            var ecbTurret = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new InitEnemyAttributeDatajob
            {
                ECB = ecbEnemy.AsParallelWriter(),
            }.Schedule(AllEnemyQuery, state.Dependency);
            state.CompleteDependency();
            ecbEnemy.Playback(state.EntityManager);

            state.Dependency = new InitAttributeDatajob
            {
                ECB = ecbTurret.AsParallelWriter(),
            }.Schedule(AllTurretQuery, state.Dependency);
            state.CompleteDependency();
            ecbTurret.Playback(state.EntityManager);

            ecbEnemy.Dispose();
            ecbTurret.Dispose();
            AllTurretQuery.Dispose();
            AllEnemyQuery.Dispose();
        }

        [BurstCompile]
        public partial struct InitAttributeDatajob : IJobEntity {
            public EntityCommandBuffer.ParallelWriter ECB;
            [BurstCompile]
            private void Execute([EntityIndexInQuery] int index, Entity e,
                 ref BasicAttributeData data,
                 in LocalTransform trans
                ) {
                if (data.Init) return;
                var posY = trans.Position.y;
                var tempTrans = LocalTransform.FromPosition(new float3(data.CurrentPos.x,posY,data.CurrentPos.z));
                ECB.SetComponent(index, e, tempTrans);

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
                 ref BaseEnemyData enemyData,
                 in LocalTransform trans
                ) {
                if (data.Init) return;
                data.MaxHP = 4;
                data.BaseAttackInterval = 2;
                data.CurrentAttackCircle = 2;
                data.CurrentHP = data.MaxHP;
                data.CurrentAttackInterval = data.BaseAttackInterval;
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                data.Init = true;

                enemyData.Speed = 5;

                var posY = trans.Position.y;
                var tempTrans = LocalTransform.FromPosition(new float3(data.CurrentPos.x,posY,data.CurrentPos.z));

                ECB.SetEnabled(index, e, true);
                ECB.SetComponent(index, e, tempTrans);
            }
        }
    }
}