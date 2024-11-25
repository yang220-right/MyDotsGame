using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using YY.MainGame;
using NativeQuadTree;
using Unity.Burst;

namespace CustomQuadTree {
    public struct QuadElement {
        public float2 pos;
        public BasicAttributeData element;

        //拓展属性
        public int selfIndex;       //自身index
        public int queryIndex;

        /// <summary>
        /// 是否需要过滤掉
        /// </summary>
        /// <param name="queryInfo"></param>
        /// <returns></returns>
        [BurstCompile]
        public bool FilterCheck(QueryInfo queryInfo) {
            bool pass = true;
            switch (queryInfo.type) {
                case QueryType.Include:
                    pass = !((element.Type & queryInfo.targetType) == element.Type);  
                    break;
                case QueryType.FilterSelf:
                    pass = (element.Type & queryInfo.targetType) == element.Type || selfIndex == queryInfo.selfIndex;
                    break;
                case QueryType.Filter:
                    pass = (element.Type & queryInfo.targetType) == element.Type;
                    break;
                case QueryType.All:
                    pass = false;
                    break;
            }
            return pass;
        }
    }

    public unsafe partial struct CustomNativeQuadTree : IDisposable {
        [NativeDisableUnsafePtrRestriction]
        UnsafeList<QuadElement>* elements;
        [NativeDisableUnsafePtrRestriction]
        UnsafeList<int>* lookup;
        [NativeDisableUnsafePtrRestriction]
        UnsafeList<QuadNode>* nodes;

        int elementsCount;
        int maxDepth;
        short maxLeafElements;
        AABB2D bounds;

        public CustomNativeQuadTree(AABB2D bounds, Allocator allocator = Allocator.Temp, int maxDepth = 6, short maxLeafElements = 16,
            int initialElementsCapacity = 256
        ) : this() {
            this.bounds = bounds;
            this.maxDepth = maxDepth;
            this.maxLeafElements = maxLeafElements;
            elementsCount = 0;

            if (maxDepth > 8) {
                throw new InvalidOperationException();
            }

            var totalSize = LookupTables.DepthSizeLookup[maxDepth+1];//7:1+2*2+4*4+8*8+16*16+32*32+64*64,
            lookup = UnsafeList<int>.Create(
                totalSize,
                allocator,
                NativeArrayOptions.ClearMemory);

            nodes = UnsafeList<QuadNode>.Create(
                totalSize,
                allocator,
                NativeArrayOptions.ClearMemory);

            elements = UnsafeList<QuadElement>.Create(
                initialElementsCapacity,
                allocator);
        }

        /// <param name="incomingElements"></param>
        public void ClearAndBulkInsert(NativeArray<QuadElement> incomingElements) {
            Clear();

            if (elements->Capacity < elementsCount + incomingElements.Length) {
                elements->Resize(math.max(incomingElements.Length, elements->Capacity * 2));
            }

            var mortonCodes = new NativeArray<int>(incomingElements.Length, Allocator.Temp);
            var depthExtentsScaling = LookupTables.DepthLookup[maxDepth] / bounds.Extents;
            for (var i = 0; i < incomingElements.Length; i++) {
                var incPos = incomingElements[i].pos;
                incPos -= bounds.Center;
                incPos.y = -incPos.y;
                var pos = (incPos + bounds.Extents) * .5f;
                pos *= depthExtentsScaling;
                mortonCodes[i] = (LookupTables.MortonLookup[(int)pos.x] | (LookupTables.MortonLookup[(int)pos.y] << 1));
            }

            for (var i = 0; i < mortonCodes.Length; i++) {
                int atIndex = 0;
                for (int depth = 0; depth <= maxDepth; depth++) {
                    (*(int*)((IntPtr)lookup->Ptr + atIndex * sizeof(int)))++;
                    atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
                }
            }
            RecursivePrepareLeaves(1, 1);
            for (var i = 0; i < incomingElements.Length; i++) {
                int atIndex = 0;

                for (int depth = 0; depth <= maxDepth; depth++) {
                    var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, atIndex);
                    if (node.isLeaf) {
                        UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
                        node.count++;
                        UnsafeUtility.WriteArrayElement(nodes->Ptr, atIndex, node);
                        break;
                    }
                    atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
                }
            }

            mortonCodes.Dispose();
        }

        int IncrementIndex(int depth, NativeArray<int> mortonCodes, int i, int atIndex) {
            var atDepth = math.max(0, maxDepth - depth);
            int shiftedMortonCode = (mortonCodes[i] >> ((atDepth - 1) * 2)) & 0b11;
            atIndex += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
            ++atIndex;
            return atIndex;
        }
        void RecursivePrepareLeaves(int prevOffset, int depth) {
            for (int l = 0; l < 4; l++) {
                var at = prevOffset + l * LookupTables.DepthSizeLookup[maxDepth - depth+1];

                var elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);

                if (elementCount > maxLeafElements && depth < maxDepth) {
                    RecursivePrepareLeaves(at + 1, depth + 1);
                } else if (elementCount != 0) {
                    var node = new QuadNode {firstChildIndex = elementsCount, count = 0, isLeaf = true };
                    UnsafeUtility.WriteArrayElement(nodes->Ptr, at, node);
                    elementsCount += elementCount;
                }
            }
        }
        /// <summary>
        /// 范围全部查询
        /// </summary>
        public void RangeQuery(AABB2D bounds, NativeList<QuadElement> results) {
            new CustomQuadTreeQuery().InitTree(this).Q(bounds, results, new QueryInfo { type = QueryType.All });
        }

        public void Clear() {
            UnsafeUtility.MemClear(lookup->Ptr, lookup->Capacity * UnsafeUtility.SizeOf<int>());
            UnsafeUtility.MemClear(nodes->Ptr, nodes->Capacity * UnsafeUtility.SizeOf<QuadNode>());
            UnsafeUtility.MemClear(elements->Ptr, elements->Capacity * UnsafeUtility.SizeOf<QuadElement>());
            elementsCount = 0;
        }

        public void Dispose() {
            UnsafeList<QuadElement>.Destroy(elements);
            elements = null;
            UnsafeList<int>.Destroy(lookup);
            lookup = null;
            UnsafeList<QuadNode>.Destroy(nodes);
            nodes = null;
        }

        #region 拓展方法

        public unsafe QuadElement GetTreeElemenetByIndex(int index) {
            if (index < elementsCount) {
                return (*elements)[index];
            }
            throw new IndexOutOfRangeException();
        }
        public void Q(AABB2D bounds, NativeList<QuadElement> results, QueryInfo info) {
            new CustomQuadTreeQuery().InitTree(this).Q(bounds, results, info);
        }

        #endregion
    }
}