using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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
                        switch (item.EnemyType) {
                            case EnemyType.BaseCube: {
                                    var e = ECB.Instantiate(index, EnemyPrefabData.BaseCubePrefab);
                                    ECB.AddComponent(index, e, new BasicAttributeData
                                    {
                                        CurrentPos = item.Pos,
                                        Type = DataType.Enemy,
                                    });
                                    ECB.AddComponent(index, e, new BaseEnemyData()                                   );
                                    ECB.AddComponent(index, e, new ShaderOverrideColor() { Value = new float4(1,0.6f,0.6f,1)});
                                    ECB.AddComponent(index, e, new DamageColorData(){
                                        BaseTime = 0.2f,
                                        BaseColor = new float4(1, 0.6f, 0.6f, 1),
                                        CurrentColor = new float4(1, 0.6f, 0.6f, 1),
                                    });
                                    ECB.AddBuffer<ReduceHPBuffer>(index, e);
                                    ECB.SetEnabled(index, e, false);
                                    break;
                                }
                            default:
                                break;
                        }
                    }
                buffer.Clear();
            }
        }
    }
}