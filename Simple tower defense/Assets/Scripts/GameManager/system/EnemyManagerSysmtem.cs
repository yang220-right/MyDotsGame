using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using YY.MainGame;

namespace YY.Enemy {
    public partial struct EnemyManagerSysmtem : ISystem {

    }
    public partial struct EnemyMoveSystem : ISystem {
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            var query = new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithAll<BaseEnemyData>()
                .WithAll<LocalTransform>()

                .Build(state.EntityManager)
                ;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new MoveJob()
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
    public partial struct MoveJob : IJobEntity {
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly] public float time;
        [BurstCompile]
        private void Execute([EntityIndexInQuery] int index, EnemyAspect data, ref LocalTransform trans) {
            if (math.distancesq(trans.Position, data.MovePos) < 2 * 2) {
                data.BeAttack(time);
                var rot = trans.RotateY(5 * time);
                ECB.SetComponent(index, data.entity, rot);
                return;
            }
            data.ResetAttack();
            data.MoveTo(time);
            var tempTran = LocalTransform.FromPosition(data.ResetPos(trans.Position));
            ECB.SetComponent(index, data.entity, tempTran);
        }
    }

}
