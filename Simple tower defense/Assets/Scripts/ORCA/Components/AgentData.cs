using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace Dots.RVO {
    public enum AxisPair {
        XY = 0,
        XZ = 1
    }

    [BurstCompile]
    public struct AgentData : IComponentData {
        public int index;
        public int kdIndex;

        public float2 position;
        public float baseline;
        public float2 prefVelocity;
        public float2 velocity;

        public float height;
        public float radius;
        public float radiusObst;
        public float maxSpeed;
        public float DefaultMaxSpeed;

        public int maxNeighbors;
        public float neighborDist;
        public float neighborElev;

        public float timeHorizon;
        public float timeHorizonObst;

        public ORCALayer layerOccupation;
        public ORCALayer layerIgnore;
        public bool navigationEnabled;
        public bool collisionEnabled;

        public float3 worldPosition;
        public float3 worldVelocity;


        public float2 resultPosition;
        public float2 resultVelocity;

        public bool IsSlowly;
        public bool IsJams;
        public float CurrentJamsTime;
        public float DefaultJamsTime;
    }
}
