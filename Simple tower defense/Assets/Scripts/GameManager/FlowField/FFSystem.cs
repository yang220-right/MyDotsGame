using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace YY.MainGame {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct FFSystem : ISystem {
        public FFControllerData data;
        private void OnCreate(ref SystemState state) {
            state.RequireForUpdate<FFControllerData>();
        }
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            data = SystemAPI.GetSingleton<FFControllerData>();
            var entity = SystemAPI.GetSingletonEntity<FFControllerData>();
            if (!data.BeginInit) return;
            var ffDataArr = new NativeArray<FFCellData>(data.Column * data.Row, Allocator.TempJob);
            if (!data.EndInit) {
                data.EndInit = true;
                for (var y = 0; y < data.Column; y++)
                    for (int x = 0; x < data.Row; x++)
                        ffDataArr[y * data.Column + x] = new FFCellData(new int2(x, y));
            } else {
                ffDataArr = data.allMapData;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var result = new FFUpdateJob()
            {
                col = data.Column,
                row = data.Row,
                DataArr = ffDataArr
            };
            state.Dependency = result.Schedule(state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);

            data.allMapData = result.DataArr;
            state.EntityManager.SetComponentData(entity, data);
            ecb.Dispose();
        }
        [BurstCompile]
        public partial struct FFUpdateJob : IJobEntity {
            [ReadOnly]public int col;
            [ReadOnly]public int row;
            public NativeArray<FFCellData> DataArr;
            //权重
            [BurstCompile]
            public void Execute(ref DynamicBuffer<FFPosBuffer> buffer) {
                if (buffer.Length <= 0) return;
                foreach (var item in buffer) {
                    var pos = item.Pos;
                    int lengthBegin = 0;
                    int length = 1;
                    var tempArr = new NativeArray<int2>(col * row ,Allocator.Temp);
                    var searchArr = new NativeArray<bool>(col * row ,Allocator.Temp);
                    tempArr[lengthBegin] = pos;
                    InitSearchArr(searchArr);
                    DotsUtility.GetIndexByXY(pos.x, pos.y, col, out var index);
                    DataArr[index] = new FFCellData(pos, 0);
                    while (lengthBegin != length) {
                        DotsUtility.GetIndexByXY(tempArr[lengthBegin].x, tempArr[lengthBegin].y, col, out index);
                        searchArr[index] = true;
                        //获取值
                        var value = DataArr[index].Value;
                        var around = new NativeArray<int2>(4,Allocator.Temp);
                        int count = FindAround(index, around);
                        //比较四周的值
                        for (int i = 0; i < count; i++) {
                            var newPos = around[i];
                            if (!searchArr[GetIndex(newPos.x, newPos.y, col)]) {
                                tempArr[length] = newPos;
                                searchArr[GetIndex(newPos.x, newPos.y, col)] = true;
                                ++length;
                            }
                            //加权
                            DotsUtility.GetIndexByXY(newPos.x, newPos.y, col, out int tempIndex);
                            if (DataArr[tempIndex].Value > value + 1) 
                                DataArr[tempIndex] = new FFCellData(newPos, value + 1);
                        }
                        ++lengthBegin;
                    }
                    buffer.Clear();
                }
            }
            private void InitSearchArr(NativeArray<bool> arr) {
                for (int i = 0; i < arr.Length; i++) arr[i] = false;
            }
            private int FindAround(in int index, NativeArray<int2> arr) {
                DotsUtility.GetPosByIndex(index, col, out var pos);
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
                return count;
            }
            private int GetIndex(int x, int y, int col) {
                DotsUtility.GetIndexByXY(x, y, col, out var index);
                return index;
            }
        }
    }
}
