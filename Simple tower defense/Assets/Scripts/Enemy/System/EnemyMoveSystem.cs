using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using YY.MainGame;

namespace YY.Enemy {
    public partial struct EnemyMoveSystem : ISystem {
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            new MoveJob(){
                time = SystemAPI.Time.DeltaTime
            }.Schedule();
        }
    }
    [BurstCompile]
    public partial struct MoveJob : IJobEntity {
        [ReadOnly] public float time;
        [BurstCompile]
        private void Execute([EntityIndexInQuery] int index, EnemyAspect data, ref LocalTransform trans) {
            if (math.distancesq(trans.Position, data.MovePos) < 2 * 2) {
                data.BeAttack(time);
                trans = trans.RotateY(5 * time);
                return;
            }
            data.ResetAttack();
            data.MoveTo(time);
            trans = LocalTransform.FromPosition(data.ResetPos(trans.Position));
        }
    }

}
