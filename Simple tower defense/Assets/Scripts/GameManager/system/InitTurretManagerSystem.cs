using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct InitTurretManagerSystem : ISystem {
    private EntityQuery TurretQuery;
    public void OnCreate(ref SystemState state) {
        var build = new EntityQueryBuilder(Allocator.TempJob)
            .WithAll<BasicAttributeData>()
            .WithAll<LocalTransform>()
            .WithOptions(EntityQueryOptions.IncludeDisabledEntities);
        ;
        TurretQuery = build.Build(state.EntityManager);
        state.RequireForUpdate(TurretQuery);
    }
    [BurstCompile]
    private void OnUpdate(ref SystemState state) {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        state.Dependency = new InitTurretJob()
        {
            ECB = ecb.AsParallelWriter()
        }.ScheduleParallel(TurretQuery, state.Dependency);
        state.CompleteDependency();
        ecb.Playback(state.EntityManager);

        ecb.Dispose();
    }

    public partial struct InitTurretJob : IJobEntity {
        public EntityCommandBuffer.ParallelWriter ECB;
        private void Execute([EntityIndexInQuery] int index,
            Entity e,
            ref BasicAttributeData data,
            in LocalTransform trans) {

            if (data.Init) return;
            data.Init = true;
            if (data.Type == DataType.Turret) {
                data.MaxHP = 10;
                data.CurrentHP = data.MaxHP;
                data.CurrentAttackRange = 3;
            } else if (data.Type == DataType.Core) {
                data.MaxHP = 100;
                data.CurrentHP = data.MaxHP;
                data.CurrentAttackRange = 5;
            } else {
                data.MaxHP = 20;
                data.CurrentHP = data.MaxHP;
                data.CurrentAttackRange = 2;
            }

            var posY = trans.Position.y;
            var tempTrans = LocalTransform.FromPosition(new float3(data.CurrentPos.x,posY,data.CurrentPos.z));
            ECB.SetComponent(index, e, tempTrans);
            ECB.SetEnabled(index, e, true);
        }
    }
}