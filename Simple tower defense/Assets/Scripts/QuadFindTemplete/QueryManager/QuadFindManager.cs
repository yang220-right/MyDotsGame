using CustomQuadTree;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;
using YY.Enemy;
using YY.Projectile;
using YY.Turret;
using NativeQuadTree;
using static CustomQuadTree.CustomNativeQuadTree;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Transforms;

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
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<FFControllerData>();
            state.RequireForUpdate<GameControllerData>();
        }
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
            var query = new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<ProjectileData>()
                .WithAll<LocalTransform>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(state.EntityManager);

            var dataArr = allQuery.ToComponentDataArray<BasicAttributeData>(Allocator.TempJob);
            var entityArr = allQuery.ToEntityArray(Allocator.TempJob);

            DotsUtility.GetQuarTreeElements(ref dataArr, out var elements);
            //重新赋值位置
            for (int i = 0; i < dataArr.Length; i++) {
                var data = elements[i];
                data.pos = dataArr[i].CurrentPos.xz;
                elements[i] = data;
            }
            var controllerData = SystemAPI.GetSingleton<GameControllerData>();
            var quadTree = new CustomNativeQuadTree(new AABB2D(0,new float2(controllerData.MapColumn,controllerData.MapRow)));

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

            //var ecbEnemyAttack = new EntityCommandBuffer(Allocator.TempJob);
            var ecbCoreAttack = new EntityCommandBuffer(Allocator.TempJob);
            var ecbTurretAttack = new EntityCommandBuffer(Allocator.TempJob);

            //敌人查询最近防御塔
            //state.Dependency = new EnemyAttackJob()
            //{
            //    ECB = ecbEnemyAttack,
            //    TreeQuery = treeQuery,
            //    AllData = dataArr,
            //    entityArr = entityArr,
            //    time = SystemAPI.Time.DeltaTime,

            //    dataIndexInAll = enemyID,
            //    QueryNum = turretEntity.Length + coreEntity.Length,
            //}.Schedule(enemyQuery, state.Dependency);
            //state.CompleteDependency();
            //ecbEnemyAttack.Playback(state.EntityManager);
            if (coreQuery.CalculateEntityCount() > 0) {
                //核心查询最近敌人
                state.Dependency = new CoreAttackJob()
                {
                    ECB = ecbCoreAttack.AsParallelWriter(),
                    TreeQuery = treeQuery,
                    AllData = dataArr,
                    entityArr = entityArr,
                    time = SystemAPI.Time.DeltaTime,

                    coreIndex = coreIndex,
                }.Schedule(coreQuery, state.Dependency);
                state.CompleteDependency();
                ecbCoreAttack.Playback(state.EntityManager);
            }
            //防御塔查询敌人并攻击
            state.Dependency = new TurretAttackJob()
            {
                ECB = ecbTurretAttack.AsParallelWriter(),
                TreeQuery = treeQuery,
                AllData = dataArr,
                entityArr = entityArr,
                time = SystemAPI.Time.DeltaTime,

                dataIndexInAll = turretID,
                QueryNum = enemyEntity.Length,
            }.Schedule(turretQuery, state.Dependency);
            state.CompleteDependency();
            ecbTurretAttack.Playback(state.EntityManager);

            var projectileECB = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new MoveProjectileJob()
            {
                ECB = projectileECB.AsParallelWriter(),
                TreeQuery = treeQuery,
                AllData = dataArr,
                entityArr = entityArr,

                time = SystemAPI.Time.DeltaTime
            }.Schedule(query, state.Dependency);
            state.CompleteDependency();
            projectileECB.Playback(state.EntityManager);

            //ecbEnemyAttack.Dispose();
            ecbCoreAttack.Dispose();
            ecbTurretAttack.Dispose();
            projectileECB.Dispose();

        }
        [BurstCompile]
        public static unsafe void FindMinTarget(in NativeList<QuadElement> tempList, in float3 comparePos, out QueryResultDispose q) {
            q = new QueryResultDispose()
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
        }
        [BurstCompile]
        public static unsafe void FindMaxHPTarget(in NativeList<QuadElement> tempList, out QueryResultDispose q) {
            q = new QueryResultDispose()
            {
                MinValue = float.MaxValue,
                QueryIndex = -1
            };
            float maxHP = 0;
            foreach (var item in tempList) {
                //排除未初始化的搜索的实体
                if (maxHP < item.element.CurrentHP) {
                    q.NearPos = item.element.CurrentPos;
                    q.QueryIndex = item.selfIndex;
                    q.SelfIndex = item.queryIndex;
                }
            }
        }

        public static unsafe void RandomTarget(in NativeList<QuadElement> tempList, out QueryResultDispose q, int seed = -1) {
            q = new QueryResultDispose()
            {
                MinValue = float.MaxValue,
                QueryIndex = -1
            };
            if (tempList.Length > 0) {
                QuadElement item;
                if (seed == -1) {
                    item = tempList[0];
                } else {
                    item = tempList[Unity.Mathematics.Random.CreateFromIndex((uint)seed).NextInt(0, tempList.Length)];
                }
                q.NearPos = item.element.CurrentPos;
                q.QueryIndex = item.selfIndex;
                q.SelfIndex = item.queryIndex;
            }
        }
    }

    ///todo 在查询前,计算一次流场进行寻路,每个敌人会计算出一个防御塔为最近的目标,每次新增防御塔都会重新计算一次
    ///todo 然后一直向着目标移动,并判断距离,如果距离够近,则开始攻击 可以用GameControl来控制是否需要重新计算流场

    //敌人攻击目标查询
    //[BurstCompile]
    //public partial struct EnemyAttackJob : IJobEntity {
    //    public EntityCommandBuffer ECB;
    //    //使用安全指针
    //    [NativeDisableUnsafePtrRestriction]
    //    [ReadOnly]public CustomQuadTreeQuery TreeQuery;
    //    [ReadOnly]public NativeArray<BasicAttributeData> AllData;
    //    [ReadOnly]public NativeArray<Entity> entityArr;
    //    [ReadOnly]public float time;
    //    //谁来查
    //    [ReadOnly]public NativeArray<int> dataIndexInAll;//并查集
    //    [ReadOnly]public int QueryNum;//能够查询的最大数量

    //    [BurstCompile]
    //    public unsafe void Execute([EntityIndexInQuery] int index, Entity e, ref BasicAttributeData data, ref BaseEnemyData queryData) {
    //        var tempList = new NativeList<QuadElement>(AllData.Length,Allocator.Temp);
    //        //查询条件
    //        var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackCircle);
    //        TreeQuery.Q(aabb,
    //                tempList,
    //                new QueryInfo()
    //                {
    //                    type = QueryType.Include,
    //                    targetType = DataType.Turret | DataType.Core,
    //                    selfIndex = dataIndexInAll[index]
    //                });
    //        //执行查询逻辑 查找最近的敌人设置位置,并且攻击扣血
    //        var q = QuadFindSystem.FindMinTarget(tempList,data.CurrentPos);
    //        if (q.QueryIndex >= 0) {
    //            var targetData = AllData[q.QueryIndex];
    //            //敌人攻击防御塔
    //            if (data.IsBeAttack) {
    //                if (data.RemainAttackIntervalTime <= 0) {
    //                    ECB.AppendToBuffer(entityArr[q.QueryIndex], new ReduceHPBuffer
    //                    {
    //                        HP = data.CurrentAttack
    //                    });
    //                    data.RemainAttackIntervalTime = data.CurrentAttackInterval;
    //                }
    //            }
    //            queryData.MovePos = q.NearPos;
    //        } else {//没查到人,则执行默认走路
    //            queryData.MovePos = float3.zero;
    //        }
    //        queryData.MovePos = float3.zero;
    //        tempList.Clear();
    //        tempList.Dispose();
    //    }
    //}


    //核心攻击敌人目标查询 暂时为单个

    [BurstCompile]
    public partial struct CoreAttackJob : IJobEntity {
        public EntityCommandBuffer.ParallelWriter ECB;
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly]public CustomQuadTreeQuery TreeQuery;
        [ReadOnly]public NativeArray<BasicAttributeData> AllData;
        [ReadOnly]public NativeArray<Entity> entityArr;
        [ReadOnly]public float time;
        //谁来查
        public int coreIndex;
        [BurstCompile]
        public unsafe void Execute([EntityIndexInQuery] int index, Entity e, ref BasicAttributeData data) {
            if (data.IsBeAttack && data.RemainAttackIntervalTime > 0) {
                data.RemainAttackIntervalTime -= time;
                return;
            }
            //这里必须指定大小足够大,否则数据错乱
            var tempList = new NativeList<QuadElement>(AllData.Length,Allocator.Temp);
            var entity = entityArr[coreIndex];
            //查询条件
            var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackCircle);
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
            QuadFindSystem.FindMinTarget(tempList, data.CurrentPos, out var q);
            if (q.QueryIndex >= 0) {
                var targetData = AllData[q.QueryIndex];
                data.IsBeAttack = true;
                //攻击敌人
                ECB.AppendToBuffer(index, entityArr[q.QueryIndex], new ReduceHPBuffer
                {
                    HP = data.CurrentAttack
                });
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                var dir = targetData.CurrentPos - data.CurrentPos;
                if (math.length(dir) < float.Epsilon) {
                    dir = math.up();
                }
                ECB.AppendToBuffer(index, entity, new CreateProjectBuffer
                {
                    Type = ProjectileType.MachineGunBaseProjectile,
                    MoveDir = math.normalize(dir),
                    EndPos = targetData.CurrentPos,
                    StartPos = float3.zero,
                    Speed = 30
                });
            } else {
                data.IsBeAttack = false;
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
            }
            tempList.Clear();
            tempList.Dispose();
        }
    }

    [BurstCompile]
    public partial struct TurretAttackJob : IJobEntity {
        //全部实体
        public EntityCommandBuffer.ParallelWriter ECB;
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly]public CustomQuadTreeQuery TreeQuery;
        [ReadOnly]public NativeArray<BasicAttributeData> AllData;
        [ReadOnly]public NativeArray<Entity> entityArr;
        //谁来查
        [ReadOnly]public NativeArray<int> dataIndexInAll;//并查集
        [ReadOnly] public int QueryNum;
        [ReadOnly]public float time;
        [BurstCompile]
        public unsafe void Execute([EntityIndexInQuery] int index, Entity e, ref BasicAttributeData data, ref BaseTurretData turretData) {
            //执行查询逻辑
            switch (turretData.Type) {
                case TurretType.GunTowers:
                    GunTowersQuery(index, ref data, ref turretData);
                    break;
                case TurretType.FireTowers:
                    FireTowersQuery(index, ref data, ref turretData);
                    break;
                case TurretType.MortorTowers:
                    MortorTowerQuery(index, ref data, ref turretData);
                    break;
                case TurretType.SniperTowers:
                    SniperTowersQuery(index, ref data, ref turretData);
                    break;
                case TurretType.GuideTowers:
                    GuideTowersQuery(index, ref data, ref turretData);
                    break;
                case TurretType.PlagueTowers:
                    PlagueTowersQuery(index, ref data, ref turretData);
                    break;
            }
        }
        [BurstCompile]
        public unsafe void GunTowersQuery(int i, ref BasicAttributeData data, ref BaseTurretData turretData) {
            if (data.IsBeAttack && data.RemainAttackIntervalTime > 0) {
                data.RemainAttackIntervalTime -= time;
                return;
            }
            var tempList = new NativeList<QuadElement>(QueryNum,Allocator.Temp);
            //查询条件
            var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackCircle);
            TreeQuery.Q(aabb,
                tempList,
                new QueryInfo()
                {
                    type = QueryType.Include,
                    targetType = DataType.Enemy,
                    selfIndex = dataIndexInAll[i],

                    AttackType = turretData.AttackType,
                });
            if (tempList.Length <= 0) return;
            QuadFindSystem.FindMinTarget(tempList, data.CurrentPos, out var q);
            //执行查询结果,并应用
            if (q.QueryIndex >= 0) {
                var targetData = AllData[q.QueryIndex];
                data.IsBeAttack = true;
                //攻击敌人
                ECB.AppendToBuffer(i, entityArr[q.QueryIndex], new ReduceHPBuffer
                {
                    HP = data.CurrentAttack
                });
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                var dir = targetData.CurrentPos - data.CurrentPos;
                if (math.length(dir) < float.Epsilon)
                    dir = math.up();
                ECB.AppendToBuffer(i, entityArr[dataIndexInAll[i]], new CreateProjectBuffer
                {
                    Type = ProjectileType.MachineGunBaseProjectile,
                    MoveDir = math.normalize(dir),
                    EndPos = targetData.CurrentPos,
                    StartPos = data.CurrentPos,
                    Speed = 30
                });
            } else {
                data.IsBeAttack = false;
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
            }
            tempList.Clear();
            tempList.Dispose();
        }
        [BurstCompile]
        public unsafe void FireTowersQuery(int i, ref BasicAttributeData data, ref BaseTurretData turretData) {
            if (data.IsBeAttack && data.RemainAttackIntervalTime > 0) {
                data.RemainAttackIntervalTime -= time;
                return;
            }
            var tempList = new NativeList<QuadElement>(QueryNum,Allocator.Temp);
            //查询条件
            var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackCircle);
            TreeQuery.Q(aabb,
                tempList,
                 new QueryInfo()
                 {
                     type = QueryType.Include,
                     targetType = DataType.Enemy,
                     selfIndex = dataIndexInAll[i],

                     AttackType = turretData.AttackType,
                     Pos = data.CurrentPos,
                     MaxNum = 1,
                 });
            float2 Dir = float2.zero;
            if (tempList.Length > 0) {
                QuadFindSystem.RandomTarget(tempList, out var minQ, i);
                Dir = minQ.NearPos.xz - data.CurrentPos.xz;
                Dir = math.normalize(Dir);
                tempList.Clear();
            }
            if (Dir.Equals(float2.zero)) {
                Dir.x = 1;
            }
            TreeQuery.Q(aabb,
                tempList,
                new QueryInfo()
                {
                    type = QueryType.Include,
                    targetType = DataType.Enemy,
                    selfIndex = dataIndexInAll[i],

                    AttackType = turretData.AttackType,
                    Pos = data.CurrentPos,
                    CurrentAttackDir = Dir,
                    AttackCircle = data.CurrentAttackCircle,
                    AttackRange = data.AttackAngle,
                    MaxNum = -1,
                });
            if (tempList.Length > 0) {
                data.IsBeAttack = true;
                for (int k = 0; k < tempList.Length; ++k) {
                    var enemyResult = tempList[k];
                    var targetData = AllData[enemyResult.selfIndex];
                    ECB.AppendToBuffer(i, entityArr[enemyResult.selfIndex], new ReduceHPBuffer
                    {
                        HP = data.CurrentAttack
                    });
                    ECB.SetComponent(i, entityArr[enemyResult.selfIndex], new DamageColorData()
                    {
                        IsChange = true,
                        BaseTime = 0.2f,
                        BaseColor = new float4(1, 0.6f, 0.6f, 1),
                        CurrentColor = new float4(1, 1, 1, 1),
                    });
                    data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                }
            } else {
                data.IsBeAttack = false;
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                data.CurrentAttackDir.xz = Dir;
            }
            tempList.Clear();
            tempList.Dispose();
        }
        [BurstCompile]
        public unsafe void MortorTowerQuery(int i, ref BasicAttributeData data, ref BaseTurretData turretData) {
            if (data.IsBeAttack && data.RemainAttackIntervalTime > 0) {
                data.RemainAttackIntervalTime -= time;
                return;
            }
            data.RemainAttackIntervalTime = data.CurrentAttackInterval;

            var tempList = new NativeList<QuadElement>(QueryNum,Allocator.Temp);
            //查询条件
            var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackCircle);
            TreeQuery.Q(aabb,
                tempList,
                new QueryInfo()
                {
                    type = QueryType.Include,
                    targetType = DataType.Enemy,
                    selfIndex = dataIndexInAll[i],

                    AttackType = turretData.AttackType,
                });
            if (tempList.Length <= 0) {
                data.IsBeAttack = false;
            } else {
                data.IsBeAttack = true;
                QuadFindSystem.RandomTarget(tempList, out var q, i);
                tempList.Clear();
                //发射子弹
                ECB.AppendToBuffer(i, entityArr[dataIndexInAll[i]], new CreateProjectBuffer()
                {
                    Type = ProjectileType.MortorProjectile,
                    MoveType = ProjectileMoveType.Curve,
                    StartPos = data.CurrentPos,
                    EndPos = q.NearPos,
                    ControlePos = (data.CurrentPos + q.NearPos) / 2 + new float3(0, 5, 0),
                    CurrentTime = 0,
                    DeadTime = 2,
                    MaxQueryNum = QueryNum,
                    BasicData = data,
                    QueryInfo = new QueryInfo()
                    {
                        type = QueryType.Include,
                        targetType = DataType.Enemy,
                        selfIndex = dataIndexInAll[i],

                        AttackType = turretData.AttackType,
                        AttackCircle = turretData.BulletCircle,
                        Pos = q.NearPos,
                        MaxNum = -1,
                    }
                });
            }
            tempList.Clear();
            tempList.Dispose();
        }
        [BurstCompile]
        public unsafe void SniperTowersQuery(int i, ref BasicAttributeData data, ref BaseTurretData turretData) {
            if (data.IsBeAttack && data.RemainAttackIntervalTime > 0) {
                data.RemainAttackIntervalTime -= time;
                return;
            }
            var tempList = new NativeList<QuadElement>(QueryNum,Allocator.Temp);
            //查询条件
            var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackCircle);
            TreeQuery.Q(aabb,
                tempList,
                new QueryInfo()
                {
                    type = QueryType.Include,
                    targetType = DataType.Enemy,
                    selfIndex = dataIndexInAll[i],

                    AttackType = turretData.AttackType,
                });
            if (tempList.Length <= 0) return;
            QuadFindSystem.FindMaxHPTarget(tempList, out var q);
            //执行查询结果,并应用
            if (q.QueryIndex >= 0) {
                var targetData = AllData[q.QueryIndex];
                data.IsBeAttack = true;
                //攻击敌人
                ECB.AppendToBuffer(i, entityArr[q.QueryIndex], new ReduceHPBuffer
                {
                    HP = data.CurrentAttack
                });
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                var dir = targetData.CurrentPos - data.CurrentPos;
                if (math.length(dir) < float.Epsilon)
                    dir = math.up();
                ECB.AppendToBuffer(i, entityArr[dataIndexInAll[i]], new CreateProjectBuffer
                {
                    Type = ProjectileType.MachineGunBaseProjectile,
                    MoveDir = math.normalize(dir),
                    EndPos = targetData.CurrentPos,
                    StartPos = data.CurrentPos,
                    Speed = 80
                });
            } else {
                data.IsBeAttack = false;
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
            }
            tempList.Clear();
            tempList.Dispose();
        }
        [BurstCompile]
        public unsafe void GuideTowersQuery(int i, ref BasicAttributeData data, ref BaseTurretData turretData) {
            if (data.IsBeAttack && data.RemainAttackIntervalTime > 0) {
                data.RemainAttackIntervalTime -= time;
                return;
            }
            data.RemainAttackIntervalTime = data.CurrentAttackInterval;

            var tempList = new NativeList<QuadElement>(QueryNum,Allocator.Temp);
            //查询条件
            var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackCircle);
            TreeQuery.Q(aabb,
                tempList,
                new QueryInfo()
                {
                    type = QueryType.Include,
                    targetType = DataType.Enemy,
                    selfIndex = dataIndexInAll[i],

                    AttackType = turretData.AttackType,
                });
            if (tempList.Length <= 0) {
                data.IsBeAttack = false;
            } else {
                data.IsBeAttack = true;
                QuadFindSystem.RandomTarget(tempList, out var q, i);
                tempList.Clear();
                //发射子弹
                ECB.AppendToBuffer(i, entityArr[dataIndexInAll[i]], new CreateProjectBuffer()
                {
                    Type = ProjectileType.MortorProjectile,
                    MoveType = ProjectileMoveType.Curve,
                    StartPos = data.CurrentPos,
                    EndPos = q.NearPos,
                    ControlePos = (data.CurrentPos + q.NearPos) / 2 + new float3(Random.CreateFromIndex((uint)(math.abs(q.NearPos.x * 100))).NextFloat(-10, 10), 5, 0),
                    CurrentTime = 0,
                    DeadTime = 0.5f,
                    MaxQueryNum = QueryNum,
                    BasicData = data,
                    QueryInfo = new QueryInfo()
                    {
                        type = QueryType.Include,
                        targetType = DataType.Enemy,
                        selfIndex = dataIndexInAll[i],

                        AttackType = turretData.AttackType,
                        AttackCircle = turretData.BulletCircle,
                        Pos = q.NearPos,
                        MaxNum = -1,
                    }
                });
            }
            tempList.Clear();
            tempList.Dispose();
        }
        [BurstCompile]
        public unsafe void PlagueTowersQuery(int i, ref BasicAttributeData data, ref BaseTurretData turretData) {
            if (data.IsBeAttack && data.RemainAttackIntervalTime > 0) {
                data.RemainAttackIntervalTime -= time;
                return;
            }
            var tempList = new NativeList<QuadElement>(QueryNum,Allocator.Temp);
            //查询条件
            var aabb = new AABB2D(data.CurrentPos.xz,data.CurrentAttackCircle);
            TreeQuery.Q(aabb,
                tempList,
                new QueryInfo()
                {
                    type = QueryType.Include,
                    targetType = DataType.Enemy,
                    selfIndex = dataIndexInAll[i],

                    AttackType = turretData.AttackType,
                    Pos = data.CurrentPos,
                    AttackCircle = data.CurrentAttackCircle,
                    MaxNum = -1,
                });
            if (tempList.Length > 0) {
                data.IsBeAttack = true;
                for (int k = 0; k < tempList.Length; ++k) {
                    var enemyResult = tempList[k];
                    var targetData = AllData[enemyResult.selfIndex];
                    ECB.AppendToBuffer(i, entityArr[enemyResult.selfIndex], new ReduceHPBuffer
                    {
                        HP = data.CurrentAttack
                    });
                    ECB.SetComponent(i, entityArr[enemyResult.selfIndex], new DamageColorData()
                    {
                        IsChange = true,
                        BaseTime = 1f,
                        BaseColor = new float4(1, 0.6f, 0.6f, 1),
                        CurrentColor = new float4(1, 1, 1, 1),
                    });
                    data.RemainAttackIntervalTime = data.CurrentAttackInterval;
                }
            } else {
                data.IsBeAttack = false;
                data.RemainAttackIntervalTime = data.CurrentAttackInterval;
            }
            tempList.Clear();
            tempList.Dispose();
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

    [BurstCompile]
    public partial struct MoveProjectileJob : IJobEntity {
        public EntityCommandBuffer.ParallelWriter ECB;
        [NativeDisableUnsafePtrRestriction]
        [ReadOnly]public CustomQuadTreeQuery TreeQuery;
        [ReadOnly]public NativeArray<BasicAttributeData> AllData;
        [ReadOnly]public NativeArray<Entity> entityArr;

        [ReadOnly]public float time;
        [BurstCompile]
        public void Execute([EntityIndexInQuery] int index, Entity e, ref ProjectileData data, ref LocalTransform trans) {
            if (!data.Init) {
                data.Init = true;
                ECB.SetEnabled(index, e, true);
            }
            var projectData = data.Data;
            switch (projectData.MoveType) {
                case ProjectileMoveType.Linear: {
                        trans.Position += projectData.MoveDir * projectData.Speed * time;
                        if (math.dot(math.normalize(projectData.EndPos - trans.Position), projectData.MoveDir) <= 0) {
                            ECB.DestroyEntity(index, e);
                            return;
                        }
                        break;
                    }
                case ProjectileMoveType.Curve: {
                        data.Data.CurrentTime += time;
                        DotsUtility.CalculateCubicBezierPoint(data.Data.CurrentTime, projectData.DeadTime,
                            projectData.StartPos, projectData.ControlePos, projectData.EndPos, out var pos);
                        trans.Position = pos;
                        if (data.Data.CurrentTime > projectData.DeadTime) {
                            var tempList = new NativeList<QuadElement>(projectData.MaxQueryNum,Allocator.Temp);
                            var aabb = new AABB2D(projectData.EndPos.xz,projectData.QueryInfo.AttackCircle);
                            TreeQuery.Q(aabb, tempList, projectData.QueryInfo);

                            if (tempList.Length > 0) {
                                for (int k = 0; k < tempList.Length; ++k) {
                                    var enemyResult = tempList[k];
                                    var targetData = AllData[enemyResult.selfIndex];
                                    ECB.AppendToBuffer(index, entityArr[enemyResult.selfIndex], new ReduceHPBuffer
                                    {
                                        HP = projectData.BasicData.CurrentAttack
                                    });
                                    ECB.SetComponent(index, entityArr[enemyResult.selfIndex], new DamageColorData()
                                    {
                                        IsChange = true,
                                        BaseTime = 0.2f,
                                        BaseColor = new float4(1, 0.6f, 0.6f, 1),
                                        CurrentColor = new float4(1, 1, 1, 1),
                                    });
                                }
                            }

                            ECB.DestroyEntity(index, e);
                            return;
                        }
                        break;
                    }
            }
        }
    }
}
