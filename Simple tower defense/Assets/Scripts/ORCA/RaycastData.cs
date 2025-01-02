using Unity.Mathematics;

namespace Dots.RVO {
    [System.Flags]
    public enum RaycastFilter {
        NONE = 0,
        AGENTS = 1,
        OBSTACLE_STATIC = 2,
        OBSTACLE_DYNAMIC = 4,
        OBSTACLES = OBSTACLE_STATIC | OBSTACLE_DYNAMIC,
        ANY = AGENTS | OBSTACLES
    }

    public struct RaycastData {
        public float2 position;
        public float3 worldPosition;
        public float baseline;
        public float2 direction;
        public float3 worldDir;
        public float distance;
        public ORCALayer layerIgnore;
        public RaycastFilter filter;
        public bool twoSided;
    }

    public struct RaycastResult {
        //public Entity entity;
        public int hitAgent;
        public float3 hitAgentLocation;
        public float2 hitAgentLocation2D;

        public bool dynamicObstacle;
        public int hitObstacle;
        public float3 hitObstacleLocation;
        public float2 hitObstacleLocation2D;
        public int ObstacleVertexA;
        public int ObstacleVertexB;
    }
}
