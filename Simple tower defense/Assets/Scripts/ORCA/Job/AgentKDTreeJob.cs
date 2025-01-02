using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Mathematics.math;

namespace Dots.RVO {
    [BurstCompile]
    public struct AgentKDTreeJob : IJob {
        public NativeArray<AgentData> m_inputAgents;
        public NativeArray<AgentTreeNode> m_outputTree;

        [BurstCompile]
        public void Execute() {
            int agentCount = m_inputAgents.Length;
            if (agentCount == 0) {
                return;
            }
            BuildAgentTreeRecursive(0, agentCount, 0);
        }

        [BurstCompile]
        private void BuildAgentTreeRecursive(int begin, int end, int node) {
            AgentTreeNode treeNode = m_outputTree[node];
            AgentData agent = m_inputAgents[begin];
            float2 pos;
            treeNode.begin = begin;
            treeNode.end = end;
            treeNode.minX = treeNode.maxX = agent.position.x;
            treeNode.minY = treeNode.maxY = agent.position.y;
            for (int i = begin + 1; i < end; ++i) {
                pos = m_inputAgents[i].position;
                treeNode.maxX = max(treeNode.maxX, pos.x);
                treeNode.minX = min(treeNode.minX, pos.x);
                treeNode.maxY = max(treeNode.maxY, pos.y);
                treeNode.minY = min(treeNode.minY, pos.y);
            }
            m_outputTree[node] = treeNode;
            if (end - begin > AgentTreeNode.MAX_LEAF_SIZE) {
                // No leaf node.
                bool isVertical = treeNode.maxX - treeNode.minX > treeNode.maxY - treeNode.minY;
                float splitValue = 0.5f * (isVertical ? treeNode.maxX + treeNode.minX : treeNode.maxY + treeNode.minY);
                int left = begin;
                int right = end;
                while (left < right) {
                    while (left < right &&
                           (isVertical ? m_inputAgents[left].position.x : m_inputAgents[left].position.y) < splitValue) {
                        ++left;
                    }
                    while (right > left &&
                           (isVertical ? m_inputAgents[right - 1].position.x : m_inputAgents[right - 1].position.y) >=
                           splitValue) {
                        --right;
                    }
                    if (left < right) {
                        AgentData tempAgent = m_inputAgents[left], rep = m_inputAgents[right - 1];
                        tempAgent.kdIndex = right - 1;
                        rep.kdIndex = left;
                        m_inputAgents[left] = rep;
                        m_inputAgents[right - 1] = tempAgent;
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
    }
}
