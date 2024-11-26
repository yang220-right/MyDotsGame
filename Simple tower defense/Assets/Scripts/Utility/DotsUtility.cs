using Unity.Collections;
using YY.MainGame;
using CustomQuadTree;
using Unity.VisualScripting;

public static partial class DotsUtility {
    /// <summary>
    /// 并没有赋值位置,所以获得数据之后还得赋值位置
    /// </summary>
    /// <returns></returns>
    public static NativeArray<QuadElement> GetQuarTreeElements(NativeArray<BasicAttributeData> dataList) {
        NativeArray<QuadElement> elements = new NativeArray<QuadElement>(dataList.Length, Allocator.Temp);
        for (int i = 0; i < dataList.Length; i++) {
            elements[i] = new QuadElement
            {
                element = dataList[i],
                selfIndex = i,
                queryIndex = -1
            };
        }
        return elements;
    }
    public static bool CompareToBool(this float value, float compareTo) {
        return (value - compareTo) < float.Epsilon;
    }
}
