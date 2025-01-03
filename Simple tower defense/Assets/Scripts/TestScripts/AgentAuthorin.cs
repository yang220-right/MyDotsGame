using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Dots.RVO {
    public class AgentAuthoring : MonoBehaviour {
        public float High = 1.8f;
        public float Height = 1f;
        public float Radius = 1f;
        public float RadiusObst = 1f;
        public float MaxSpeed = 10f;
        public float DefaultMaxSpeed = 10f;
        public float TimeHorizon = 0.0001f;
        public float TimeHorizonObst = 0.0001f;
        public bool NavigationEnabled = true;
        public bool CollisionEnabled = true;
        public int MaxNeighbors = 4;
        public float NeighborDist = 5f;
        public float NeighborElev = 0.5f;
        public ORCALayer LayerOccupation = ORCALayer.L0;
        public ORCALayer LayerIgnore = ORCALayer.NONE;
        public float2 PrefVelocity = new float2(1,0);
        public float2 Velocity = float2.zero;
    }

    public class AgentSpawn : Baker<AgentAuthoring> {
        public override void Bake(AgentAuthoring authoring) {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new AgentData()
            {
                height = authoring.Height,
                radius = authoring.Radius,
                radiusObst = authoring.RadiusObst,
                maxSpeed = authoring.MaxSpeed,
                DefaultMaxSpeed = authoring.DefaultMaxSpeed,
                maxNeighbors = authoring.MaxNeighbors,
                neighborDist = authoring.NeighborDist,
                neighborElev = authoring.NeighborElev,
                timeHorizon = authoring.TimeHorizon,
                timeHorizonObst = authoring.TimeHorizonObst,
                navigationEnabled = authoring.NavigationEnabled,
                collisionEnabled = authoring.CollisionEnabled,
                layerOccupation = authoring.LayerOccupation,
                layerIgnore = authoring.LayerIgnore,
                prefVelocity = authoring.PrefVelocity,
                velocity = authoring.Velocity,
                IsSlowly = false,
                IsJams = false,
                CurrentJamsTime = 0,
                DefaultJamsTime = 0.5f,
            }
            );
        }
    }
}
