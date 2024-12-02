using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using YY.MainGame;
using NativeQuadTree;
using Unity.Burst;
using YY.Turret;

namespace CustomQuadTree {
    [BurstCompile]
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
        public bool CheckPass(QueryInfo queryInfo, int currentNum = 0) {
            bool pass = false;
            switch (queryInfo.type) {
                case QueryType.Include:
                    pass = (element.Type & queryInfo.targetType) == element.Type;
                    break;
                case QueryType.FilterSelf:
                    pass = !((element.Type & queryInfo.targetType) == element.Type || selfIndex == queryInfo.selfIndex);
                    break;
                case QueryType.Filter:
                    pass = !((element.Type & queryInfo.targetType) == element.Type);
                    break;
                case QueryType.All:
                    pass = true;
                    break;
            }
            //如果没有符合的,直接返回false
            if (!pass) return false;
            //单个查询直接返回
            if (queryInfo.AttackType == AttackRangeType.Single)
                return pass;
            //检查是否符合范围 大于直接pass
            if (math.distancesq(element.CurrentPos.xz, queryInfo.Pos.xz) > queryInfo.AttackCircle * queryInfo.AttackCircle)
                return false;
            if (queryInfo.AttackType == AttackRangeType.Fans) {
                //以下为扇形查询
                //检查是否在范围内
                //math.degrees 弧度转角度
                //math.radians 角度转弧度
                var deg = math.radians(queryInfo.AttackRange / 2 );//一半的弧度
                var dir = math.normalize(element.CurrentPos.xz - queryInfo.Pos.xz);
                var dotValue = math.dot(queryInfo.CurrentAttackDir,dir);
                var dotDeg = math.acos(dotValue);//转弧度了
                if (deg >= dotDeg) return true;
            }
            //检查是否最大人数 
            //if (queryInfo.MaxNum <= 0) return pass;//注释掉,不用检查,因为下一行代码会狠狠检查
            if (queryInfo.MaxNum >= currentNum) return true;

            return false;
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

        public CustomNativeQuadTree(AABB2D bounds,
            Allocator allocator = Allocator.Temp,
            int maxDepth = 6,
            short maxLeafElements = 16,
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
        [BurstCompile]
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

        [BurstCompile]
        int IncrementIndex(int depth, NativeArray<int> mortonCodes, int i, int atIndex) {
            var atDepth = math.max(0, maxDepth - depth);
            int shiftedMortonCode = (mortonCodes[i] >> ((atDepth - 1) * 2)) & 0b11;
            atIndex += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
            ++atIndex;
            return atIndex;
        }
        [BurstCompile]
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
        [BurstCompile]
        public void RangeQuery(AABB2D bounds, NativeList<QuadElement> results) {
            new CustomQuadTreeQuery().InitTree(this).Q(bounds, results, new QueryInfo { type = QueryType.All });
        }
        [BurstCompile]
        public void Clear() {
            UnsafeUtility.MemClear(lookup->Ptr, lookup->Capacity * UnsafeUtility.SizeOf<int>());
            UnsafeUtility.MemClear(nodes->Ptr, nodes->Capacity * UnsafeUtility.SizeOf<QuadNode>());
            UnsafeUtility.MemClear(elements->Ptr, elements->Capacity * UnsafeUtility.SizeOf<QuadElement>());
            elementsCount = 0;
        }
        [BurstCompile]
        public void Dispose() {
            UnsafeList<QuadElement>.Destroy(elements);
            elements = null;
            UnsafeList<int>.Destroy(lookup);
            lookup = null;
            UnsafeList<QuadNode>.Destroy(nodes);
            nodes = null;
        }

        #region 拓展方法
        [BurstCompile]
        public unsafe QuadElement GetTreeElemenetByIndex(int index) {
            if (index < elementsCount) {
                return (*elements)[index];
            }
            throw new IndexOutOfRangeException();
        }
        [BurstCompile]
        public void Q(AABB2D bounds, NativeList<QuadElement> results, QueryInfo info) {
            new CustomQuadTreeQuery().InitTree(this).Q(bounds, results, info);
        }

        #endregion
    }
}