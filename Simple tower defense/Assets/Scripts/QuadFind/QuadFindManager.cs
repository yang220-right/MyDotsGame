using UnityEngine;
using NativeQuadTree;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using static UnityEditor.Progress;
using Unity.Mathematics;
using System;

namespace YY {
    public class QuadFindManager : MonoBehaviour {

    }

    public partial struct QuadFindTurretSystem : ISystem {
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            //查询防御塔
            var query = new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .Build(state.EntityManager);
            var turretQuery = new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithAll<BaseTurretData>()
                .Build(state.EntityManager);

            var dataArr = query.ToComponentDataArray<BasicAttributeData>(Allocator.TempJob);
            var entityArr = query.ToEntityArray(Allocator.TempJob);

            var elements = DotsUtility.GetQuarTreeElements(dataArr);
            //重新赋值位置
            for (int i = 0; i < dataArr.Length; i++) {
                var data = elements[i];
                data.pos = dataArr[i].CurrentPos.xz;
                elements[i] = data;
            }
            var quadTree = new NativeQuadTree<BasicAttributeData>(new AABB2D(0,100));
            //清除并重新排序
            quadTree.ClearAndBulkInsert(elements);
            //自定义查询
            //var queryJob = new QuadFindTurretJob
            //{
            //    QuadTree = quadTree,
            //    Results = new NativeList<QuadElement<BasicAttributeData>>(10, Allocator.TempJob)
            //};
            //state.Dependency = queryJob.Schedule(state.Dependency);
            //state.Dependency.Complete();
            //设置并应用查询的数据
            //var entityArr = query.ToEntityArray(Allocator.Temp);
            //for (int i = 0; i < queryJob.Results.Length; i++) {
            //    var queryIndex = queryJob.Results[i].queryIndex;
            //    var tempData = dataList[queryIndex];
            //    tempData.IsBeAttack = true;
            //    ecb.SetComponent(entityArr[queryIndex], tempData);
            //}

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new TurretQueryEnemyBestNearJob()
            {
                ECB = ecb,
                QuadTree = quadTree,
                AllData = dataArr,
                entityArr = entityArr,

                dataArr = turretQuery.ToComponentDataArray<BasicAttributeData>(Allocator.TempJob),
                queryDataArr = turretQuery.ToComponentDataArray<BaseTurretData>(Allocator.TempJob)
            }.Schedule(state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);

            quadTree.Dispose();
            elements.Dispose();
            //queryJob.Results.Dispose();
            ecb.Dispose();
        }
    }
    [BurstCompile]
    public partial struct QuadFindTurretJob : IJob {
        public NativeQuadTree<BasicAttributeData> QuadTree;
        public NativeList<QuadElement<BasicAttributeData>> Results;
        [BurstCompile]
        public void Execute() {
            var allList = QuadTree.GetAllTreeElement();
            var tempList = new NativeList<QuadElement<BasicAttributeData>>(Allocator.Temp);
            foreach (var item in allList) {
                var data = item.element;
                QuadTree.FilterRangeQuery(QuadTree,
                    new QueryInfo()
                    {
                        type = QueryType.Filter,
                        targetType = DataType.Turret,
                        index = item.selfIndex
                    },
                    new AABB2D(data.CurrentPos.xz, data.CurrentRange),
                    tempList);

                if (tempList.Length <= 0) continue;
                Results.Add(tempList[0]);
                tempList.Clear();
            }
            tempList.Dispose();
        }
    }

    [BurstCompile]
    public partial struct TurretQueryEnemyBestNearJob : IJob {
        //全部实体
        public EntityCommandBuffer ECB;
        public NativeQuadTree<BasicAttributeData> QuadTree;
        public NativeArray<BasicAttributeData> AllData;
        public NativeArray<Entity> entityArr;
        //谁来查
        public NativeArray<BasicAttributeData> dataArr;
        public NativeArray<BaseTurretData> queryDataArr;
        [BurstCompile]
        public void Execute() {
            var tempList = new NativeList<QuadElement<BasicAttributeData>>(Allocator.Temp);

            for (int i = 0; i < dataArr.Length; i++) {
                var data = dataArr[i];
                var queryData = queryDataArr[i];
                //查询条件
                var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentRange);
                QuadTree.FilterRangeQuery(QuadTree,
                        new QueryInfo()
                        {
                            type = QueryType.Include,
                            targetType = DataType.Enemy,
                        },
                        new AABB2D(data.CurrentPos.xz, data.CurrentRange),
                        tempList);
                float minValue = float.MaxValue;
                //执行查询逻辑
                int nearIndex = -1;
                float3 nearPos = 0;
                foreach (var item in tempList) {
                    var dis = math.distancesq(item.element.CurrentPos,data.CurrentPos);
                    if (dis < minValue) {
                        minValue = dis;
                        nearPos = item.element.CurrentPos;
                        nearIndex = item.selfIndex;
                    }
                }
                //执行查询结果,并应用
                if (nearIndex != -1) {
                    var tempData = AllData[nearIndex];
                    tempData.CurrentHP = 444;
                    queryData.NearEnemy = nearPos;
                    ECB.SetComponent(entityArr[nearIndex], tempData);
                }
                tempList.Clear();
            }
            tempList.Dispose();
        }
    }
}
