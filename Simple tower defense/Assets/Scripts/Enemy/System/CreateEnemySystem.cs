using Dots.RVO;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.PackageManager;
using YY.MainGame;

namespace YY.Enemy {
    //不允许自动创建系统
    //[DisableAutoCreation]
    [UpdateInGroup(typeof(CreateBasicAttributeSystemGroup))]
    public partial struct CreateEnemySystem : ISystem {
        public static CreateEnemySystem Ins;
        private void OnCreate(ref SystemState state) {
            Ins = this;
            state.RequireForUpdate<CreateEnemyBuffer>();
        }
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var enemyPrefabData = SystemAPI.GetSingleton<EnemyPrefabData>();

            state.Dependency = new CreateEnemyJob()
            {
                ECB = ecb.AsParallelWriter(),
                EnemyPrefabData = enemyPrefabData,
            }.ScheduleParallel(state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        private partial struct CreateEnemyJob : IJobEntity {
            public EntityCommandBuffer.ParallelWriter ECB;
            public EnemyPrefabData EnemyPrefabData;
            private void Execute([EntityIndexInQuery] int index, ref DynamicBuffer<CreateEnemyBuffer> buffer) {
                if (buffer.Length <= 0) return;
                foreach (var item in buffer)
                    for (int i = 0; i < item.Num; i++) {
                        Entity e = Entity.Null;
                        BasicAttributeData basicData = new BasicAttributeData();
                        BaseEnemyData data = new BaseEnemyData();
                        ShaderOverrideColor color = new ShaderOverrideColor();

                        basicData.CurrentPos = item.Pos;
                        basicData.Type = DataType.Enemy;
                        switch (item.EnemyType) {
                            case EnemyType.BaseCube: {
                                    e = ECB.Instantiate(index, EnemyPrefabData.BaseCubePrefab);
                                    basicData.MaxHP = 4;
                                    basicData.BaseAttack = 10;
                                    basicData.BaseAttackInterval = 2;
                                    basicData.CurrentAttackCircle = 2;

                                    data.Speed = 5;

                                    color.Value = new float4(1, 0.6f, 0.6f, 1);
                                    break;
                                }
                            case EnemyType.HighHP: {
                                    e = ECB.Instantiate(index, EnemyPrefabData.HighHPCubePrefab);
                                    basicData.MaxHP = 100;
                                    basicData.BaseAttack = 300;
                                    basicData.BaseAttackInterval = 5;
                                    basicData.CurrentAttackCircle = 2;

                                    data.Speed = 2;

                                    color.Value = new float4(0.5f, 0, 0, 1);
                                    break;
                                }
                        }
                        basicData.CurrentHP = basicData.MaxHP;
                        basicData.CurrentAttack = basicData.BaseAttack;
                        basicData.CurrentAttackInterval = basicData.BaseAttackInterval;
                        basicData.RemainAttackIntervalTime = basicData.CurrentAttackInterval;

                        data.MovePos = basicData.CurrentPos;
                        data.MovePosValue = int.MaxValue;

                        ECB.AddComponent(index, e, basicData);
                        ECB.AddComponent(index, e, data);
                        ECB.AddComponent(index, e, color);
                        ECB.AddComponent(index, e, new DamageColorData()
                        {
                            BaseTime = 0.2f,
                            BaseColor = color.Value,
                            CurrentColor = color.Value,
                        });
                        ECB.AddBuffer<ReduceHPBuffer>(index, e);
                        ECB.AddComponent<NewItemTag>(index, e);

                        #region ORCA
                        //float High = 1.8f;
                        float Height = 1f;
                        float Radius = 1f;
                        float RadiusObst = 1f;
                        float MaxSpeed = 10f;
                        float DefaultMaxSpeed = 10f;
                        float TimeHorizon = 0.0001f;
                        float TimeHorizonObst = 0.0001f;
                        bool NavigationEnabled = true;
                        bool CollisionEnabled = true;
                        int MaxNeighbors = 4;
                        float NeighborDist = 5f;
                        float NeighborElev = 0.5f;
                        ORCALayer LayerOccupation = ORCALayer.L0;
                        ORCALayer LayerIgnore = ORCALayer.NONE;
                        float2 PrefVelocity = float2.zero;
                        float2 Velocity = float2.zero;
                        ECB.AddComponent(index, e, new AgentData()
                        {
                            height = Height,
                            radius = Radius,
                            radiusObst = RadiusObst,
                            maxSpeed = MaxSpeed,
                            DefaultMaxSpeed = DefaultMaxSpeed,
                            maxNeighbors = MaxNeighbors,
                            neighborDist = NeighborDist,
                            neighborElev = NeighborElev,
                            timeHorizon = TimeHorizon,
                            timeHorizonObst = TimeHorizonObst,
                            navigationEnabled = NavigationEnabled,
                            collisionEnabled = CollisionEnabled,
                            layerOccupation = LayerOccupation,
                            layerIgnore = LayerIgnore,
                            prefVelocity = PrefVelocity,
                            velocity = Velocity,
                            IsSlowly = false,
                            IsJams = false,
                            CurrentJamsTime = 0,
                            DefaultJamsTime = 0.5f,
                            position = basicData.CurrentPos.xz,
                            worldPosition = basicData.CurrentPos,
                        });
                        #endregion

                        ECB.SetEnabled(index, e, false);
                    }
                buffer.Clear();
            }
        }
    }
}