using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Dots.RVO {
    [BurstCompile]
    public struct AgentDataResult : IComponentData {
        public float2 position;
        public float2 velocity;
    }
}
