using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace YY.Enemy {
    public class EnemyManagerAuthroing : MonoBehaviour {
        public List<GameObject> EnemyList;
        public Vector3 GeneratorPos;
    }
    public partial class EnemyManagerBaker : Baker<EnemyManagerAuthroing> {
        public override void Bake(EnemyManagerAuthroing authoring) {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent<BaseEnemyData>(e);
            AddComponent(e, new EnemyPrefabData
            {
                BaseCubePrefab = GetEntity(authoring.EnemyList[0], TransformUsageFlags.Dynamic)
            });
            AddComponent(e, new EnemyInitData
            {
                InitPos = authoring.GeneratorPos
            });
            AddComponent<CreateEnemyBuffer>(e);
        }
    }

    public partial struct EnemyPrefabData : IComponentData {
        public Entity BaseCubePrefab;
    }
    public partial struct EnemyInitData : IComponentData {
        public float3  InitPos;
    }
    public enum EnemyType {
        BaseCube,
    }
    public partial struct CreateEnemyBuffer : IBufferElementData {
        public EnemyType EnemyType;
        public int Num;
        public float3 Pos;
    }

    public partial struct CreateEnemySystem : ISystem {
        private void OnCreate(ref SystemState state) {
            state.RequireForUpdate<CreateEnemyBuffer>();
        }
        [BurstCompile]
        private void OnUpdate(ref SystemState state) {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            var enemyPrefabData = SystemAPI.GetSingleton<EnemyPrefabData>();
            var enemyInitData = SystemAPI.GetSingleton<EnemyInitData>();

            state.Dependency = new CreateEnemyJob()
            {
                ECB = ecb.AsParallelWriter(),
                EnemyPrefabData = enemyPrefabData,
                enemyInitData = enemyInitData

            }.ScheduleParallel(state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        private partial struct CreateEnemyJob : IJobEntity {
            public EntityCommandBuffer.ParallelWriter ECB;
            public EnemyPrefabData EnemyPrefabData;
            public EnemyInitData enemyInitData;
            private void Execute([EntityIndexInQuery] int index, ref DynamicBuffer<CreateEnemyBuffer> buffer) {
                if (buffer.Length <= 0) return;
                foreach (var item in buffer)
                    for (int i = 0; i < item.Num; i++) {
                        switch (item.EnemyType) {
                            case EnemyType.BaseCube: {
                                    var e = ECB.Instantiate(index, EnemyPrefabData.BaseCubePrefab);
                                    ECB.AddComponent(index, e, new BasicAttributeData
                                    {
                                        CurrentPos = enemyInitData.InitPos,
                                        Type = DataType.Enemy,
                                    });
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