using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using YY.MainGame;

namespace NativeQuadTree {
    public unsafe partial struct NativeQuadTree<T> where T : unmanaged {
        struct QuadTreeRangeQuery {
            NativeQuadTree<T> tree;

            UnsafeList<T>* fastResults;
            int count;

            AABB2D bounds;

            public void Query(NativeQuadTree<T> tree, AABB2D bounds, NativeList<QuadElement<T>> results) {
                this.tree = tree;
                this.bounds = bounds;
                count = 0;

                // 获取指向内部列表数据的指针以加快写入速度
                fastResults = (UnsafeList<T>*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref results);

                RecursiveRangeQuery(tree.bounds, false, 1, 1);

                fastResults->Length = count;
            }

            public void RecursiveRangeQuery(AABB2D parentBounds, bool parentContained, int prevOffset, int depth) {
                if (count + 4 * tree.maxLeafElements > fastResults->Capacity) {
                    fastResults->Resize(math.max(fastResults->Capacity * 2, count + 4 * tree.maxLeafElements));
                }

                var depthSize = LookupTables.DepthSizeLookup[tree.maxDepth - depth+1];
                for (int l = 0; l < 4; l++) {
                    var childBounds = GetChildBounds(parentBounds, l);

                    var contained = parentContained;
                    if (!contained) {
                        if (bounds.Contains(childBounds)) {
                            contained = true;
                        } else if (!bounds.Intersects(childBounds)) {
                            continue;
                        }
                    }


                    var at = prevOffset + l * depthSize;

                    var elementCount = UnsafeUtility.ReadArrayElement<int>(tree.lookup->Ptr, at);

                    if (elementCount > tree.maxLeafElements && depth < tree.maxDepth) {
                        RecursiveRangeQuery(childBounds, contained, at + 1, depth + 1);
                    } else if (elementCount != 0) {
                        var node = UnsafeUtility.ReadArrayElement<QuadNode>(tree.nodes->Ptr, at);

                        if (contained) {
                            var index = (void*) ((IntPtr) tree.elements->Ptr + node.firstChildIndex * UnsafeUtility.SizeOf<QuadElement<T>>());

                            UnsafeUtility.MemCpy((void*)((IntPtr)fastResults->Ptr + count * UnsafeUtility.SizeOf<QuadElement<T>>()),
                                index, node.count * UnsafeUtility.SizeOf<QuadElement<T>>());
                            count += node.count;
                        } else {
                            for (int k = 0; k < node.count; k++) {
                                var element = UnsafeUtility.ReadArrayElement<QuadElement<T>>(tree.elements->Ptr, node.firstChildIndex + k);
                                if (bounds.Contains(element.pos)) {
                                    UnsafeUtility.WriteArrayElement(fastResults->Ptr, count++, element);
                                }
                            }
                        }
                    }
                }
            }

            static AABB2D GetChildBounds(AABB2D parentBounds, int childZIndex) {
                var half = parentBounds.Extents.x * .5f;

                switch (childZIndex) {
                    case 0: return new AABB2D(new float2(parentBounds.Center.x - half, parentBounds.Center.y + half), half);
                    case 1: return new AABB2D(new float2(parentBounds.Center.x + half, parentBounds.Center.y + half), half);
                    case 2: return new AABB2D(new float2(parentBounds.Center.x - half, parentBounds.Center.y - half), half);
                    case 3: return new AABB2D(new float2(parentBounds.Center.x + half, parentBounds.Center.y - half), half);
                    default: throw new Exception();
                }
            }

            #region 拓展方法 

            NativeQuadTree<BasicAttributeData> newTree;
            UnsafeList<BasicAttributeData>* newFastResults;
            public void Query(QueryInfo queryInfo, NativeQuadTree<BasicAttributeData> tree, AABB2D bounds, NativeList<QuadElement<BasicAttributeData>> results) {
                this.newTree = tree;
                this.bounds = bounds;
                count = 0;

                // 获取指向内部列表数据的指针以加快写入速度
                newFastResults = (UnsafeList<BasicAttributeData>*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref results);
                RecursiveRangeQuery(queryInfo, newTree.bounds, false, 1, 1);
                newFastResults->Length = count;
            }
            public void RecursiveRangeQuery(QueryInfo info, AABB2D parentBounds, bool parentContained, int prevOffset, int depth) {
                if (count + 4 * newTree.maxLeafElements > newFastResults->Capacity) {
                    newFastResults->Resize(math.max(newFastResults->Capacity * 2, count + 4 * newTree.maxLeafElements));
                }

                var depthSize = LookupTables.DepthSizeLookup[newTree.maxDepth - depth+1];
                for (int l = 0; l < 4; l++) {
                    var childBounds = GetChildBounds(parentBounds, l);

                    var contained = parentContained;
                    if (!contained) {
                        if (bounds.Contains(childBounds)) {
                            contained = true;
                        } else if (!bounds.Intersects(childBounds)) {
                            continue;
                        }
                    }

                    var at = prevOffset + l * depthSize;

                    var elementCount = UnsafeUtility.ReadArrayElement<int>(newTree.lookup->Ptr, at);

                    if (elementCount > newTree.maxLeafElements && depth < newTree.maxDepth) {
                        RecursiveRangeQuery(info, childBounds, contained, at + 1, depth + 1);
                    } else if (elementCount != 0) {
                        var node = UnsafeUtility.ReadArrayElement<QuadNode>(newTree.nodes->Ptr, at);

                        if (contained) {
                            var index = (void*) ((IntPtr) newTree.elements->Ptr + node.firstChildIndex * UnsafeUtility.SizeOf<QuadElement<BasicAttributeData>>());

                            UnsafeUtility.MemCpy((void*)((IntPtr)newFastResults->Ptr + count * UnsafeUtility.SizeOf<QuadElement<BasicAttributeData>>()),
                                index, node.count * UnsafeUtility.SizeOf<QuadElement<BasicAttributeData>>());
                            count += node.count;
                        } else {
                            for (int k = 0; k < node.count; k++) {
                                var element = UnsafeUtility.ReadArrayElement<QuadElement<BasicAttributeData>>(newTree.elements->Ptr, node.firstChildIndex + k);
                                var targetData = element.element;
                                if (bounds.Contains(element.pos)) {
                                    bool canAdd = false;
                                    switch (info.type) {
                                        case QueryType.Include:
                                            canAdd = (targetData.Type & info.targetType) == targetData.Type;
                                            break;
                                        case QueryType.FilterSelf:
                                            canAdd = (targetData.Type & info.targetType) != targetData.Type && element.selfIndex != info.selfIndex;
                                            break;
                                        case QueryType.Filter:
                                            canAdd = (targetData.Type & info.targetType) != targetData.Type;
                                            break;
                                        case QueryType.All:
                                            canAdd = true;
                                            break;
                                    }
                                    if (canAdd) {
                                        element.queryIndex = info.selfIndex;
                                        UnsafeUtility.WriteArrayElement(newFastResults->Ptr, count++, element);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            #endregion
        }

    }
}