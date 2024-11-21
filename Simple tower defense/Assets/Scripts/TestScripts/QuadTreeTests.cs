using NativeQuadTree;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

enum testType {
    noe, tow, tree
}
struct testData {
    public int a;
    public int hp;
    public testType type;
}

public class QuadTreeTests : MonoBehaviour {
    public TestGScriptes go;
    AABB2D Bounds => new AABB2D(0, 50);

    public void Update() {
        if (Input.GetKeyDown(KeyCode.A)) {
            testDatas.Clear();
            testDatas.Capacity = go.x * go.y;
            for (int i = 0; i < go.x; i++)
                for (int j = 0; j < go.y; j++) {
                    //go.pos1 = new Vector2(math.lerp(0, 50, (float)i / go.x), math.lerp(0, 50, (float)j / go.y));
                    //go.pos2 = new Vector2(math.lerp(0, -50, (float)i / go.x), math.lerp(0, -50, (float)j / go.y));
                    //go.pos3 = new Vector2(math.lerp(0, -50, (float)i / go.x), math.lerp(0, 50, (float)j / go.y));
                    //go.pos4 = new Vector2(math.lerp(0, 50, (float)i / go.x), math.lerp(0, -50, (float)j / go.y));
                    //go.pos5 = new Vector2(0, math.lerp(0, 50, (float)j / go.y));
                    //testDatas.Add(new Vector2(math.lerp(0, 50, (float)i / go.x), math.lerp(0, -50, (float)j / go.y)));
                    testDatas.Add(new Vector2(math.lerp(0, 1, (float)i / go.x), math.lerp(0, 1, (float)j / go.y)));
                    //testDatas.Add(new float2(0, 49));

                }

            TestMorton();
        }
    }
    NativeList<float2> testDatas = new NativeList<float2>(Allocator.Persistent);
    public unsafe void TestMorton() {
        ClearLog();

        var morNums = new List<int>(LookupTables.MortonLookup.Length);
        for (int i = 0; i < LookupTables.MortonLookup.Length; i++) {
            morNums.Add(LookupTables.MortonLookup[i]);
        }
        for (int i = 0; i < morNums.Count; i += 8) {
            Debug.Log($"{(morNums[i])} {(morNums[i + 1])} {(morNums[i + 2])} {(morNums[i + 3])} {(morNums[i + 4])} {(morNums[i + 5])} {(morNums[i + 6])} {(morNums[i + 7])} ");
        }
        var incomingElements = new NativeArray<QuadElement<testData>>(go.x * go.y,Allocator.Temp);
        for (int i = 0; i < testDatas.Length; i++) {
            incomingElements[i] = new QuadElement<testData>() { pos = go.pos1, element = new testData { a = 1, hp = i * 123, type = testType.noe } };
        }
        //incomingElements[0] = new QuadElement<testData>() { pos = go.pos1, element = new testData { a = 1, hp = 123, type = testType.noe } };
        //incomingElements[1] = new QuadElement<testData>() { pos = go.pos2, element = new testData { a = 2, hp = 15, type = testType.tow } };
        //incomingElements[2] = new QuadElement<testData>() { pos = go.pos3, element = new testData { a = 3, hp = 1, type = testType.tow } };
        //incomingElements[3] = new QuadElement<testData>() { pos = go.pos4, element = new testData { a = 4, hp = 110, type = testType.tree } };
        //incomingElements[4] = new QuadElement<testData>() { pos = go.pos5, element = new testData { a = 5, hp = 120, type = testType.tree } };
        var mortonCodes = new NativeArray<int>(incomingElements.Length, Allocator.Temp);

        //Debug.Log("交织莫顿码");
        var depthExtentsScaling = LookupTables.DepthLookup[6] / Bounds.Extents;
        for (int i = 0; i < incomingElements.Length; i++) {
            var pos = new float2(incomingElements[i].pos.x + Bounds.Extents.x, Bounds.Center.y + incomingElements[i].pos.y + Bounds.Extents.y) * 0.5f * depthExtentsScaling;
            var x = incomingElements[i].pos.x;
            var mx = LookupTables.MortonLookup[(int)pos.x];
            var y = incomingElements[i].pos.y;
            var my = LookupTables.MortonLookup[(int)pos.y];
            //Debug.Log($"{x}_{mx},{y}_{my}点 ");
            mortonCodes[i] = (mx | (my << 1));
        }

        var totalSize = LookupTables.DepthSizeLookup[6+1];
        UnsafeList<int>* lookup = UnsafeList<int>.Create(
                totalSize,
                Allocator.Temp,
                NativeArrayOptions.ClearMemory); ;
        UnsafeList<QuadNode>* nodes =  UnsafeList<QuadNode>.Create(
                totalSize,
                Allocator.Temp,
                NativeArrayOptions.ClearMemory);
        UnsafeList<QuadElement<testData>>* elements = UnsafeList<QuadElement<testData>>.Create(
                256,
                Allocator.Temp);
        short maxLeafElements = 16;//最大叶子数量
        int elementsCount = 0;//节点数量

        // 索引每个节点的子元素总数（总计，因此父节点的计数包括子节点的计数）
        for (var i = 0; i < mortonCodes.Length; i++) {
            int atIndex = 0;
            for (int depth = 0; depth <= 6; depth++) {
                // 此节点的叶子数量增加
                (*(int*)((IntPtr)lookup->Ptr + atIndex * sizeof(int)))++;
                atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
                //Debug.Log($"节点总数:{mortonCodes[i]} atIndex {atIndex}");
            }
        }
        elements->Capacity = (int)(go.x * go.y - 1);
        RecursivePrepareLeaves(1, 1);
        // 将元素添加到叶节点
        for (var i = 0; i < incomingElements.Length; i++) {
            int atIndex = 0;
            for (int depth = 0; depth <= 6; depth++) {
                //指针 索引
                var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, atIndex);
                if (node.isLeaf) {
                    //我们找到一个叶子，将这个元素添加到其中并移动到下一个元素
                    //指针 索引 value
                    //node.firstchildIndex 当前第一个叶子的索引
                    //node.count 当前叶子数量
                    UnsafeUtility.WriteArrayElement(elements->Ptr, node.firstChildIndex + node.count, incomingElements[i]);
                    //Debug.Log($"写入元素:位置{node.firstChildIndex + node.count}元素:{incomingElements[i].pos}");
                    node.count++;
                    //再写入进去
                    UnsafeUtility.WriteArrayElement(nodes->Ptr, atIndex, node);
                    break;
                }
                // 没有找到叶子，我们继续深入，直到找到一片
                atIndex = IncrementIndex(depth, mortonCodes, i, atIndex);
                //Debug.Log($"添加叶子节点:深入叶子{atIndex}");
            }
        }
        //开始查询
        int count = 0;
        NativeList<QuadElement<testData>> resultList = new NativeList<QuadElement<testData>>(Allocator.Temp);
        UnsafeList<QuadElement<testData>>* fastResults = (UnsafeList<QuadElement<testData>>*)NativeListUnsafeUtility.GetInternalListDataPtrUnchecked(ref resultList);
        RecursiveRangeQuery(Bounds, false, 1, 1);
        fastResults->Length = count;


        int ttindex = 0;
        foreach (var item in resultList) {
            if (item.element.a > 10) {
                Debug.Log($"pos:{item.pos} 当前temp的Index{ttindex} 数据{item.element.a}-{item.element.hp}-{item.element.type}");
            }
            ttindex++;
            //Debug.Log($"pos:{item.pos} 数据{item.element.a}-{item.element.hp}-{item.element.type}");
        }

        void RecursiveRangeQuery(AABB2D parentBounds, bool parentContained, int prevOffset, int depth) {
            if (count + 4 * maxLeafElements > fastResults->Capacity) {
                fastResults->Resize(math.max(fastResults->Capacity * 2, count + 4 * maxLeafElements));
            }

            var depthSize = LookupTables.DepthSizeLookup[6 - depth+1];
            //四个块查询
            for (int l = 0; l < 4; l++) {
                var childBounds = GetChildBounds(parentBounds, l);

                var contained = parentContained;
                if (!contained) {
                    if (Bounds.Contains(childBounds)) {
                        contained = true;
                    } else if (!Bounds.Intersects(childBounds)) {
                        continue;
                    }
                }


                var at = prevOffset + l * depthSize;

                var elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);

                if (elementCount > maxLeafElements && depth < 6) {
                    RecursiveRangeQuery(childBounds, contained, at + 1, depth + 1);
                } else if (elementCount != 0) {
                    var node = UnsafeUtility.ReadArrayElement<QuadNode>(nodes->Ptr, at);
                    //if(elementCount < maxLeafElements) {
                    //    for (int k = 0; k < node.count; k++) {
                    //        var element = UnsafeUtility.ReadArrayElement<QuadElement<int>>(elements->Ptr, node.firstChildIndex + k);
                    //        if (Bounds.Contains(element.pos)) {
                    //            UnsafeUtility.WriteArrayElement(fastResults->Ptr, count++, element);
                    //        }
                    //    }
                    //} else {
                    //压边界情况 如果最后一层,还是包含,则说明压边界
                    if (contained && depth != 6) {
                        //当前索引
                        var index = (void*) ((IntPtr) elements->Ptr + node.firstChildIndex * UnsafeUtility.SizeOf<QuadElement<testData>>());
                        //copy  从index复制node.count个数据给fastResults
                        UnsafeUtility.MemCpy((void*)((IntPtr)fastResults->Ptr + count * UnsafeUtility.SizeOf<QuadElement<testData>>()),
                            index, node.count * UnsafeUtility.SizeOf<QuadElement<testData>>());
                        count += node.count;
                        for (int i = 0; i < node.count; i++) {
                            var tempE = *(fastResults->Ptr + i * UnsafeUtility.SizeOf<QuadElement<testData>>());
                        }
                    } else if (!contained || depth == 6 || elementCount < maxLeafElements) {
                        for (int k = 0; k < node.count; k++) {
                            //var element = UnsafeUtility.ReadArrayElement<QuadElement<testData>>(elements->Ptr, node.firstChildIndex + k);
                            var element = *(elements->Ptr + (node.firstChildIndex + k));
                            if (Bounds.Contains(element.pos)) {
                                Debug.Log($"下面data element1  a - {element.element.a} hp - {element.element.hp}");
                                if (element.element.a != 1) {
                                    Debug.Log($"下面当前K{k}");
                                }
                                //UnsafeUtility.WriteArrayElement(fastResults->Ptr, count++, element);
                                *(fastResults->Ptr + count) = element; ++count;
                                var tempE = *(fastResults->Ptr+(count-1));
                                if (tempE.element.a != 1) {
                                    Debug.Log($"下面data 当前K{k}");
                                }
                                Debug.Log($"下面data a - {tempE.element.a} hp - {tempE.element.hp}");
                                Debug.Log($"下面data element2  a - {element.element.a} hp - {element.element.hp}");

                                if (tempE.element.a > 100) {
                                    Debug.Log($"下面当前node.cout{node.count} node.firstChildIndex{node.firstChildIndex} k{k}");
                                }
                            }
                        }
                    }
                    //}
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



        void RecursivePrepareLeaves(int prevOffset, int depth) {
            for (int l = 0; l < 4; l++) {
                var at = prevOffset + l * LookupTables.DepthSizeLookup[6 - depth+1];

                var elementCount = UnsafeUtility.ReadArrayElement<int>(lookup->Ptr, at);

                if (elementCount > maxLeafElements && depth < 6) {
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
        int IncrementIndex(int depth, NativeArray<int> mortonCodes, int i, int atIndex) {
            var atDepth = math.max(0, 6 - depth);
            // 向右移位，只取前两位
            int shiftedMortonCode = (mortonCodes[i] >> ((atDepth - 1) * 2)) & 0b11;//0b或者0B表示二进制
                                                                                   // 所以索引变成......（0,1,2,3）
            atIndex += LookupTables.DepthSizeLookup[atDepth] * shiftedMortonCode;
            ++atIndex; // 自动移动一位,因为两位代表一个坐标
            return atIndex;
        }
    }
    public void ClearLog() {
        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.SceneView));
        System.Type logEntries = assembly.GetType("UnityEditor.LogEntries");
        System.Reflection.MethodInfo clearConsoleMethod = logEntries.GetMethod("Clear");
        clearConsoleMethod.Invoke(new object(), null);
    }
}
