using CustomQuadTree;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using YY.Enemy;
using YY.Projectile;
using YY.Turret;
using NativeQuadTree;
using static CustomQuadTree.CustomNativeQuadTree;
using Unity.Collections.LowLevel.Unsafe;


namespace YY.MainGame {
    /// <summary>
    /// 查询的数据处理
    /// </summary>
    public partial struct QueryResultDispose {
        public int SelfIndex;//被处理数据的index
        public float MinValue;
        public int QueryIndex;//谁查到的这个数据的index
        public float3 NearPos;
    }
    //[UpdateInGroup(typeof(CustomFixedStep025SimulationSystemGroup))]
    public partial struct QuadFindSystem : ISystem {
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
            var coreQuery = new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<BasicAttributeData>()
                .WithAll<TurretBaseCoreData>()
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
            var quadTree = new CustomNativeQuadTree(new AABB2D(0,100));

            //并查集ID
            int currentIndex = 0;
            int turrentIndex = 0;
            var turretEntity = turretQuery.ToEntityArray(Allocator.Temp);
            var turretID = new NativeArray<int>(turretEntity.Length,Allocator.TempJob);
            int enemyIndex = 0;
            var enemyEntity = enemyQuery.ToEntityArray(Allocator.Temp);
            var enemyID = new NativeArray<int>(enemyEntity.Length,Allocator.TempJob);
            var coreEntity = coreQuery.ToEntityArray(Allocator.Temp);
            int coreIndex = 0;
            foreach (var item in entityArr) {
                if (turretEntity.Length > turrentIndex && turretEntity[turrentIndex] == entityArr[currentIndex]) {
                    turretID[turrentIndex++] = currentIndex++;
                    continue;
                } else if (enemyEntity.Length > enemyIndex && enemyEntity[enemyIndex] == entityArr[currentIndex]) {
                    enemyID[enemyIndex++] = currentIndex++;
                    continue;
                } else if (coreEntity.Length > 0 && coreEntity[coreIndex] == entityArr[currentIndex]) {
                    coreIndex = currentIndex++;
                    continue;
                }
                currentIndex++;
            }
            //清除并重新排序
            quadTree.ClearAndBulkInsert(elements);
            //创建查询
            var treeQuery = new CustomQuadTreeQuery().InitTree(quadTree);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            //敌人查询最近防御塔
            var enemyDataArr = enemyQuery.ToComponentDataArray<BasicAttributeData>(Allocator.TempJob);
            var enemyQueryDataArr = enemyQuery.ToComponentDataArray<BaseEnemyData>(Allocator.TempJob);
            state.Dependency = new EnemyAttackJob()
            {
                ECB = ecb,
                TreeQuery = treeQuery,
                AllData = dataArr,
                entityArr = entityArr,
                time = SystemAPI.Time.DeltaTime,

                dataArr = enemyDataArr,
                queryDataArr = enemyQueryDataArr,
                dataIndexInAll = enemyID,
                QueryNum = turretEntity.Length + coreEntity.Length,
            }.Schedule(state.Dependency);
            state.CompleteDependency();
            if (coreQuery.CalculateEntityCount() > 0) {
                //核心查询最近敌人
                state.Dependency = new CoreAttackJob()
                {
                    ECB = ecb,
                    TreeQuery = treeQuery,
                    AllData = dataArr,
                    entityArr = entityArr,
                    time = SystemAPI.Time.DeltaTime,

                    dataArr = coreQuery.ToComponentDataArray<BasicAttributeData>(Allocator.Temp)[0],
                    coreIndex = coreIndex,
                }.Schedule(state.Dependency);
                state.CompleteDependency();
            }
            //防御塔查询敌人并攻击
            var turretDataArr = turretQuery.ToComponentDataArray<BasicAttributeData>(Allocator.TempJob);
            var turretQueryDataArr = turretQuery.ToComponentDataArray<BaseTurretData>(Allocator.TempJob);
            state.Dependency = new TurretAttackJob()
            {
                ECB = ecb,
                TreeQuery = treeQuery,
                AllData = dataArr,
                entityArr = entityArr,
                time = SystemAPI.Time.DeltaTime,

                dataArr = turretDataArr,
                queryDataArr = turretQueryDataArr,
                dataIndexInAll = turretID,
                QueryNum = enemyEntity.Length,
            }.Schedule(state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);

            //allQuery.Dispose();
            //enemyQuery.Dispose();
            //turretQuery.Dispose();
            ecb.Dispose();
            //turretID.Dispose();
            //enemyID.Dispose();
            //dataArr.Dispose();
            //entityArr.Dispose();
            //elements.Dispose();
            //treeQuery.Dispose();
            //enemyDataArr.Dispose();
            //enemyQueryDataArr.Dispose();
            //turretDataArr.Dispose();
            //turretQueryDataArr.Dispose();
        }
        public static unsafe QueryResultDispose FindMinTarget(NativeList<QuadElement> tempList, float3 comparePos) {
            var q= new QueryResultDispose()
            {
                MinValue = float.MaxValue,
                QueryIndex = -1
            };
            foreach (var item in tempList) {
                var dis = math.distancesq(item.element.CurrentPos,comparePos);
                //排除未初始化的搜索的实体
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
        //使用安全指针
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly]public CustomQuadTreeQuery TreeQuery;
        [ReadOnly]public NativeArray<BasicAttributeData> AllData;
        [ReadOnly]public NativeArray<Entity> entityArr;
        [ReadOnly]public float time;
        //谁来查
        [ReadOnly]public NativeArray<BasicAttributeData> dataArr;
        [ReadOnly]public NativeArray<BaseEnemyData> queryDataArr;
        [ReadOnly]public NativeArray<int> dataIndexInAll;//并查集
        [ReadOnly]public int QueryNum;//能够查询的最大数量

        [BurstCompile]
        public unsafe void Execute() {
            var tempList = new NativeList<QuadElement>(AllData.Length,Allocator.Temp);

            for (int i = 0; i < dataArr.Length; i++) {
                var data = dataArr[i];
                var queryData = queryDataArr[i];
                //查询条件
                var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackRange);
                TreeQuery.Q(aabb,
                        tempList,
                        new QueryInfo()
                        {
                            type = QueryType.Include,
                            targetType = DataType.Turret | DataType.Core,
                            selfIndex = dataIndexInAll[i]
                        });
                //执行查询逻辑 查找最近的敌人设置位置,并且攻击扣血
                var q = QuadFindSystem.FindMinTarget(tempList,data.CurrentPos);
                if (q.QueryIndex >= 0) {
                    var targetData = AllData[q.QueryIndex];
                    //敌人攻击防御塔
                    if (data.IsBeAttack) {
                        if (data.RemainAttackIntervalTime <= 0) {
                            ECB.AppendToBuffer(entityArr[q.QueryIndex], new ReduceHPBuffer
                            {
                                HP = data.CurrentAttack
                            });
                            data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                        }
                    }
                    queryData.MovePos = q.NearPos;
                    ECB.SetComponent(entityArr[dataIndexInAll[i]], queryData);
                    ECB.SetComponent(entityArr[dataIndexInAll[i]], data);
                } else {//没查到人,则执行默认走路
                    queryData.MovePos = float3.zero;
                    ECB.SetComponent(entityArr[dataIndexInAll[i]], queryData);
                }
                tempList.Clear();
            }
            tempList.Dispose();
        }
    }

    //核心攻击敌人目标查询 暂时为单个
    [BurstCompile]
    public partial struct CoreAttackJob : IJob {
        public EntityCommandBuffer ECB;
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly]public CustomQuadTreeQuery TreeQuery;
        [ReadOnly]public NativeArray<BasicAttributeData> AllData;
        [ReadOnly]public NativeArray<Entity> entityArr;
        [ReadOnly]public float time;
        //谁来查
        public BasicAttributeData dataArr;
        public int coreIndex;
        public unsafe void Execute() {
            //这里必须指定大小足够大,否则数据错乱
            var tempList = new NativeList<QuadElement>(AllData.Length,Allocator.Temp);
            var entity = entityArr[coreIndex];
            var data = dataArr;
            //查询条件
            var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackRange);
            TreeQuery.Q(
                aabb,
                tempList,
                new QueryInfo()
                {
                    type = QueryType.Include,
                    targetType = DataType.Enemy,
                    selfIndex = coreIndex,
                });
            //执行查询逻辑 查找最近的敌人设置位置,并且攻击扣血
            var q = QuadFindSystem.FindMinTarget(tempList,data.CurrentPos);
            if (q.QueryIndex >= 0) {
                var targetData = AllData[q.QueryIndex];
                data.IsBeAttack = true;
                //攻击敌人
                if (data.RemainAttackIntervalTime <= 0) {
                    ECB.AppendToBuffer(entityArr[q.QueryIndex], new ReduceHPBuffer
                    {
                        HP = data.CurrentAttack
                    });
                    data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                    var dir = targetData.CurrentPos - data.CurrentPos;
                    if (math.length(dir) < float.Epsilon) {
                        dir = math.up();
                    }
                    ECB.AppendToBuffer(entity, new CreateProjectBuffer
                    {
                        Type = ProjectileType.MachineGunBaseProjectile,
                        MoveDir = math.normalize(dir),
                        EndPos = targetData.CurrentPos,
                        StartPos = float3.zero,
                        Speed = 30
                    });
                } else {
                    data.RemainAttackIntervalTime -= time;
                }
                ECB.SetComponent(entity, data);
            } else {
                data.IsBeAttack = false;
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                ECB.SetComponent(entity, data);
            }
            tempList.Clear();
            tempList.Dispose();
        }
    }

    [BurstCompile]
    public partial struct TurretAttackJob : IJob {
        //全部实体
        public EntityCommandBuffer ECB;
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly]public CustomQuadTreeQuery TreeQuery;
        [ReadOnly]public NativeArray<BasicAttributeData> AllData;
        [ReadOnly]public NativeArray<Entity> entityArr;
        //谁来查
        [ReadOnly]public NativeArray<BasicAttributeData> dataArr;
        [ReadOnly]public NativeArray<BaseTurretData> queryDataArr;
        [ReadOnly]public NativeArray<int> dataIndexInAll;//并查集
        [ReadOnly] public int QueryNum;
        [ReadOnly]public float time;
        [BurstCompile]
        public unsafe void Execute() {
            var tempList = new NativeList<QuadElement>(QueryNum,Allocator.Temp);

            for (int i = 0; i < dataArr.Length; i++) {
                var data = dataArr[i];
                var queryData = queryDataArr[i];
                //查询条件
                var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackRange);
                TreeQuery.Q(
                    aabb,
                    tempList,
                    new QueryInfo()
                    {
                        type = QueryType.Include,
                        targetType = DataType.Enemy,
                        selfIndex = dataIndexInAll[i],
                    });
                //执行查询逻辑
                var q = QuadFindSystem.FindMinTarget(tempList,data.CurrentPos);

                //执行查询结果,并应用
                if (q.QueryIndex >= 0) {
                    var targetData = AllData[q.QueryIndex];
                    data.IsBeAttack = true;
                    //攻击敌人
                    if (data.RemainAttackIntervalTime <= 0) {
                        ECB.AppendToBuffer(entityArr[q.QueryIndex], new ReduceHPBuffer
                        {
                            HP = data.CurrentAttack
                        });
                        data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                        var dir = targetData.CurrentPos - data.CurrentPos;
                        if (math.length(dir) < float.Epsilon) {
                            dir = math.up();
                        }
                        ECB.AppendToBuffer(entityArr[dataIndexInAll[i]], new CreateProjectBuffer
                        {
                            Type = ProjectileType.MachineGunBaseProjectile,
                            MoveDir = math.normalize(dir),
                            EndPos = targetData.CurrentPos,
                            StartPos = data.CurrentPos,
                            Speed = 30
                        });
                    } else {
                        data.RemainAttackIntervalTime -= time;
                    }
                    ECB.SetComponent(entityArr[dataIndexInAll[i]], data);
                } else {
                    data.IsBeAttack = false;
                    data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                    ECB.SetComponent(entityArr[dataIndexInAll[i]], data);
                }
                tempList.Clear();
            }
            tempList.Dispose();
        }
    }

    //#region test query
    //[BurstCompile]
    //public partial struct TestQueryBestNearJob : IJob {
    //    //全部实体
    //    public EntityCommandBuffer ECB;
    //    [NativeDisableUnsafePtrRestriction]
    //    public CustomQuadTreeQuery TreeQuery;
    //    public NativeArray<BasicAttributeData> AllData;
    //    public NativeArray<Entity> entityArr;
    //    //谁来查
    //    public NativeArray<BasicAttributeData> dataArr;
    //    public NativeArray<BaseTurretData> queryDataArr;
    //    [BurstCompile]
    //    public void Execute() {
    //        var tempList = new NativeList<QuadElement>(Allocator.Temp);

    //        for (int i = 0; i < dataArr.Length; i++) {
    //            var data = dataArr[i];
    //            var queryData = queryDataArr[i];
    //            //查询条件
    //            var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackRange);
    //            TreeQuery.Q(new AABB2D(data.CurrentPos.xz, data.CurrentAttackRange),
    //                    tempList,
    //                    new QueryInfo()
    //                    {
    //                        type = QueryType.Include,
    //                        targetType = DataType.Enemy,
    //                    });
    //            //执行查询逻辑
    //            float minValue = float.MaxValue;
    //            int nearIndex = -1;
    //            float3 nearPos = 0;
    //            foreach (var item in tempList) {
    //                var dis = math.distancesq(item.element.CurrentPos,data.CurrentPos);
    //                if (dis < minValue) {
    //                    minValue = dis;
    //                    nearPos = item.element.CurrentPos;
    //                    nearIndex = item.selfIndex;
    //                }
    //            }
    //            //执行查询结果,并应用
    //            if (nearIndex != -1) {
    //                var tempData = AllData[nearIndex];
    //                tempData.CurrentHP = 444;
    //                queryData.NearEnemy = nearPos;
    //                ECB.SetComponent(entityArr[nearIndex], tempData);
    //            }
    //            tempList.Clear();
    //        }
    //        tempList.Dispose();
    //    }
    //}
    //#endregion
}
