using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using YY.MainGame;

namespace NativeQuadTree {
    // 表示四叉树中的一个元素节点。
    // 注意,pos必须要小于树的范围,否则报错
    public struct QuadElement<T> where T : unmanaged {
        public float2 pos;
        public T element;

        //拓展属性
        public int selfIndex;       //自身index
        public int queryIndex;
    }

    struct QuadNode {
        // 指向此节点在元素中的第一个子节点索引
        public int firstChildIndex;

        // 叶子中的元素数量
        public short count;
        public bool isLeaf;
    }


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
        public int selfIndex;
    }

    #endregion

    /// <summary>
    /// 四叉树旨在与 Burst 一起使用，支持快速批量插入和查询。
    ///
    /// TODO:
    /// - 更好的测试覆盖率
    /// - 自动深度/边界/最大叶元素计算
    /// </summary>
    public unsafe partial struct NativeQuadTree<T> : IDisposable where T : unmanaged {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        // Safety
        AtomicSafetyHandle safetyHandle;
        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel disposeSentinel;
#endif
        // Data
        [NativeDisableUnsafePtrRestriction]
        UnsafeList<QuadElement<T>>* elements;

        [NativeDisableUnsafePtrRestriction]
        UnsafeList<int>* lookup;

        [NativeDisableUnsafePtrRestriction]
        UnsafeList<QuadNode>* nodes;

        int elementsCount;

        int maxDepth;
        short maxLeafElements;

        AABB2D bounds; // NOTE: Currently assuming uniform

        /// <summary>
        /// 创建一个新的四叉树。
        /// - 确保边界不会比需要的大很多，否则桶会非常偏离。最好计算边界
        /// - 深度越高，开销越大，尤其是在深度为 7/8 时开销会更大
        /// </summary>
        public NativeQuadTree(AABB2D bounds, Allocator allocator = Allocator.Temp, int maxDepth = 6, short maxLeafElements = 16,
            int initialElementsCapacity = 256
        ) : this() {
            this.bounds = bounds;
            this.maxDepth = maxDepth;
            this.maxLeafElements = maxLeafElements;
            elementsCount = 0;

            if (maxDepth > 8) {
                // 目前不支持更高的深度，Morton 代码查找表必须支持它
                throw new InvalidOperationException();
            }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // TODO：找出最新实体中与此等同的内容
            // CollectionHelper.CheckIsUnmanaged<T>();
            DisposeSentinel.Create(out safetyHandle, out disposeSentinel, 1, allocator);
#endif

            // 为每个深度分配内存，所有深度上的节点都存储在一个连续的数组中
            var totalSize = LookupTables.DepthSizeLookup[maxDepth+1];

            lookup = UnsafeList<int>.Create(
                totalSize,
                allocator,
                NativeArrayOptions.ClearMemory);

            nodes = UnsafeList<QuadNode>.Create(
                totalSize,
                allocator,
                NativeArrayOptions.ClearMemory);

            elements = UnsafeList<QuadElement<T>>.Create(
                initialElementsCapacity,
                allocator);
        }

        public void ClearAndBulkInsert(NativeArray<QuadElement<T>> incomingElements) {
            // 批量插入之前务必清除，否则查找和节点分配需要考虑 
            // 现有数据。
            Clear();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(safetyHandle);
#endif

            // 如果需要则调整大小
            if (elements->Capacity < elementsCount + incomingElements.Length) {
                elements->Resize(math.max(incomingElements.Length, elements->Capacity * 2));
            }

            // 准备 Morton 代码
            var mortonCodes = new NativeArray<int>(incomingElements.Length, Allocator.Temp);
            var depthExtentsScaling = LookupTables.DepthLookup[maxDepth] / bounds.Extents;
            for (var i = 0; i < incomingElements.Length; i++) {
                var incPos = incomingElements[i].pos;
                incPos -= bounds.Center; // 中心偏移
                incPos.y = -incPos.y; // World -> array
                var pos = (incPos + bounds.Extents) * .5f; // 积极
                                                           // 现在扩展到属于深度的可用空间
                pos *= depthExtentsScaling;
                // 并交织莫顿代码的比特
                mortonCodes[i] = (LookupTables.MortonLookup[(int)pos.x] | (LookupTables.MortonLookup[(int)pos.y] << 1));
            }

            // 索引每个节点的子元素总数（总计，因此父节点的计数包括子节点的计数）
            for (var i = 0; i < mortonCodes.Length; i++) {
                int atIndex = 0;
                for (int depth = 0; depth <= maxDepth; depth++) {
                    // 增加此元素所包含的深度节点
                    (*(int*)((IntPtr)lookup->Ptr + atIndex * sizeof(int)))++;
                    atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
                }
            }

            // 准备树叶节点
            RecursivePrepareLeaves(1, 1);

            // 将元素添加到叶节点
            for (var i = 0; i < incomingElements.Length; i++) {
                int atIndex = 0;

                for (int depth = 0; depth <= maxDepth; depth++) {
                    var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, atIndex);
                    if (node.isLeaf) {
                        //我们找到一个叶子，将这个元素添加到其中并移动到下一个元素
                        UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
                        node.count++;
                        UnsafeUtility.WriteArrayElement(nodes->Ptr, atIndex, node);
                        break;
                    }
                    // 没有找到叶子，我们继续深入，直到找到一片
                    atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
                }
            }

            mortonCodes.Dispose();
        }

        int IncrementIndex(int depth, NativeArray<int> mortonCodes, int i, int atIndex) {
            var atDepth = math.max(0, maxDepth - depth);
            // 向右移位，只取前两位
            int shiftedMortonCode = (mortonCodes[i] >> ((atDepth - 1) * 2)) & 0b11;
            // 所以索引变成......（0,1,2,3）
            atIndex += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
            atIndex++; // offset for self
            return atIndex;
        }

        void RecursivePrepareLeaves(int prevOffset, int depth) {
            for (int l = 0; l < 4; l++) {
                var at = prevOffset + l * LookupTables.DepthSizeLookup[maxDepth - depth+1];

                var elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);

                if (elementCount > maxLeafElements && depth < maxDepth) {
                    // 此节点上的元素数量超出允​​许范围，因此请继续深入
                    RecursivePrepareLeaves(at + 1, depth + 1);
                } else if (elementCount != 0) {
                    //我们要么达到最大深度，要么此节点上的元素少于最大元素数，使其成为叶子节点
                    var node = new QuadNode {firstChildIndex = elementsCount, count = 0, isLeaf = true };
                    UnsafeUtility.WriteArrayElement(nodes->Ptr, at, node);
                    elementsCount += elementCount;
                }
            }
        }

        public void RangeQuery(AABB2D bounds, NativeList<QuadElement<T>> results) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(safetyHandle);
#endif
            new QuadTreeRangeQuery().Query(this, bounds, results);
        }

        public void Clear() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(safetyHandle);
#endif
            UnsafeUtility.MemClear(lookup->Ptr, lookup->Capacity * UnsafeUtility.SizeOf<int>());
            UnsafeUtility.MemClear(nodes->Ptr, nodes->Capacity * UnsafeUtility.SizeOf<QuadNode>());
            UnsafeUtility.MemClear(elements->Ptr, elements->Capacity * UnsafeUtility.SizeOf<QuadElement<T>>());
            elementsCount = 0;
        }

        public void Dispose() {
            UnsafeList<QuadElement<T>>.Destroy(elements);
            elements = null;
            UnsafeList<int>.Destroy(lookup);
            lookup = null;
            UnsafeList<QuadNode>.Destroy(nodes);
            nodes = null;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            DisposeSentinel.Dispose(ref safetyHandle, ref disposeSentinel);
#endif
        }


        #region 拓展方法

        /// <summary>
        /// 返回只读的数据,无法修改
        /// </summary>
        /// <returns></returns>
        public unsafe NativeArray<QuadElement<T>> GetAllTreeElement() {
            NativeArray<QuadElement<T>> nativeArray = new NativeArray<QuadElement<T>>(elementsCount, Allocator.Temp);
            for (int i = 0; i < elementsCount; i++) {
                nativeArray[i] = UnsafeUtility.ReadArrayElement<QuadElement<T>>(elements->Ptr, i);
            }
            return nativeArray;

            //根据数组指针转NativeArray
            //return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<QuadElement<T>>
            //    (elements, elementsCount, Allocator.Temp);
        }
        public unsafe QuadElement<T> GetTreeElemenetByIndex(int index) {
            if (index < elementsCount) {
                return (*elements)[index];
            }
            //直接报错
            throw new IndexOutOfRangeException();
        }

        public void FilterRangeQuery(NativeQuadTree<BasicAttributeData> tree, QueryInfo queryInfo, AABB2D bounds, NativeList<QuadElement<BasicAttributeData>> results) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(safetyHandle);
#endif
            new QuadTreeRangeQuery().Query(queryInfo, tree, bounds, results);
        }

        #endregion


    }
}
