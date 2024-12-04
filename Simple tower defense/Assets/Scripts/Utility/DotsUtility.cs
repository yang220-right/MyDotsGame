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
    public static void CalculateCubicBezierPoint(float ct, float at, float3 p0, float3 p1, float3 p2, out float3 pos) {
        if (ct > at) {
            ct = at;
        }
        var t = ct / at;
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        pos = uu * p0;
        pos += 2 * u * t * p1;
        pos += tt * p2;
    }

    public static float3 CalculateCubicBezierPoint(float t, float3 p0, float3 p1, float3 p2) {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float3 p = uu * p0;
        p += 2 * u * t * p1;
        p += tt * p2;
        return p;
    }
    ///<summary>
    ///获取存储贝塞尔曲线点的数组
    ///</summary>
    ///<paramname="startPoint"></param>起始点
    ///<paramname="controlPoint"></param>控制点
    ///<paramname="endPoint"></param>目标点
    ///<paramname="segmentNum"></param>采样点的数量
    ///<returns></returns>存储贝塞尔曲线点的数组
    public static float3[] GetBeizerList(float3 startPoint, float3 controlPoint, float3 endPoint, int segmentNum) {
        float3[] path = new float3[segmentNum];
        for (int i = 1; i <= segmentNum; i++) {
            float t = i / (float) segmentNum;
            float3 pixel = CalculateCubicBezierPoint(t, startPoint,
                    controlPoint, endPoint);
            path[i - 1] = pixel;
        }
        return path;
    }
}
