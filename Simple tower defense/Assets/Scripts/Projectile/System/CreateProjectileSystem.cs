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
        public EntityCommandBuffer.ParallelWriter ECB;

        [ReadOnly]public ProjectilePrefabData prefabData;
        [BurstCompile]
        public void Execute([EntityIndexInQuery] int index, ref DynamicBuffer<CreateProjectBuffer> buffer) {
            if (buffer.Length <= 0) return;

            foreach (var item in buffer) {
                Entity e = Entity.Null;
                LocalTransform tempTrans = LocalTransform.Identity;
                ProjectileData data = new ProjectileData();

                data.Data = item;

                switch (item.Type) {
                    case ProjectileType.MachineGunBaseProjectile: {
                            e = ECB.Instantiate(index, prefabData.MachineGunBaseProjectilePrefab);
                            tempTrans = LocalTransform.FromPosition(item.StartPos);
                            tempTrans.Rotation = quaternion.LookRotation(item.MoveDir, math.up());

                            break;
                        }
                    case ProjectileType.MortorProjectile: {
                            e = ECB.Instantiate(index, prefabData.MortorProjectPrefab);
                            tempTrans = LocalTransform.FromPosition(item.StartPos);
                            break;
                        }
                    default:
                        break;
                }
                ECB.AddComponent(index, e, data);
                ECB.SetComponent(index, e, tempTrans);
                ECB.SetEnabled(index, e, false);
            }
            buffer.Clear();
        }
    }
}