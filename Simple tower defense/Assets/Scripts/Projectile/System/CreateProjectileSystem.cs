using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace YY.Projectile {
    [BurstCompile]
    public partial struct CreateProjectileSystem : ISystem {
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<CreateProjectBuffer>();
            state.RequireForUpdate<ProjectilePrefabData>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var prefabData = SystemAPI.GetSingleton<ProjectilePrefabData>();
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new CreateProjectileJob()
            {
                prefabData = prefabData,
                ECB = ecb.AsParallelWriter(),
            }.Schedule(state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    [BurstCompile]
    public partial struct CreateProjectileJob : IJobEntity {
        [ReadOnly]public ProjectilePrefabData prefabData;
        public EntityCommandBuffer.ParallelWriter ECB;
        [BurstCompile]
        public void Execute([EntityIndexInQuery] int index, ref DynamicBuffer<CreateProjectBuffer> buffer) {
            if (buffer.Length <= 0) return;

            foreach (var item in buffer) {
                switch (item.Type) {
                    case ProjectileType.MachineGunBaseProjectile: {
                            var e = ECB.Instantiate(index,prefabData.MachineGunBaseProjectilePrefab);
                            ECB.AddComponent(index, e, new ProjectileData
                            {
                                MoveDir = item.MoveDir,
                                Speed = item.Speed,
                                EndPos = item.EndPos,
                                DeadTime = 2,
                            });
                            var tempTrans = LocalTransform.FromPosition(item.StartPos);
                            tempTrans.Rotation = quaternion.Euler(math.dot(math.forward(), item.MoveDir));
                            ECB.SetComponent(index, e, tempTrans);
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