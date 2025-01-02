using Dots.RVO;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using YY.MainGame;

namespace YY.Enemy {
    public partial struct CreateEnemyBuffer : IBufferElementData {
        public EnemyType EnemyType;
        public int Num;
        public float3 Pos;
    }

    public partial struct BaseEnemyData : IComponentData {
        public float Speed;
        public float3 MovePos;//想要到达的位置
        public bool ForceTo;//强制位移,例如被嘲讽或者不攻击防御塔
    }

    public readonly partial struct EnemyAspect : IAspect {
        public readonly Entity entity;
        public readonly RefRW<BaseEnemyData> enemyData;
        public readonly RefRW<BasicAttributeData> baseData;
        public readonly RefRW<AgentData> agent;

        public readonly float3 MovePos => enemyData.ValueRO.MovePos;
        public readonly float3 CurrentPos => baseData.ValueRO.CurrentPos;
        public readonly float3 MoveDir {
            get {
                var dir = enemyData.ValueRO.MovePos - baseData.ValueRO.CurrentPos;
                if (dir.x == 0 && dir.y == 0 && dir.z == 0) {
                    return math.normalize(float3.zero - baseData.ValueRO.CurrentPos);
                }
                return math.normalize(dir);
            }
        }

        public readonly void ResetAttack() {
            baseData.ValueRW.IsBeAttack = false;
            baseData.ValueRW.RemainAttackIntervalTime = baseData.ValueRO.CurrentAttackInterval;
        }
        public readonly void BeAttack(float time) {
            baseData.ValueRW.IsBeAttack = true;
            baseData.ValueRW.RemainAttackIntervalTime -= time;
        }
        /// <summary>
        /// 数据同步
        /// </summary>
        public readonly void SynchronizeData() {
        }
        public readonly float3 ResetPos(float3 pos) {
            return new float3(CurrentPos.x, pos.y, CurrentPos.z);
        }
        public readonly void SetMove(float3 move) {
            enemyData.ValueRW.MovePos = move;
        }
        public readonly void MoveTo(float delTime) {
            baseData.ValueRW.CurrentPos += delTime * MoveDir;
        }

        #region ORCA



        #endregion
    }

    [MaterialProperty("_BaseColor")]
    public partial struct ShaderOverrideColor : IComponentData {
        public float4 Value;
    }
}