using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace YY.Projectile {
    public class ProjectileManagerAuthoring : MonoBehaviour {
        public List<GameObject> ProjectilePrefab;
    }
    public partial class ProjectileManagerBaker : Baker<ProjectileManagerAuthoring> {
        public override void Bake(ProjectileManagerAuthoring authoring) {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new ProjectilePrefabData
            {
                MachineGunBaseProjectilePrefab = GetEntity(authoring.ProjectilePrefab[0], TransformUsageFlags.Dynamic),
            });
        }
    }
    public enum ProjectileType {
        MachineGunBaseProjectile,
    }
    [BurstCompile]
    public partial struct ProjectilePrefabData : IComponentData {
        public Entity MachineGunBaseProjectilePrefab;
    }
    [BurstCompile]
    public partial struct CreateProjectBuffer : IBufferElementData {
        public ProjectileType Type;
        public float3 MoveDir;
        public float3 EndPos;
        public float3 StartPos;
        public float Speed;
    }
    [BurstCompile]
    public partial struct ProjectileData : IComponentData {
        public bool Init;
        public float Speed;
        public float3 MoveDir;
        public float3 EndPos;
    }
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
        }
    }
    [BurstCompile]
    public partial struct CreateProjectileJob : IJobEntity {
        public ProjectilePrefabData prefabData;
        public EntityCommandBuffer.ParallelWriter ECB;
        [BurstCompile]
        public void Execute([EntityIndexInQuery]int index,ref DynamicBuffer<CreateProjectBuffer> buffer) {
            if (buffer.Length <= 0) return;

            foreach (var item in buffer)
            {
                switch (item.Type) {
                    case ProjectileType.MachineGunBaseProjectile: {
                            var e = ECB.Instantiate(index,prefabData.MachineGunBaseProjectilePrefab);
                            ECB.AddComponent(index, e, new ProjectileData
                            {
                                MoveDir = item.MoveDir,
                                Speed = item.Speed,
                                EndPos = item.EndPos
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

    public partial struct MoveProjectSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var query = new EntityQueryBuilder(Allocator.TempJob)
                .WithAll<ProjectileData>()
                .WithAll<LocalTransform>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Build(state.EntityManager);

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new MoveProjectileJob()
            {
                ECB = ecb.AsParallelWriter(),
                time = SystemAPI.Time.DeltaTime
            }.Schedule(query,state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);
        }
    }
    [BurstCompile]
    public partial struct MoveProjectileJob : IJobEntity {
        public EntityCommandBuffer.ParallelWriter ECB;
        [ReadOnly]public float time;
        [BurstCompile]
        public void Execute([EntityIndexInQuery]int index,Entity e, in ProjectileData data,in LocalTransform trans) {
            var tempData = data;
            var tempTrans = trans;
            if (!tempData.Init) {
                tempData.Init = true;
                ECB.SetEnabled(index, e, true);
            }
            if(math.distancesq(tempTrans.Position,tempData.EndPos) < 0.1f) {
                ECB.DestroyEntity(index, e);
                return;
            }

            tempTrans.Position += tempData.MoveDir * tempData.Speed * time;

            ECB.SetComponent(index, e, tempData);
            ECB.SetComponent(index, e, tempTrans);
        }
    }
}
