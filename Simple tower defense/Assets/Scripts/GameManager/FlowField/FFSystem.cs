using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace YY.MainGame {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(QuadFindSystem))]
    [BurstCompile]
    public partial struct FFSystem : ISystem {
        public FFControllerData data;
        private void OnCreate(ref SystemState state) {
            state.RequireForUpdate<FFControllerData>();
            state.RequireForUpdate<GameControllerData>();
        }
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            data = SystemAPI.GetSingleton<FFControllerData>();
            var entity = SystemAPI.GetSingletonEntity<FFControllerData>();
            //初始化
            if (!data.BeginInit) {
                var controlData = SystemAPI.GetSingleton<GameControllerData>();
                data.BeginInit = true;
                data.Column = controlData.MapColumn;
                data.Row = controlData.MapRow;
                data.Reset = false;
                data.TempCalculateData = default;
                data.NeedCalculateData = default;
                data.AllMapData = default;
            }
            var ffDataArr = new NativeArray<FFCellData>(data.Column * data.Row, Allocator.TempJob);
            var tempPos = new NativeParallelHashSet<int2>(64,Allocator.TempJob);
            if (!data.EndInit || data.Reset) {
                data.EndInit = true;
                data.Reset = false;
                data.TempCalculateData = data.NeedCalculateData;
                data.NeedCalculateData = default;
                for (var y = 0; y < data.Column; y++)
                    for (int x = 0; x < data.Row; x++)
                        ffDataArr[y * data.Column + x] = new FFCellData(new int2(x, y));
            } else {
                ffDataArr = data.AllMapData;
            }
            bool needReset =  data.TempCalculateData.Length != 0;
            var ffCalculateNum = data.TempCalculateData.Length>3?3:data.TempCalculateData.Length;
            var ffCalculateArr = new NativeArray<int2>(ffCalculateNum,Allocator.TempJob);

            for (int i = 0; i < ffCalculateNum; i++)
                ffCalculateArr[i] = data.TempCalculateData[i];
            var ffTempCalculateData = new NativeArray<int2>(data.TempCalculateData.Length - ffCalculateNum,Allocator.TempJob);
            for (int i = ffCalculateNum; i < data.TempCalculateData.Length; i++)
                ffTempCalculateData[i - ffCalculateNum] = data.TempCalculateData[i];
            data.TempCalculateData = ffTempCalculateData;

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var result = new FFUpdateJob()
            {
                NeedCalcute = needReset,
                col = data.Column,
                row = data.Row,
                DataArr = ffDataArr,
                tempPos = tempPos,
                CalcuteArr = ffCalculateArr,
            };
            state.Dependency = result.Schedule(state.Dependency);
            state.CompleteDependency();
            //重新赋值
            if (tempPos.Count() != 0) {
                var posArr = new NativeArray<int2>(tempPos.Count() + data.NeedCalculateData.Length,Allocator.TempJob);
                for (int i = 0; i < data.NeedCalculateData.Length; i++)
                    posArr[i] = data.NeedCalculateData[i];
                int currentIndex =  data.NeedCalculateData.Length;
                foreach (var item in tempPos)
                    posArr[currentIndex++] = item;
                data.NeedCalculateData = posArr;
            }

            data.AllMapData = result.DataArr;
            ecb.SetComponent(entity, data);
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        [BurstCompile]
        public partial struct FFUpdateJob : IJobEntity {
            [ReadOnly]public int col;
            [ReadOnly]public int row;
            [ReadOnly]public bool NeedCalcute;
            public NativeArray<FFCellData> DataArr;
            public NativeParallelHashSet<int2> tempPos;
            [ReadOnly]public NativeArray<int2> CalcuteArr;
            //权重
            [BurstCompile]
            public void Execute(ref DynamicBuffer<FFPosBuffer> buffer) {
                if (!(buffer.Length > 0 || NeedCalcute)) return;
                NativeArray<int2> posTemp;
                int currentNum = 0;
                if (!NeedCalcute) {
                    posTemp = new NativeArray<int2>(buffer.Length, Allocator.Temp);
                    foreach (var item in buffer)
                        posTemp[currentNum++] = item.Pos;
                } else {
                    posTemp = new NativeArray<int2>(buffer.Length + CalcuteArr.Length, Allocator.Temp);
                    foreach (var item in buffer)
                        posTemp[currentNum++] = item.Pos;
                    foreach (var item in CalcuteArr)
                        posTemp[currentNum++] = item;
                }
                foreach (var item in posTemp) {
                    tempPos.Add(item);
                    #region dj搜索
                    var pos = item;
                    int lengthBegin = 0;
                    int length = 1;
                    var tempArr = new NativeArray<int2>(col * row ,Allocator.Temp);
                    var searchArr = new NativeArray<bool>(col * row ,Allocator.Temp);
                    tempArr[lengthBegin] = pos;
                    //InitSearchArr(searchArr);
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
                    #endregion
                }
                buffer.Clear();
            }
            [BurstCompile]
            private void InitSearchArr(NativeArray<bool> arr) {
                for (int i = 0; i < arr.Length; i++) arr[i] = false;
            }
            [BurstCompile]
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
            [BurstCompile]
            private int GetIndex(int x, int y, int col) {
                DotsUtility.GetIndexByXY(x, y, col, out var index);
                return index;
            }
        }
    }
}
