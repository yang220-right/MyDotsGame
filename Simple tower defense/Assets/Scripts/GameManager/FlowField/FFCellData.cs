using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace YY.MainGame {
    [ChunkSerializable]
    public partial struct FFControllerData : IComponentData {
        public bool BeginInit;
        public bool EndInit;
        public bool NeedUpdate;//更新
        //地图大小
        public int Row;
        public int Column;
        public NativeArray<FFCellData> allMapData;//地图总数据
    }
    public partial struct FFPosBuffer : IBufferElementData {
        public int2 Pos;
    }

    public partial struct FFCellData {
        public int2 Pos;
        public int X => Pos.x;
        public int Y => Pos.y;
        public int Value;
        public FFCellData(int2 pos,int value = int.MaxValue) {
            Pos = pos;
            Value = value;
        }
    }
}