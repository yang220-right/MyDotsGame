using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using YY.MainGame;
using static UnityEditor.Rendering.FilterWindow;

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
        //是叶子节点
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
        //查询时自身index
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
        //用于在多线程环境中管理和验证内存访问的安全性。
        //它主要用于确保数据的并发访问不会引发未定义行为或崩溃，特别是在 Unity 的 JobSystem 和 BurstCompiler 中使用。
        AtomicSafetyHandle safetyHandle;
        //用于优化多线程环境中 JobSystem 的调度行为。它的主要作用是帮助 Unity 在作业调度后清理某些引用类型的字段，以避免潜在的错误或无效引用问题。
        //NativeSetClassTypeToNullOnSchedule 是一个 Attribute，用于修饰类的引用字段。
        //当一个作业被调度（Schedule）后，Unity 会自动将带有此属性标记的字段设置为 null。
        [NativeSetClassTypeToNullOnSchedule]
        //DisposeSentinel 是一种调试工具，用于帮助开发者检测未正确释放的 NativeContainer（如 NativeArray、NativeList 等）对象，以防止内存泄漏。
        //它的核心作用是检测内存泄漏，并帮助开发者在调试过程中定位问题
        DisposeSentinel disposeSentinel;
#endif
        // Data
        //在 Unity 的 Job System 中，为了保证多线程代码的安全性，Unity 会对某些不安全的操作（例如直接使用指针）施加限制。
        //默认情况下，如果你在作业（Job）或 NativeContainer 中使用指针，会引发编译错误或警告，因为 Unity 需要确保线程安全和内存安全。
        [NativeDisableUnsafePtrRestriction]
        UnsafeList<QuadElement<T>>* elements;

        [NativeDisableUnsafePtrRestriction]
        UnsafeList<int>* lookup;

        [NativeDisableUnsafePtrRestriction]
        UnsafeList<QuadNode>* nodes;

        int elementsCount;//节点数量
        int maxDepth;//最大深度
        short maxLeafElements;//最大叶子数量

        AABB2D bounds; // NOTE: 当前假设均匀

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

            //用于创建和初始化一个 DisposeSentinel，
            //它帮助开发者在使用原生数据结构（如 NativeArray、NativeList 等）时追踪内存分配和释放的正确性，
            //从而检测潜在的内存泄漏问题。
            DisposeSentinel.Create(out safetyHandle, out disposeSentinel, 1, allocator);
#endif

            // 为每个深度分配内存，所有深度上的节点都存储在一个连续的数组中
            var totalSize = LookupTables.DepthSizeLookup[maxDepth+1];//7:1+2*2+4*4+8*8+16*16+32*32+64*64,
            //分配节点 大小totalSize 分配器allocator  NativeArrayOptions.ClearMemory在分配时清除NativeArray内存。
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
        /// <summary>
        /// 清除并插入
        /// </summary>
        /// <param name="incomingElements"></param>
        public void ClearAndBulkInsert(NativeArray<QuadElement<T>> incomingElements) {
            // 批量插入之前务必清除，否则查找和节点分配需要考虑现有数据。
            Clear();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(safetyHandle);
#endif

            // 如果需要则调整大小
            if (elements->Capacity < elementsCount + incomingElements.Length) {
                //调整二倍大小
                elements->Resize(math.max(incomingElements.Length, elements->Capacity * 2));
            }

            // 准备 Morton 代码 
            var mortonCodes = new NativeArray<int>(incomingElements.Length, Allocator.Temp);
            //深度范围扩展
            var depthExtentsScaling = LookupTables.DepthLookup[maxDepth] / bounds.Extents;
            for (var i = 0; i < incomingElements.Length; i++) {
                var incPos = incomingElements[i].pos;
                //将元素的位置从全局坐标系转换到以边界框中心为原点的局部坐标系。
                incPos -= bounds.Center; // 中心偏移
                //将y轴方向反转，这通常是因为在某些应用中（如图像处理），数组坐标系统的原点在左上角，而通常的空间坐标系统原点在左下角。
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
                    // 此节点的叶子数量增加
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
                    //指针 索引
                    var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, atIndex);
                    if (node.isLeaf) {
                        //我们找到一个叶子，将这个元素添加到其中并移动到下一个元素
                        //指针 索引 value
                        //node.firstchildIndex 当前第一个叶子的索引
                        //node.count 当前叶子数量
                        UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
                        node.count++;
                        //再写入进去
                        UnsafeUtility.WriteArrayElement(nodes->Ptr, atIndex, node);
                        break;
                    }
                    // 没有找到叶子，我们继续深入，直到找到一片
                    atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
                }
            }

            mortonCodes.Dispose();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="depth">当前深度</param>
        /// <param name="mortonCodes">莫顿码</param>
        /// <param name="i">当前线性索引</param>
        /// <param name="atIndex">映射到四叉树的索引</param>
        /// <returns></returns>
        int IncrementIndex(int depth, NativeArray<int> mortonCodes, int i, int atIndex) {
            var atDepth = math.max(0, maxDepth - depth);
            // 向右移位，只取前两位
            int shiftedMortonCode = (mortonCodes[i] >> ((atDepth - 1) * 2)) & 0b11;//0b或者0B表示二进制
            // 所以索引变成......（0,1,2,3）
            atIndex += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
            ++atIndex; // 自动移动一位,因为两位代表一个坐标
            return atIndex;
        }

        void RecursivePrepareLeaves(int prevOffset, int depth) {
            for (int l = 0; l < 4; l++) {
                var at = prevOffset + l * LookupTables.DepthSizeLookup[maxDepth - depth+1];

                var elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);

                if (elementCount > maxLeafElements && depth < maxDepth) {
                    // 此节点上的元素数量超出允​​许范围，因此继续深入
                    RecursivePrepareLeaves(at + 1, depth + 1);
                } else if (elementCount != 0) {
                    //我们要么达到最大深度，要么此节点上的元素少于最大元素数，使其成为叶子节点
                    //将第一个叶子设置为当前的索引值element
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
        public void FilterResult(QueryInfo info, ref NativeList<QuadElement<BasicAttributeData>> results) {
            for (int i = 0; i < results.Length; ++i) {
                var element = results[i];
                var targetData = element.element;
                //需要排除的实体
                bool canRemove = false;
                switch (info.type) {
                    case QueryType.Include:
                        canRemove = !((targetData.Type & info.targetType) == targetData.Type);
                        break;
                    case QueryType.FilterSelf:
                        canRemove = (targetData.Type & info.targetType) == targetData.Type || element.selfIndex == info.selfIndex;
                        break;
                    case QueryType.Filter:
                        canRemove = (targetData.Type & info.targetType) == targetData.Type;
                        break;
                    case QueryType.All:
                        canRemove = false;
                        break;
                }
                if (canRemove) {
                    results.RemoveAt(i);
                    --i;
                }
            }
        }

        #endregion


    }
}
