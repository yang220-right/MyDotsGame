using Unity.Collections;
using YY.MainGame;
using CustomQuadTree;
using Unity.VisualScripting;
using Unity.Burst;
using Unity.Mathematics;

[BurstCompile]
public static partial class DotsUtility {
    /// <summary>
    /// 并没有赋值位置,所以获得数据之后还得赋值位置
    /// </summary>
    /// <returns></returns>
    [BurstCompile]
    public static void GetQuarTreeElements(ref NativeArray<BasicAttributeData> dataList, out NativeArray<QuadElement> elements) {
        elements = new NativeArray<QuadElement>(dataList.Length, Allocator.Temp);
        for (int i = 0; i < dataList.Length; i++) {
            elements[i] = new QuadElement
            {
                element = dataList[i],
                selfIndex = i,
                queryIndex = -1
            };
        }
    }
    [BurstCompile]
    public static bool CompareToBool(this float value, float compareTo) {
        return (value - compareTo) < float.Epsilon;
    }
    [BurstCompile]
    public static void CalculateCubicBezierPoint(in float ct, in float at, in float3 p0, in float3 p1, in float3 p2, out float3 pos) {
        var tempct = ct;
        if (ct > at) {
            tempct = at;
        }
        var t = tempct / at;
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        pos = uu * p0;
        pos += 2 * u * t * p1;
        pos += tt * p2;
    }
    [BurstCompile]
    public static void CalculateCubicBezierPoint(in float t, in float3 p0, in float3 p1, in float3 p2, out float3 p) {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        p = uu * p0;
        p += 2 * u * t * p1;
        p += tt * p2;
    }
    ///<summary>
    ///获取存储贝塞尔曲线点的数组
    ///</summary>
    ///<paramname="startPoint"></param>起始点
    ///<paramname="controlPoint"></param>控制点
    ///<paramname="endPoint"></param>目标点
    ///<paramname="segmentNum"></param>采样点的数量
    ///<returns></returns>存储贝塞尔曲线点的数组
    [BurstCompile]
    public static void GetBeizerList(in float3 startPoint, in float3 controlPoint, in float3 endPoint, in int segmentNum, out NativeArray<float3> path) {
        path = new NativeArray<float3>(segmentNum, Allocator.Temp);
        for (int i = 1; i <= segmentNum; i++) {
            float t = i / (float) segmentNum;
            CalculateCubicBezierPoint(t, startPoint, controlPoint, endPoint, out var pixel);
            path[i - 1] = pixel;
        }
    }

    #region 流场

    [BurstCompile]
    public static void GetIndexByXY(int x, int y, int col, out int index) => index = y * col + x;
    [BurstCompile]
    public static void GetIndexByXY(float x, float y, int col, out int index) => index = ((int)y) * col + ((int)x);
    [BurstCompile]
    public static void GetPosByIndex(int index, int col, out int2 pos) => pos = new int2(index % col, index / col);
    [BurstCompile]
    public static void ToFFPos(in int3 pos, out int2 ffPos) => ffPos = new int2(pos.x + 50, pos.z + 50);
    [BurstCompile]
    public static void ToFFPos(in float3 pos, out int2 ffPos) => ffPos = new int2((int)pos.x + 50, (int)pos.z + 50);
    [BurstCompile]
    public static void ToFFPos(in float2 pos, out int2 ffPos) => ffPos = new int2((int)pos.x + 50, (int)pos.y + 50);
    [BurstCompile]
    public static void ToFFPos(in int2 pos, out int2 ffPos) => ffPos = new int2(pos.x + 50, pos.x + 50);
    [BurstCompile]
    public static void ToPos(in int2 ffPos, out float2 pos) => pos = new float2(ffPos.x - 50, ffPos.y - 50);
    #endregion
}
