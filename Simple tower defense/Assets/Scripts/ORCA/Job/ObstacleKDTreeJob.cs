using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Dots.RVO {
    [BurstCompile]
    public struct ObstacleKDTreeJob : IJob {
        private const float EPSILON = 0.00001f;

        public bool m_recompute;

        [ReadOnly] public NativeArray<ObstacleInfos> m_inputObstacleInfos;
        [ReadOnly] public NativeArray<ObstacleVertexData> m_referenceObstacles;
        public NativeArray<ObstacleVertexData> m_inputObstacles;
        public NativeArray<ObstacleTreeNode> m_outputTree;

        [BurstCompile]
        public void Execute() {
            if (!m_recompute) {
                return;
            }
            int obsCount = m_inputObstacles.Length;
            if (obsCount == 0) {
                return;
            }
            BuildAgentTreeRecursive(0, obsCount, 0);
        }

        [BurstCompile]
        private bool MakeLength<T>(ref NativeArray<T> nativeArray, int length, Allocator alloc = Allocator.Persistent)
            where T : unmanaged {
            if (!nativeArray.IsCreated
                || nativeArray.Length != length) {
                nativeArray.Dispose();
                nativeArray = new NativeArray<T>(length, alloc);
                return false;
            }
            return true;
        }

        [BurstCompile]
        private void BuildAgentTreeRecursive(int begin, int end, int node) {
            ObstacleTreeNode treeNode = m_outputTree[node];
            ObstacleVertexData obstacle = m_inputObstacles[begin];
            float2 pos;
            float minX, minY, maxX, maxY;
            treeNode.begin = begin;
            treeNode.end = end;
            minX = maxX = obstacle.pos.x;
            minY = maxY = obstacle.pos.y;
            for (int i = begin + 1; i < end; ++i) {
                pos = m_inputObstacles[i].pos;
                maxX = max(maxX, pos.x);
                minX = min(minX, pos.x);
                maxY = max(maxY, pos.y);
                minY = min(minY, pos.y);
            }
            treeNode.minX = minX;
            treeNode.maxX = maxX;
            treeNode.minY = minY;
            treeNode.maxY = maxY;
            m_outputTree[node] = treeNode;
            if (end - begin > ObstacleTreeNode.MAX_LEAF_SIZE) {
                // No leaf node.
                bool isVertical = treeNode.maxX - treeNode.minX > treeNode.maxY - treeNode.minY;
                float splitValue = 0.5f * (isVertical ? treeNode.maxX + treeNode.minX : treeNode.maxY + treeNode.minY);
                int left = begin;
                int right = end;
                while (left < right) {
                    while (left < right && (isVertical ? m_inputObstacles[left].pos.x : m_inputObstacles[left].pos.y) <
                        splitValue) {
                        ++left;
                    }
                    while (right > left &&
                           (isVertical ? m_inputObstacles[right - 1].pos.x : m_inputObstacles[right - 1].pos.y) >=
                           splitValue) {
                        --right;
                    }
                    if (left < right) {
                        ObstacleVertexData tempAgent = m_inputObstacles[left];
                        m_inputObstacles[left] = m_inputObstacles[right - 1];
                        m_inputObstacles[right - 1] = tempAgent;
                        ++left;
                        --right;
                    }
                }
                int leftSize = left - begin;
                if (leftSize == 0) {
                    ++leftSize;
                    ++left;
                    ++right;
                }
                treeNode.left = node + 1;
                treeNode.right = node + 2 * leftSize;
                m_outputTree[node] = treeNode;
                BuildAgentTreeRecursive(begin, left, treeNode.left);
                BuildAgentTreeRecursive(left, end, treeNode.right);
            }
        }

        private float LeftOf(float2 a, float2 b, float2 c) {
            float x1 = a.x = c.x, y1 = a.y - c.y, x2 = b.x - a.x, y2 = b.y - a.y;
            return x1 * y2 - y1 * x2;
        }

        private float Det(float2 a, float2 b) {
            return a.x * b.y - a.y * b.x;
        }
    }
}
