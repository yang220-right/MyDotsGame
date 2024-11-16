using UnityEngine;
using NativeQuadTree;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace YY {
    public class QuadFindManager : MonoBehaviour {

    }

    public partial struct QuadFindTurretSystem : ISystem {
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            var dataList = DotsUtility.GetQueryDataArr<BasicAttributeData>(ref state);
            var elements = DotsUtility.GetQuarTreeElements(dataList);
            //重新赋值位置
            for (int i = 0; i < dataList.Length; i++) {
                var data = elements[i];
                data.pos = dataList[i].CurrentPos.xz;
                elements[i] = data;
            }
            var quadTree = new NativeQuadTree<BasicAttributeData>(new AABB2D(0,100));
            //清除并重新排序
            quadTree.ClearAndBulkInsert(elements);
            //自定义查询
            var queryJob = new QuadFindTurretJob
            {
                QuadTree = quadTree,
                Results = new NativeList<QuadElement<BasicAttributeData>>(10, Allocator.TempJob)
            };
            state.Dependency = queryJob.Schedule(state.Dependency);
            state.Dependency.Complete();

            quadTree.Dispose();
            elements.Dispose();
            queryJob.Results.Dispose();
        }
    }
    public partial struct QuadFindTurretJob : IJob {
        public NativeQuadTree<BasicAttributeData> QuadTree;
        public NativeList<QuadElement<BasicAttributeData>> Results;
        [BurstCompile]
        public void Execute() {
            var allList = QuadTree.GetAllTreeElement();
            var tempList = new NativeList<QuadElement<BasicAttributeData>>(Allocator.Temp);
            foreach (var item in allList) {
                var data = item.element;
                QuadTree.RangeQuery(new AABB2D(0, 3), tempList);
                if (tempList.Length <= 0) break;
                Results.AddRange(tempList.AsArray());
                tempList.Clear();
            }
            tempList.Dispose();
        }
    }
}
