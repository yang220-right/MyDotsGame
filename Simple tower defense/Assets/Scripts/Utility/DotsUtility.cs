using NativeQuadTree;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public partial struct DotsUtility {
    public static NativeArray<T> GetQueryDataArr<T>(ref SystemState state) where T : unmanaged, IComponentData {
        var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>()
                .Build(state.EntityManager);
        return query.ToComponentDataArray<T>(Allocator.Temp);
    }
    /// <summary>
    /// 并没有赋值位置,所以获得数据之后还得赋值位置
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="dataList"></param>
    /// <returns></returns>
    public static NativeArray<QuadElement<T>> GetQuarTreeElements<T>(NativeArray<T> dataList) where T : unmanaged, IComponentData {
        NativeArray<QuadElement<T>> elements = new NativeArray<QuadElement<T>>(dataList.Length, Allocator.Temp);
        for (int i = 0; i < dataList.Length; i++) {
            elements[i] = new QuadElement<T>
            {
                element = dataList[i],
                selfIndex = i,
                queryIndex = -1
            };
        }
        return elements;
    }
}
