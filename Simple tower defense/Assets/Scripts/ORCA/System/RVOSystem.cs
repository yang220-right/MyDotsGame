using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System;

namespace Dots.RVO {
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct RVOSystem : ISystem {
        private int Muilt;

        private NativeArray<AgentData> m_AgentsData;
        private NativeArray<AgentTreeNode> m_AgentTreeNode;

        private NativeArray<AgentDataResult> m_ResultsData;

        private NativeArray<AgentData> m_FixAgentData;
        private NativeArray<AgentData> m_OutAgentData;


        public NativeArray<ObstacleTreeNode> m_staticObstacleTree;

        public NativeArray<ObstacleTreeNode> m_dynObstacleTree;


        private EntityQuery m_AgentQuery;

        private JobHandle m_CurrentJobHandle;

        public float m_MaxRadius;

        private readonly static float3 m_DefauleOffsetDynStart = new float3(-12, 0, -4);
        private readonly static float3 m_DefauleOffsetDynEnd = new float3(12, 0, -4);

        private bool m_IsInit;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<AgentData>();
            m_MaxRadius = 0;
            m_IsInit = false;
            m_AgentQuery = new EntityQueryBuilder(Allocator.Temp).WithAllRW<AgentData>().Build(ref state);
        }

        /// <summary>
        /// 更新数据
        /// </summary>
        private void NewNativeArray() {
            m_AgentsData = m_AgentQuery.ToComponentDataArray<AgentData>(Allocator.TempJob);
            m_ResultsData = new NativeArray<AgentDataResult>(m_AgentsData.Length, Allocator.TempJob);
            m_FixAgentData = new NativeArray<AgentData>(m_AgentsData.Length, Allocator.TempJob);
            m_OutAgentData = new NativeArray<AgentData>(m_AgentsData.Length, Allocator.TempJob);
            m_AgentTreeNode = new NativeArray<AgentTreeNode>(2 * m_AgentsData.Length, Allocator.TempJob);
        }

        private void DisposeNative() {
            m_AgentsData.Dispose();
            m_ResultsData.Dispose();
            m_FixAgentData.Dispose();
            m_OutAgentData.Dispose();
            m_AgentTreeNode.Dispose();
            m_staticObstacleTree.Dispose();
            m_dynObstacleTree.Dispose();
        }

        [BurstCompile]
        public bool MakeLength<T>(ref NativeArray<T> nativeArray, int length, Allocator alloc = Allocator.Persistent)
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
        public bool MakeAgentData(
            ref NativeArray<AgentData> nativeArray, EntityQuery query, Allocator alloc = Allocator.Persistent
        ) {
            if (!nativeArray.IsCreated
                || nativeArray.Length != query.CalculateEntityCount()) {
                nativeArray.Dispose();
                nativeArray = query.ToComponentDataArray<AgentData>(alloc);
                return false;
            }
            return true;
        }
        /// <summary>
        /// 尝试完成
        /// </summary>
        /// <param name="state"></param>
        private void TryComplete(ref SystemState state) {
            NewNativeArray();
            Muilt = m_AgentsData.Length / 100;
            if (Muilt < 8) {
                Muilt = 8;
            }
            RVOComponent.Ins.PrepareStatic();
            RVOComponent.Ins.PrepareDynamic();
            RVOComponent.Ins.PrepareRaycast();
            m_staticObstacleTree =
                new NativeArray<ObstacleTreeNode>(2 * RVOComponent.Ins.StaticReferenceObstacles.Length,
                    Allocator.TempJob);
            m_dynObstacleTree = new NativeArray<ObstacleTreeNode>(2 * RVOComponent.Ins.DynamicReferenceObstacles.Length,
                Allocator.TempJob);
            var setDataJob = new SetAgentDataJobEntity
            {
                //  m_plane = m_Plane,
                m_inputAgents = m_AgentsData,
                m_FixAgents = m_FixAgentData,
            };
            var setDataJobDependency = setDataJob.ScheduleParallel(state.Dependency);
            var kdJob = new AgentKDTreeJob
            {
                m_inputAgents = setDataJob.m_inputAgents,
                m_outputTree = m_AgentTreeNode,
            };
            var kdJobDependency = kdJob.Schedule(setDataJobDependency);
            var staticObstacleOrientation = new ObstacleOrientationJob
            {
                m_recompute = RVOComponent.Ins.Recompute,
                m_inputObstacleInfos = RVOComponent.Ins.StaticOutputObstacleInfos,
                m_referenceObstacles = RVOComponent.Ins.StaticReferenceObstacles,
                m_inputObstacles = RVOComponent.Ins.StaticOutputObstacles,
            };
            var staticobstacleOrientationDependency =
                staticObstacleOrientation.Schedule(RVOComponent.Ins.StaticOutputObstacles.Length, 8, state.Dependency);
            var staticObstacleFix = new ObstacleFixJob
            {
                m_recompute = RVOComponent.Ins.Recompute,
                m_referenceObstacles = RVOComponent.Ins.StaticReferenceObstacles,
                m_inputObstacles = RVOComponent.Ins.StaticOutputObstacles,
            };
            var staticObstacleFixDependency = staticObstacleFix.Schedule(staticobstacleOrientationDependency);
            var staticObstacleKdTreeJob = new ObstacleKDTreeJob
            {
                m_recompute = RVOComponent.Ins.Recompute,
                m_inputObstacleInfos = RVOComponent.Ins.StaticOutputObstacleInfos,
                m_referenceObstacles = RVOComponent.Ins.StaticReferenceObstacles,
                m_inputObstacles = RVOComponent.Ins.StaticOutputObstacles,
                m_outputTree = m_staticObstacleTree,
            };
            var dynObstacleOrientation = new ObstacleOrientationJob
            {
                m_recompute = RVOComponent.Ins.Recompute,
                m_inputObstacleInfos = RVOComponent.Ins.DynamicOutputObstacleInfos,
                m_referenceObstacles = RVOComponent.Ins.DynamicReferenceObstacles,
                m_inputObstacles = RVOComponent.Ins.DynamicOutputObstacles,
            };
            var dynobstacleOrientationDependency =
                dynObstacleOrientation.Schedule(RVOComponent.Ins.DynamicOutputObstacles.Length, 8, state.Dependency);
            var dynObstacleFix = new ObstacleFixJob
            {
                m_recompute = RVOComponent.Ins.Recompute,
                m_referenceObstacles = RVOComponent.Ins.DynamicReferenceObstacles,
                m_inputObstacles = RVOComponent.Ins.DynamicOutputObstacles,
            };
            var dynObstacleFixDependency = dynObstacleFix.Schedule(dynobstacleOrientationDependency);
            var dynObstacleKdTreeJob = new ObstacleKDTreeJob
            {
                m_recompute = RVOComponent.Ins.Recompute,
                m_inputObstacleInfos = RVOComponent.Ins.DynamicOutputObstacleInfos,
                m_referenceObstacles = RVOComponent.Ins.DynamicReferenceObstacles,
                m_inputObstacles = RVOComponent.Ins.DynamicOutputObstacles,
                m_outputTree = m_dynObstacleTree,
            };
            var dynObsKDtreeDependency = dynObstacleKdTreeJob.Schedule(dynObstacleFixDependency);
            var staticObsKDtreeDependency = staticObstacleKdTreeJob.Schedule(staticObstacleFixDependency);
            var kdDependency =
                JobHandle.CombineDependencies(kdJobDependency, staticObsKDtreeDependency, dynObsKDtreeDependency);
            var lineJob = new ORCALinesJob
            {
                m_inputAgents = kdJob.m_inputAgents,
                m_inputAgentTree = kdJob.m_outputTree,
                m_results = m_ResultsData,
                m_OutputAgents = m_OutAgentData,
                m_timestep = SystemAPI.Time.DeltaTime,
                m_staticObstacleInfos = RVOComponent.Ins.StaticOutputObstacleInfos,
                m_staticObstacles = RVOComponent.Ins.StaticOutputObstacles,
                m_staticObstacleTree = m_staticObstacleTree,
                m_staticRefObstacles = RVOComponent.Ins.StaticReferenceObstacles,
                m_dynObstacleInfos = RVOComponent.Ins.DynamicOutputObstacleInfos,
                m_dynObstacles = RVOComponent.Ins.DynamicOutputObstacles,
                m_dynObstacleTree = m_dynObstacleTree,
                m_dynRefObstacles = RVOComponent.Ins.DynamicReferenceObstacles,
            };
            state.Dependency = lineJob.Schedule(m_AgentsData.Length, Muilt, kdDependency);
            var raycastsJob = new RaycastsJob
            {
                m_inputAgents = lineJob.m_OutputAgents,
                m_maxAgentRadius = m_MaxRadius,
                m_inputAgentTree = lineJob.m_inputAgentTree,
                m_staticObstacleInfos = RVOComponent.Ins.StaticOutputObstacleInfos,
                m_staticObstacles = RVOComponent.Ins.StaticOutputObstacles,
                m_staticObstacleTree = m_staticObstacleTree,
                m_staticRefObstacles = RVOComponent.Ins.StaticReferenceObstacles,
                m_dynObstacleInfos = RVOComponent.Ins.DynamicOutputObstacleInfos,
                m_dynObstacles = RVOComponent.Ins.DynamicOutputObstacles,
                m_dynObstacleTree = m_dynObstacleTree,
                m_dynRefObstacles = RVOComponent.Ins.DynamicReferenceObstacles,
                m_inputRaycasts = RVOComponent.Ins.RaycastDatas,
                m_results = RVOComponent.Ins.RaycastResults,
                m_plane = AxisPair.XZ,
            };
            state.Dependency = raycastsJob.Schedule(RVOComponent.Ins.RaycastDatas.Length, 1, state.Dependency);
            var rvoJob = new RVOApplyJob
            {
                m_inputAgentResults = lineJob.m_results,
                m_inputAgents = lineJob.m_OutputAgents,
                m_FixAgents = setDataJob.m_FixAgents,
            };
            state.Dependency = rvoJob.Schedule(m_AgentsData.Length, Muilt * 5, state.Dependency);
            var rvoSetEntityJob = new RVOApplyEntityJob
            {
                m_FixAgents = rvoJob.m_FixAgents,
            };
            rvoSetEntityJob.ScheduleParallel(state.Dependency).Complete();
            DisposeNative();
        }

        public void OnDestory() {
            DisposeNative();
        }

        public void OnUpdate(ref SystemState state) {
            if (!m_IsInit) {
                foreach (var item in SystemAPI.Query<AgentData>().WithOptions(EntityQueryOptions.IncludePrefab)) {
                    m_MaxRadius = math.max(m_MaxRadius, item.radius);
                    m_IsInit = true;
                }
            }
            TryComplete(ref state);
        }


        private partial struct DrawAgentJob : IJobEntity {
            public void Execute(AgentData agentData) {
                Circle(agentData.worldPosition, agentData.radius, Color.red);
            }

            private void DrawAgent(float3 point, float radius) {
                Circle(point, radius, Color.red, 12);
            }

            private void Circle(float3 center, float radius, Color col, int samples = 30) {
                float3 from, to;
                float angleIncrease = (float) (Math.PI * 2) / samples;
                from = to = new float3(center.x + radius * (float)Math.Cos(0.0f), center.y,
                    center.z + radius * (float)Math.Sin(0.0f));
                for (int i = 0; i < samples; i++) {
                    float rad = angleIncrease * (i + 1);
                    to = new float3(center.x + radius * Mathf.Cos(rad), center.y, center.z + radius * Mathf.Sin(rad));
                    Line(from, to, col);
                    from = to;
                }
            }

            private void Line(float3 from, float3 to, Color col) {
                Debug.DrawLine(from, to, col);
            }
        }


        [BurstCompile]
        public partial struct SetAgentDataJobEntity : IJobEntity {
            public NativeArray<AgentData> m_inputAgents;
            public NativeArray<AgentData> m_FixAgents;

            [BurstCompile]
            public void Execute([EntityIndexInQuery] int index, ref AgentData a) {
                a.index = index;
                a.kdIndex = index;
                m_inputAgents[index] = new AgentData()
                {
                    index = index,
                    kdIndex = index,
                    position = math.float2(a.position.x, a.position.y),
                    worldPosition = a.worldPosition,
                    baseline = a.worldPosition.y,
                    prefVelocity = math.float2(a.prefVelocity.x, a.prefVelocity.y),
                    velocity = math.float2(a.velocity.x, a.velocity.y),
                    worldVelocity = a.worldVelocity,
                    height = a.height,
                    radius = a.radius,
                    radiusObst = a.radiusObst,
                    maxSpeed = a.maxSpeed,
                    DefaultMaxSpeed = a.DefaultMaxSpeed,
                    maxNeighbors = a.maxNeighbors,
                    neighborDist = a.neighborDist,
                    neighborElev = a.neighborElev,
                    timeHorizon = a.timeHorizon,
                    timeHorizonObst = a.timeHorizonObst,
                    navigationEnabled = a.navigationEnabled,
                    collisionEnabled = a.collisionEnabled,
                    layerOccupation = a.layerOccupation,
                    layerIgnore = a.layerIgnore,
                    IsSlowly = a.IsSlowly,
                    IsJams = a.IsJams,
                    CurrentJamsTime = a.CurrentJamsTime,
                    DefaultJamsTime = a.DefaultJamsTime,
                };
                m_FixAgents[index] = new AgentData()
                {
                    index = index,
                    kdIndex = index,
                    position = math.float2(a.position.x, a.position.y),
                    worldPosition = a.worldPosition,
                    baseline = a.worldPosition.y,
                    prefVelocity = math.float2(a.prefVelocity.x, a.prefVelocity.y),
                    velocity = math.float2(a.velocity.x, a.velocity.y),
                    worldVelocity = a.worldVelocity,
                    height = a.height,
                    radius = a.radius,
                    radiusObst = a.radiusObst,
                    maxSpeed = a.maxSpeed,
                    DefaultMaxSpeed = a.DefaultMaxSpeed,
                    maxNeighbors = a.maxNeighbors,
                    neighborDist = a.neighborDist,
                    neighborElev = a.neighborElev,
                    timeHorizon = a.timeHorizon,
                    timeHorizonObst = a.timeHorizonObst,
                    navigationEnabled = a.navigationEnabled,
                    collisionEnabled = a.collisionEnabled,
                    layerOccupation = a.layerOccupation,
                    layerIgnore = a.layerIgnore,
                    IsSlowly = a.IsSlowly,
                    IsJams = a.IsJams,
                    CurrentJamsTime = a.CurrentJamsTime,
                    DefaultJamsTime = a.DefaultJamsTime,
                };
            }
        }


        [BurstCompile]
        public partial struct RVOApplyJob : IJobParallelFor {
            public NativeArray<AgentDataResult> m_inputAgentResults;

            public NativeArray<AgentData> m_inputAgents;

            [NativeDisableParallelForRestriction] public NativeArray<AgentData> m_FixAgents;

            [BurstCompile]
            public void Execute(int index) {
                AgentDataResult result = m_inputAgentResults[index];
                AgentData agent = m_inputAgents[index];
                float3 worldPosition = agent.worldPosition, worldVelocity = agent.worldVelocity;
                worldPosition = math.float3(result.position.x, worldPosition.y, result.position.y);
                worldVelocity = math.float3(result.velocity.x, worldVelocity.y, result.velocity.y);
                var vel = new float2(worldVelocity.x, worldVelocity.z);
                agent.worldPosition = worldPosition;
                agent.worldVelocity = worldVelocity;
                agent.velocity = vel;
                agent.position = result.position;
                m_FixAgents[agent.index] = agent;
            }
        }


        [BurstCompile]
        public partial struct RVOApplyEntityJob : IJobEntity {
            [ReadOnly] public NativeArray<AgentData> m_FixAgents;

            [BurstCompile]
            public void Execute(ref AgentData agentData, Entity entity) {
                agentData = m_FixAgents[agentData.index];
                //agentData.entity = entity;
            }
        }
    }
}
