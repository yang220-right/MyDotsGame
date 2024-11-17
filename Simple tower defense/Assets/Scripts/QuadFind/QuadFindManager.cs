using UnityEngine;
using NativeQuadTree;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using YY.Enemy;

namespace YY {
    public class QuadFindManager : MonoBehaviour {

    }
    /// <summary>
    /// 查询的数据处理
    /// </summary>
    public partial struct QueryResultDispose {
        public int SelfIndex;//被处理数据的index
        public float MinValue;
        public int QueryIndex;//谁查到的这个数据的index
        public float3 NearPos;
    }
    [UpdateInGroup(typeof(CustomFixedStep025SimulationSystemGroup))]
    public partial struct QuadFindTurretSystem : ISystem {
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            var allQuery = new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .Build(state.EntityManager);
            var turretQuery = new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithAll<BaseTurretData>()
                .Build(state.EntityManager);
            var enemyQuery = new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithAll<BaseEnemyData>()
                .Build(state.EntityManager);
            var dataArr = allQuery.ToComponentDataArray<BasicAttributeData>(Allocator.TempJob);
            var entityArr = allQuery.ToEntityArray(Allocator.TempJob);

            var elements = DotsUtility.GetQuarTreeElements(dataArr);
            //重新赋值位置
            for (int i = 0; i < dataArr.Length; i++) {
                var data = elements[i];
                data.pos = dataArr[i].CurrentPos.xz;
                elements[i] = data;
            }
            var quadTree = new NativeQuadTree<BasicAttributeData>(new AABB2D(0,100));
            //并查集ID
            int currentIndex = 0;
            int turrentIndex = 0;
            var turretEntity = turretQuery.ToEntityArray(Allocator.Temp);
            var turretID = new NativeArray<int>(turretEntity.Length,Allocator.TempJob);
            int enemyIndex = 0;
            var enemyEntity = enemyQuery.ToEntityArray(Allocator.TempJob);
            var enemyID = new NativeArray<int>(enemyEntity.Length,Allocator.TempJob);
            foreach (var item in entityArr) {
                if (turretEntity.Length > turrentIndex && turretEntity[turrentIndex] == entityArr[currentIndex]) {
                    turretID[turrentIndex++] = currentIndex++;
                    continue;
                } else if (enemyEntity.Length > enemyIndex && enemyEntity[enemyIndex] == entityArr[currentIndex]) {
                    enemyID[enemyIndex++] = currentIndex++;
                    continue;
                }
                currentIndex++;
            }
            if (turretID.Length >= 1 && turretID[^1] == 0 || enemyID.Length >= 1 && enemyID[^1] == 0) {
                //id如果不一致,直接歇逼
            }
            //清除并重新排序
            quadTree.ClearAndBulkInsert(elements);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            //敌人查询最近防御塔
            state.Dependency = new EnemyAttackJob()
            {
                ECB = ecb,
                QuadTree = quadTree,
                AllData = dataArr,
                entityArr = entityArr,

                dataArr = enemyQuery.ToComponentDataArray<BasicAttributeData>(Allocator.TempJob),
                queryDataArr = enemyQuery.ToComponentDataArray<BaseEnemyData>(Allocator.TempJob),
                dataIndexInAll = enemyID
            }.Schedule(state.Dependency);
            state.CompleteDependency();

            ecb.Playback(state.EntityManager);
            quadTree.Dispose();
            elements.Dispose();
            ecb.Dispose();
        }
        [BurstCompile]
        public static QueryResultDispose FindMinTarget(NativeList<QuadElement<BasicAttributeData>> tempList, float3 comparePos) {
            var q= new QueryResultDispose()
            {
                MinValue = float.MaxValue,
                SelfIndex = -1
            };

            foreach (var item in tempList) {
                var dis = math.distancesq(item.element.CurrentPos,comparePos);
                if (dis < q.MinValue) {
                    q.MinValue = dis;
                    q.NearPos = item.element.CurrentPos;
                    q.QueryIndex = item.selfIndex;
                    q.SelfIndex = item.queryIndex;
                }
            }
            return q;
        }
    }

    //敌人攻击目标查询
    [BurstCompile]
    public partial struct EnemyAttackJob : IJob {
        public EntityCommandBuffer ECB;
        public NativeQuadTree<BasicAttributeData> QuadTree;
        public NativeArray<BasicAttributeData> AllData;
        public NativeArray<Entity> entityArr;
        //谁来查
        public NativeArray<BasicAttributeData> dataArr;
        public NativeArray<BaseEnemyData> queryDataArr;
        public NativeArray<int> dataIndexInAll;//并查集
        [BurstCompile]
        public void Execute() {
            var tempList = new NativeList<QuadElement<BasicAttributeData>>(Allocator.Temp);

            for (int i = 0; i < dataArr.Length; i++) {
                var data = dataArr[i];
                var queryData = queryDataArr[i];
                //查询条件
                var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackRange);
                QuadTree.FilterRangeQuery(QuadTree,
                        new QueryInfo()
                        {
                            type = QueryType.Include,
                            targetType = DataType.Turret | DataType.Core,
                            selfIndex = dataIndexInAll[i]
                        },
                        new AABB2D(data.CurrentPos.xz, data.CurrentAttackRange),
                        tempList);
                //执行查询逻辑 查找最近的敌人设置位置,并且攻击扣血
                var q = QuadFindTurretSystem.FindMinTarget(tempList,data.CurrentPos);
                if (q.SelfIndex != -1) {
                    //var tempData = AllData[q.NearIndex];
                    queryData.MovePos = q.NearPos;
                    //ECB.SetComponent(entityArr[q.NearIndex], tempData);
                    ECB.SetComponent(entityArr[q.SelfIndex], queryData);
                }
                tempList.Clear();
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    public partial class CustomFixedStep025SimulationSystemGroup : ComponentSystemGroup {
        // 设置此组使用的时间步长，以秒为单位。默认值为 1/60 秒。
        // 此值将被限制在 [0.0001f ... 10.0f] 范围内。
        public float Timestep {
            get => RateManager != null ? RateManager.Timestep : 0;
            set {
                if (RateManager != null)
                    RateManager.Timestep = value;
            }
        }
        // Default constructor
        public CustomFixedStep025SimulationSystemGroup() {
            float defaultFixedTimestep = 1.0f / 60.0f * 0.25f;//0.25秒执行一次
            // 将固定利率简单管理器设置为利率管理器并创建系统组分配器
            SetRateManagerCreateAllocator(new RateUtils.FixedRateSimpleManager(defaultFixedTimestep));
        }

    }
    #region test query
    [BurstCompile]
    public partial struct TestQuadFindTurretJob : IJob {
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
                        selfIndex = item.selfIndex
                    },
                    new AABB2D(data.CurrentPos.xz, data.CurrentAttackRange),
                    tempList);

                if (tempList.Length <= 0) continue;
                Results.Add(tempList[0]);
                tempList.Clear();
            }
            tempList.Dispose();
        }
    }

    [BurstCompile]
    public partial struct TestQueryBestNearJob : IJob {
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
                var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackRange);
                QuadTree.FilterRangeQuery(QuadTree,
                        new QueryInfo()
                        {
                            type = QueryType.Include,
                            targetType = DataType.Enemy,
                        },
                        new AABB2D(data.CurrentPos.xz, data.CurrentAttackRange),
                        tempList);
                //执行查询逻辑
                float minValue = float.MaxValue;
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

    #endregion
}
