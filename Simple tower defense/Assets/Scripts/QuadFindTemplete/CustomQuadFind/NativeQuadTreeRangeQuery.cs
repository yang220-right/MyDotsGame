using NativeQuadTree;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using YY.MainGame;
using Unity.Burst;
using Unity.Entities.UniversalDelegates;
using static UnityEngine.GraphicsBuffer;

namespace CustomQuadTree {
    #region 拓展数据
    /// <summary>
    /// 查询类型
    /// </summary>
    public enum QueryType {
        Include,            //多目标查询
        FilterSelf,    //查询某一目标并排除自己
        Filter,             //过滤除了target的其他目标
        All,                //全部查询
    }
    /// <summary>
    /// 查询信息
    /// </summary>
    public partial struct QueryInfo {
        public QueryType type;
        //查询目标
        public DataType targetType;
        //查询时自身index
        public int selfIndex;
    }
    #endregion
    public unsafe partial struct CustomNativeQuadTree {
        [BurstCompile]
        public struct CustomQuadTreeQuery : IDisposable {
            CustomNativeQuadTree tree;
            UnsafeList<BasicAttributeData>* fastResults;
            int count;
            AABB2D bounds;
            QueryInfo info;
            public CustomQuadTreeQuery InitTree(CustomNativeQuadTree tree) {
                this.tree = tree;
                return this;
            }
            /// <summary>
            /// 时刻要记住,查的是全部!!!
            /// 所以如果数组超出,一定是容量不够,得扩大容量
            /// </summary>
            [BurstCompile]
            public void Q(AABB2D bounds, NativeList<QuadElement> results, QueryInfo info) {
                //初始化值
                this.bounds = bounds;
                count = 0;
                this.info = info;
                fastResults = (UnsafeList<BasicAttributeData>*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref results);
                //开始查询
                Query();
            }
            [BurstCompile]
            public void Query() {
                RecursiveRangeQuery(tree.bounds, false, 1, 1);
                fastResults->Length = count;
            }
            [BurstCompile]
            public void RecursiveRangeQuery(AABB2D parentBounds, bool parentContained, int prevOffset, int depth) {
                if (count + 4 * tree.maxLeafElements > fastResults->Capacity)
                    fastResults->Resize(math.max(fastResults->Capacity * 2, count + 4 * tree.maxLeafElements));
                var depthSize = LookupTables.DepthSizeLookup[tree.maxDepth - depth+1];
                for (int l = 0; l < 4; l++) {
                    var childBounds = GetChildBounds(parentBounds, l);
                    var contained = parentContained;
                    if (!contained) {
                        if (bounds.Contains(childBounds))
                            contained = true;
                        else if (!bounds.Intersects(childBounds))
                            continue;
                    }
                    var at = prevOffset + l * depthSize;
                    var elementCount = UnsafeUtility.ReadArrayElement<int>(tree.lookup->Ptr, at);
                    if (elementCount > tree.maxLeafElements && depth < tree.maxDepth) {
                        RecursiveRangeQuery(childBounds, contained, at + 1, depth + 1);
                    } else if (elementCount != 0) {
                        var node = UnsafeUtility.ReadArrayElement<QuadNode>(tree.nodes->Ptr, at);
                        if (contained) {
                            if(info.type == QueryType.All) {
                                //更快查询
                                var index = (void*) ((IntPtr) tree.elements->Ptr + node.firstChildIndex * UnsafeUtility.SizeOf<QuadElement>());
                                UnsafeUtility.MemCpy((void*)((IntPtr)fastResults->Ptr + count * UnsafeUtility.SizeOf<QuadElement>()),
                                    index, node.count * UnsafeUtility.SizeOf<QuadElement>());
                                count += node.count;
                            } else {
                                //这里使用过滤
                                for (int k = 0; k < node.count; k++) {
                                    var element = UnsafeUtility.ReadArrayElement<QuadElement>(tree.elements->Ptr, node.firstChildIndex + k);
                                    if (!element.FilterCheck(info)) {
                                        UnsafeUtility.WriteArrayElement(fastResults->Ptr, count++, element);
                                    }
                                }
                            }
                        } else {
                            for (int k = 0; k < node.count; k++) {
                                var element = UnsafeUtility.ReadArrayElement<QuadElement>(tree.elements->Ptr, node.firstChildIndex + k);
                                if (bounds.Contains(element.pos) && !element.FilterCheck(info)) {
                                    UnsafeUtility.WriteArrayElement(fastResults->Ptr, count++, element);
                                }
                            }
                        }
                    }
                }
            }
            [BurstCompile]
            AABB2D GetChildBounds(AABB2D parentBounds, int childZIndex) {
                var half = parentBounds.Extents.x * .5f;
                switch (childZIndex) {
                    case 0: return new AABB2D(new float2(parentBounds.Center.x - half, parentBounds.Center.y + half), half);
                    case 1: return new AABB2D(new float2(parentBounds.Center.x + half, parentBounds.Center.y + half), half);
                    case 2: return new AABB2D(new float2(parentBounds.Center.x - half, parentBounds.Center.y - half), half);
                    case 3: return new AABB2D(new float2(parentBounds.Center.x + half, parentBounds.Center.y - half), half);
                    default: throw new Exception();
                }
            }
            public void Dispose() {
                tree.Dispose();
                fastResults = null;
            }
        }
    }
}
