using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace YY.MainGame {
    public partial struct DeadSystem : ISystem {
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<BasicAttributeData>();
        }
        public void OnUpdate(ref SystemState state) {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new DeadJob()
            {
                ECB = ecb.AsParallelWriter()
            }.Schedule(state.Dependency);
            state.CompleteDependency();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
    public partial struct DeadJob : IJobEntity {
        public EntityCommandBuffer.ParallelWriter ECB;
        public void Execute([EntityIndexInQuery] int index, Entity e, in BasicAttributeData data) {
            if (data.CurrentHP <= 0) {
                ECB.DestroyEntity(index, e);
            }
        }
    }
}
