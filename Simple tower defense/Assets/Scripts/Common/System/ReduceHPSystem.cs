using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace YY.MainGame {
    [BurstCompile]
    public partial struct ReduceHPSystem : ISystem {
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<ReduceHPBuffer>();
            state.RequireForUpdate<BasicAttributeData>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            new ReduceHPJob().Schedule();
        }
    }
    [BurstCompile]
    public partial struct ReduceHPJob : IJobEntity {
        [BurstCompile]
        public void Execute([EntityIndexInQuery]int index,ref DynamicBuffer<ReduceHPBuffer> buffer,ref BasicAttributeData data) {
            if (buffer.Length <= 0) return;
            foreach (var item in buffer)
            {
                data.CurrentHP -= item.HP;
                if (data.CurrentHP <= 0)
                    data.CurrentHP = 0;
            }
            buffer.Clear();
        }
    }
}