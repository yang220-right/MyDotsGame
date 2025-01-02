using Unity.Burst;
using Unity.Mathematics;

namespace Dots.RVO {
    public struct ObstacleInfos {
        public int index;
        public int start;
        public int length;
        public ORCALayer layerOccupation;
        public bool collisionEnabled;
        public bool edge;
        public float thickness;
        public float baseline;
        public float height;
    }


    public struct ObstacleVertexData {
        public int infos;
        public int index;
        public int next;
        public int prev;
        public bool convex;
        public float2 pos;
        public float2 dir;
    }


    public struct ObstacleTreeNode {
        public const int MAX_LEAF_SIZE = 10;

        public int index;
        public int vertex;
        public int left;
        public int right;

        public int begin;
        public int end;

        public float maxX;
        public float maxY;
        public float minX;
        public float minY;
    }
}
