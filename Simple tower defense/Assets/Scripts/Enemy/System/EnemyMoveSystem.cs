using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using YY.MainGame;
using static UnityEngine.Rendering.DebugUI.Table;

namespace YY.Enemy {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(FFSystem))]
    public partial struct EnemyMoveSystem : ISystem {
        private void OnCreate(ref SystemState state) {
            state.RequireForUpdate<FFControllerData>();
        }
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            var ffControllData = SystemAPI.GetSingleton<FFControllerData>();
            if (!ffControllData.EndInit)
                return;

            var moveJob = new MoveJob()
            {
                time = SystemAPI.Time.DeltaTime,
                ffDatas = ffControllData.AllMapData,
                col = ffControllData.Column,
                row = ffControllData.Row
            };
            state.Dependency = moveJob.Schedule(state.Dependency);
            state.CompleteDependency();
        }
    }
    [BurstCompile]
    [WithNone(typeof(NewItemTag))]
    public partial struct MoveJob : IJobEntity {
        [ReadOnly] public float time;
        [ReadOnly] public NativeArray<FFCellData> ffDatas;
        [ReadOnly] public int col;
        [ReadOnly] public int row;
        [BurstCompile]
        private void Execute([EntityIndexInQuery] int index, EnemyAspect data, ref LocalTransform trans) {
            DotsUtility.ToFFPos(data.CurrentPos.xz, out var xy);
            DotsUtility.GetIndexByXY(xy.x, xy.y, col, out var ffIndex);
            if (ffIndex > col * row) return;
            var ffData = ffDatas[ffIndex];
            if (ffData.Value <= data.baseData.ValueRO.CurrentAttackCircle) {
                data.DisableRVO();
                data.BeAttack(time);
                trans = trans.RotateY(5 * time);//旋转
                return;
            } else if (math.distancesq(data.CurrentPos, data.MovePos) < 0.01f || data.MovePosValue > ffData.Value) {
                //获取当前位置值小的地方
                data.EnableRVO();
                var minPos = FindMinValue(ffDatas, ffData.Pos);
                data.SetMove(new float3(minPos.x, 0, minPos.y));
                DotsUtility.ToFFPos(minPos, out var ffMinPos);
                DotsUtility.GetIndexByXY(ffMinPos.x, ffMinPos.y, col, out var targetIndex);
                data.SetMoveValue(ffDatas[targetIndex].Value);
            }

            data.ResetAttack();
            data.SetVocity();
            trans = LocalTransform.FromPosition(data.ResetPos(trans.Position));
        }
        [BurstCompile]
        private float2 FindMinValue(in NativeArray<FFCellData> data, int2 pos) {
            NativeArray<int2> aroundArr = new NativeArray<int2>(4,Allocator.Temp);
            FindAround(pos, col, row, aroundArr, out var count);
            int minValue = int.MaxValue;
            int2 minPos = new int2(0);
            for (int i = 0; i < count; i++) {
                DotsUtility.GetIndexByXY(aroundArr[i].x, aroundArr[i].y, col, out var index);
                if (minValue > data[index].Value) {
                    minValue = data[index].Value;
                    minPos = aroundArr[i];
                }
            }
            DotsUtility.ToPos(minPos, out var newMinPos);
            return newMinPos;
        }
        [BurstCompile]
        private void FindAround(in int2 pos, in int col, in int row, NativeArray<int2> arr, out int value) {
            NativeArray<int> dx = new NativeArray<int>(4,Allocator.Temp);
            NativeArray<int> dy = new NativeArray<int>(4,Allocator.Temp);
            dx[0] = 0; dy[0] = 1;
            dx[1] = 1; dy[1] = 0;
            dx[2] = 0; dy[2] = -1;
            dx[3] = -1; dy[3] = 0;
            int count = 0;
            for (int i = 0; i < 4; i++) {
                int newX = pos.x + dx[i];
                int newY = pos.y + dy[i];
                if (newX >= 0 && newX < col && newY >= 0 && newY < row)
                    arr[count++] = new int2(newX, newY);
            }
            value = count;
        }
    }

}
