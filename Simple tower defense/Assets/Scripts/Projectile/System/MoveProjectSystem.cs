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
        public void Execute([EntityIndexInQuery] int index, Entity e, ref ProjectileData data, ref LocalTransform trans) {
            if (!data.Init) {
                data.Init = true;
                ECB.SetEnabled(index, e, true);
            }
            if (math.dot(math.normalize(data.EndPos - trans.Position), data.MoveDir) <= 0 || data.DeadTime < 0) {
                ECB.DestroyEntity(index, e);
                return;
            }

            trans.Position += data.MoveDir * data.Speed * time;
        }
    }
}
