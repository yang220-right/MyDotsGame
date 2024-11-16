using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace NativeQuadTree {
    /// <summary>
    /// 原生四叉树作业示例
    /// </summary>
    public static class QuadTreeJobs {
        /// <summary>
        /// 批量将多个项目插入树中
        /// </summary>
        [BurstCompile]
        public struct AddBulkJob<T> : IJob where T : unmanaged {
            [ReadOnly]
            public NativeArray<QuadElement<T>> Elements;

            public NativeQuadTree<T> QuadTree;

            public void Execute() {
                QuadTree.ClearAndBulkInsert(Elements);
            }
        }

        /// <summary>
        /// 关于如何进行范围查询的示例，最好自己编写并批量执行多个查询
        /// </summary>
        [BurstCompile]
        public struct RangeQueryJob<T> : IJob where T : unmanaged {
            [ReadOnly]
            public AABB2D Bounds;

            [ReadOnly]
            public NativeQuadTree<T> QuadTree;

            public NativeList<QuadElement<T>> Results;

            public void Execute() {
                for (int i = 0; i < 1000; i++) {
                    QuadTree.RangeQuery(Bounds, Results);
                    Results.Clear();
                }
                QuadTree.RangeQuery(Bounds, Results);
            }
        }
    }
}