using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace YY.MainGame {
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct InitBasicAttributeDataSystem : ISystem {
        private EntityQuery AllDataQuery;
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            AllDataQuery =
                new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithAll<NewItemTag>()
                .WithAll<LocalTransform>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(state.EntityManager);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
           
            state.Dependency = new InitAttributeDatajob
            {
                ECB = ecb.AsParallelWriter(),
            }.Schedule(AllDataQuery, state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);

            ecb.Dispose();
            AllDataQuery.Dispose();
        }

        [BurstCompile]
        public partial struct InitAttributeDatajob : IJobEntity {
            public EntityCommandBuffer.ParallelWriter ECB;
            [BurstCompile]
            private void Execute([EntityIndexInQuery] int index, Entity e,
                 ref BasicAttributeData data,
                 in LocalTransform trans
                ) {
                var posY = trans.Position.y;
                var tempTrans = LocalTransform.FromPosition(new float3(data.CurrentPos.x,posY,data.CurrentPos.z));
                ECB.SetComponent(index, e, tempTrans);

                ECB.RemoveComponent<NewItemTag>(index, e);
                ECB.SetEnabled(index, e, true);
            }
        }
    }
}