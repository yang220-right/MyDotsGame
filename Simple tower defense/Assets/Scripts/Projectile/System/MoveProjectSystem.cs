using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace YY.Projectile {
    [BurstCompile]
    public partial struct MoveProjectSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var query = new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<ProjectileData>()
                .WithAll<LocalTransform>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(state.EntityManager);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new MoveProjectileJob()
            {
                ECB = ecb.AsParallelWriter(),
                time = SystemAPI.Time.DeltaTime
            }.Schedule(query, state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    [BurstCompile]
    public partial struct MoveProjectileJob : IJobEntity {
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly]public float time;
        [BurstCompile]
        public void Execute([EntityIndexInQuery] int index, Entity e, in ProjectileData data, in LocalTransform trans) {
            var tempData = data;
            var tempTrans = trans;
            if (!tempData.Init) {
                tempData.Init = true;
                ECB.SetEnabled(index, e, true);
            }
            if (math.distancesq(tempTrans.Position, tempData.EndPos) < 0.1f || tempData.DeadTime < 0) {
                ECB.DestroyEntity(index, e);
                return;
            }

            tempTrans.Position += tempData.MoveDir * tempData.Speed * time;

            ECB.SetComponent(index, e, tempData);
            ECB.SetComponent(index, e, tempTrans);
        }
    }
}
