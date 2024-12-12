using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace YY.MainGame {
    [ChunkSerializable]
    public partial struct FFControllerData : IComponentData {
        public bool BeginInit;
        public bool EndInit;
        public bool NeedUpdate;//更新

        public bool Reset;//重置流场地图
        //地图大小
        public int Column;
        public int Row;
        public NativeArray<int2> NeedCalculateData;//需要计算的数据
        public NativeArray<int2> TempCalculateData;//零时数据 一次计算三个 每次将加入的路径加入
        public NativeArray<FFCellData> AllMapData;//地图总数据
    }
    public partial struct FFPosBuffer : IBufferElementData {
        public int2 Pos;
    }

    public partial struct FFCellData {
        public int2 Pos;
        public int X => Pos.x;
        public int Y => Pos.y;
        public int Value;
        public FFCellData(int2 pos, int value = int.MaxValue) {
            Pos = pos;
            Value = value;
        }
    }
}