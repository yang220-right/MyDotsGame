using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using System;
using YY;

namespace Dots.RVO {
    [System.Serializable]
    public struct ObstacleDef {
        public bool edge;
        public bool inverse;
        public float thickness;
        public List<float3> vertices;
    }

    public interface IObstacleGroup {
        int Count { get; }

        Obstacle this[int i] { get; }
    }

    public class ObstacleGroup : IObstacleGroup {
        private List<Obstacle> m_obstacles = new List<Obstacle>();

        public int Count => m_obstacles.Count;

        public Obstacle this[int i] {
            get { return m_obstacles[i]; }
        }

        public Obstacle Add(Obstacle obstacle) {
            if (m_obstacles.Contains(obstacle)) {
                return obstacle;
            }
            m_obstacles.Add(obstacle);
            return obstacle;
        }

        public void RemoveFirst() {
            if (m_obstacles.Count > 0) {
                m_obstacles.RemoveAt(0);
            }
        }

        public void Clean() {
            m_obstacles.Clear();
        }

        public Obstacle Add(List<float3> vertices, bool inverseOrder = false, float maxSegmentLength = 0f) {
            Obstacle obstacle = new Obstacle();
            int count = vertices.Count;
            if (!inverseOrder) {
                for (int i = 0; i < count; i++) {
                    obstacle.Add(vertices[i]);
                }
            } else {
                for (int i = count - 1; i >= 0; i--) {
                    obstacle.Add(vertices[i]);
                }
            }
            int lastIndex = obstacle.Vertices.Count - 1;
            if (math.distancesq(obstacle.Vertices[lastIndex], obstacle.Vertices[0]) != 0.0f) {
                obstacle.Add(obstacle.Vertices[0]);
            }
            if (maxSegmentLength > 0.0f) {
                obstacle.Subdivide(maxSegmentLength);
            }
            return Add(obstacle);
        }
    }

    [Serializable]
    public class Obstacle {
        private ORCALayer m_layerOccupation = ORCALayer.ANY;
        private bool m_collisionEnabled = true;
        private float m_thickness = 0.0f;
        private float m_height = 1.0f;
        private float m_baseline = 0.0f;
        private bool m_edge = false;


        private List<float3> m_vertices = new List<float3>();

        public List<float3> Vertices => m_vertices;

        /// <summary>
        /// Which layer this Obstacle occupies within the simulation.
        /// This define whether an Agent will account for this obstacle or not based on its own layerOccupation.
        /// </summary>
        public ORCALayer layerOccupation {
            get { return m_layerOccupation; }
            set { m_layerOccupation = value; }
        }
        /// <summary>
        /// Whether the collision is enabled or not for this Obstacle.
        /// If True, the agents will avoid it, otherwise ignore it.
        /// </summary>
        public bool collisionEnabled {
            get { return m_collisionEnabled; }
            set { m_collisionEnabled = value; }
        }
        /// <summary>
        /// The thickness of the obstacle' line, in both directions.
        /// </summary>
        public float thickness {
            get { return m_thickness; }
            set { m_thickness = value; }
        }
        /// <summary>
        /// The height of the obstacle. Used by the simulation to check whether an Agent would collide with that obstacle or not based
        /// on both the Obstacle & Agent baseline & height.
        /// </summary>
        public float height {
            get { return m_height; }
            set { m_height = value; }
        }
        /// <summary>
        /// The vertical position of the Obstacle (Z if in XY plane, otherwise Y)
        /// </summary>
        public float baseline {
            get { return m_baseline; }
            set { m_baseline = value; }
        }
        /// <summary>
        /// If true, reat obstacle as an open group of line instead of a closed polygon
        /// </summary>
        public bool edge {
            get { return m_edge; }
            set { m_edge = value; }
        }


        public ObstacleInfos infos {
            get {
                return new ObstacleInfos()
                {
                    length = m_vertices.Count,
                    layerOccupation = m_layerOccupation,
                    collisionEnabled = m_collisionEnabled,
                    edge = m_collisionEnabled,
                    thickness = m_thickness,
                    baseline = m_baseline,
                    height = m_height
                };
            }
        }

        public float3 Add(float3 v) {
            if (m_vertices.Contains(v)) {
                return v;
            }
            m_vertices.Add(v);
            return v;
        }

        public void Subdivide(float subSegmentLength) {
            int count = m_vertices.Count - 1, insertIndex = 0;
            for (int i = 0; i < count; i++) {
                float3 v = m_vertices[insertIndex], v_next = m_vertices[insertIndex + 1];
                float dist = math.distance(v, v_next), subDist = dist / subSegmentLength;
                insertIndex++;
                if (subDist <= 1.0f) {
                    continue;
                }
                int steps = ((int) math.ceil(dist / subSegmentLength)) - 1;
                float3 dir = math.normalize(v_next - v);
                v = v + dir * ((dist - ((steps - 1) * subSegmentLength)) * 0.5f);
                for (int j = 0; j < steps; j++) {
                    Insert(insertIndex, v + dir * (j * subSegmentLength));
                    insertIndex++;
                }
            }
        }

        public float3 Insert(int index, float3 v) {
            float3 vert = v;
            int currentIndex = m_vertices.IndexOf(v);
            if (currentIndex == index) {
                return vert;
            }
            if (currentIndex != -1) {
                m_vertices.RemoveAt(currentIndex);
                if (currentIndex < index)
                    m_vertices.Insert(index - 1, vert);
                else
                    m_vertices.Insert(index, vert);
            } else {
                //Add vertex
                m_vertices.Insert(index, vert);
            }
            return vert;
        }


        public void Init() {
            m_layerOccupation = ORCALayer.ANY;
            m_collisionEnabled = true;
            m_thickness = 0.0f;
            m_height = 1.0f;
            m_baseline = 0.0f;
            m_edge = false;
        }
    }

    public class RVOComponent : MonoBehaviour {
        public static RVOComponent Ins;
        private void Awake() {
            Ins = this;
        }
        [SerializeField] private List<ObstacleDef> staticObstacles;

        [SerializeField] private List<ObstacleDef> dynamicObStacles;

        private ObstacleGroup m_staticObstacles = new ObstacleGroup();

        private ObstacleGroup m_dynamicObstacles = new ObstacleGroup();

        private NativeArray<ObstacleInfos> m_StaticOutputObstacleInfos = default;
        private NativeArray<ObstacleVertexData> m_StaticReferenceObstacles = default;
        private NativeArray<ObstacleVertexData> m_StaticOutputObstacles = default;


        private NativeArray<ObstacleInfos> m_DynamicOutputObstacleInfos = default;
        private NativeArray<ObstacleVertexData> m_DynamicReferenceObstacles = default;
        private NativeArray<ObstacleVertexData> m_DynamicOutputObstacles = default;

        public NativeArray<RaycastData> m_RaycastDatas;

        private NativeArray<RaycastResult> m_RaycastResults;

        private bool m_Recompute;


        public List<ObstacleDef> StaticObstacles {
            get { return staticObstacles; }
            set { staticObstacles = value; }
        }

        public List<ObstacleDef> DynamicObStacles {
            get { return dynamicObStacles; }
            set { dynamicObStacles = value; }
        }

        public NativeArray<ObstacleInfos> DynamicOutputObstacleInfos {
            get { return m_DynamicOutputObstacleInfos; }
        }
        public NativeArray<ObstacleVertexData> DynamicReferenceObstacles {
            get { return m_DynamicReferenceObstacles; }
        }
        public NativeArray<ObstacleVertexData> DynamicOutputObstacles {
            get { return m_DynamicOutputObstacles; }
        }


        public NativeArray<ObstacleInfos> StaticOutputObstacleInfos {
            get { return m_StaticOutputObstacleInfos; }
        }
        public NativeArray<ObstacleVertexData> StaticReferenceObstacles {
            get { return m_StaticReferenceObstacles; }
        }
        public NativeArray<ObstacleVertexData> StaticOutputObstacles {
            get { return m_StaticOutputObstacles; }
        }

        public NativeArray<RaycastData> RaycastDatas {
            get { return m_RaycastDatas; }
            set { m_RaycastDatas = value; }
        }

        public NativeArray<RaycastResult> RaycastResults {
            get { return m_RaycastResults; }
            set { m_RaycastResults = value; }
        }

        public bool Recompute {
            get { return m_Recompute; }
        }

        private void Start() {
            Init();
            if (staticObstacles != null) {
                for (int i = 0; i < staticObstacles.Count; i++) {
                    ObstacleDef def = staticObstacles[i];
                    Obstacle o = m_staticObstacles.Add(def.vertices, def.inverse);
                    o.Init();
                    o.edge = def.edge;
                    o.thickness = def.thickness;
                }
            }
            if (dynamicObStacles != null) {
                for (int i = 0; i < dynamicObStacles.Count; i++) {
                    ObstacleDef def = dynamicObStacles[i];
                    Obstacle o = m_dynamicObstacles.Add(def.vertices, def.inverse);
                    o.Init();
                    o.edge = def.edge;
                    o.thickness = def.thickness;
                }
            }
            InitObstacles();
        }

        public void Init() {
            StaticObstacles.Add(new ObstacleDef
            {
                edge = true,
                inverse = false,
                thickness = 1,
                vertices = new List<float3>{
                    new float3(-9, 0, -10000),
                    new float3(-9, 0, 100),
                },
            });
            StaticObstacles.Add(new ObstacleDef
            {
                edge = true,
                inverse = true,
                thickness = 1,
                vertices = new List<float3>{
                    new float3(9, 0, -10000),
                    new float3(9, 0, 100),
                },
            });
        }

        public void InitObstacles() {
            if (staticObstacles != null) {
                for (int i = 0; i < staticObstacles.Count; i++) {
                    ObstacleDef def = staticObstacles[i];
                    Obstacle o = m_staticObstacles.Add(def.vertices, def.inverse);
                    o.Init();
                    o.edge = def.edge;
                    o.thickness = def.thickness;
                }
            }
        }

        public void AddDynamicObstacles(ObstacleDef def) {
            Obstacle o = m_dynamicObstacles.Add(def.vertices, def.inverse);
            o.Init();
            o.edge = def.edge;
            o.thickness = def.thickness;
        }

        public void RemoveDynamicFirst() {
            if (m_dynamicObstacles.Count > 0) {
                m_dynamicObstacles.RemoveFirst();
            }
        }

        public void CleanDynamic() {
            m_dynamicObstacles.Clean();
        }


        public void SetHeroDynamicObStacles(float3 start, float3 end) {
            m_dynamicObstacles[0].Vertices[0] = start;
            m_dynamicObstacles[0].Vertices[1] = end;
            m_dynamicObstacles[0].Vertices[2] = end + new float3(0, 0, 0.1f);
            m_dynamicObstacles[0].Vertices[3] = start + new float3(0, 0, 0.1f);
        }

        /// <summary>
        /// 准备静态数据
        /// </summary>
        public void PrepareStatic() {
            // for (int i = 0; i < m_staticObstacles.Count; i++){
            //
            //     DrawDebug(m_staticObstacles[i]);
            // }
            int obsCount = staticObstacles == null ? 0 : staticObstacles.Count;
            int refCount = m_StaticReferenceObstacles.Length;
            int vCount = 0;
            m_Recompute = !MakeLength(ref m_StaticOutputObstacleInfos, obsCount);
            m_Recompute = true;
            Obstacle o;
            ObstacleInfos infos;
            for (int i = 0; i < obsCount; i++) {
                o = m_staticObstacles[i];
                infos = o.infos;
                infos.index = i;
                infos.start = vCount;
                m_StaticOutputObstacleInfos[i] = infos;
                vCount += infos.length;
            }
            if (!m_Recompute) {
                if (refCount != vCount) {
                    m_Recompute = true;
                } else {
                    return;
                }
            }
            MakeLength(ref m_StaticReferenceObstacles, vCount);
            MakeLength(ref m_StaticOutputObstacles, vCount);
            ObstacleVertexData oData;
            int gIndex = 0, index = 0, vCountMinusOne, firstIndex, lastIndex;
            for (int i = 0; i < obsCount; i++) {
                o = m_staticObstacles[i];
                vCount = o.Vertices.Count;
                vCountMinusOne = vCount - 1;
                firstIndex = gIndex;
                lastIndex = gIndex + vCountMinusOne;
                if (!o.edge) {
                    //Obstacle is a closed polygon
                    for (int v = 0; v < vCount; v++) {
                        oData = new ObstacleVertexData()
                        {
                            infos = i,
                            index = index,
                            pos = new float2(o.Vertices[v].x, o.Vertices[v].z),
                            prev = v == 0 ? lastIndex : index - 1,
                            next = v == vCountMinusOne ? firstIndex : index + 1
                        };
                        m_StaticReferenceObstacles[index++] = oData;
                    }
                } else {
                    //Obstacle is an open path
                    for (int v = 0; v < vCount; v++) {
                        oData = new ObstacleVertexData()
                        {
                            infos = i,
                            index = index,
                            pos = new float2(o.Vertices[v].x, o.Vertices[v].z),
                            prev = v == 0 ? index : index - 1,
                            next = v == vCountMinusOne ? index : index + 1
                        };
                        m_StaticReferenceObstacles[index++] = oData;
                    }
                }
                gIndex += vCount;
            }
            m_StaticReferenceObstacles.CopyTo(m_StaticOutputObstacles);
        }

        public void PrepareDynamic() {
            // for (int i = 0; i < m_dynamicObstacles.Count; i++){
            //     DrawDebug(m_dynamicObstacles[i]);
            // }
            int obsCount = m_dynamicObstacles == null ? 0 : m_dynamicObstacles.Count;
            int refCount = m_DynamicReferenceObstacles.Length;
            int vCount = 0;
            m_Recompute = !MakeLength(ref m_DynamicOutputObstacleInfos, obsCount);
            m_Recompute = true;
            Obstacle o;
            ObstacleInfos infos;
            for (int i = 0; i < obsCount; i++) {
                o = m_dynamicObstacles[i];
                infos = o.infos;
                infos.index = i;
                infos.start = vCount;
                m_DynamicOutputObstacleInfos[i] = infos;
                vCount += infos.length;
            }
            if (!m_Recompute) {
                if (refCount != vCount) {
                    m_Recompute = true;
                } else {
                    return;
                }
            }
            MakeLength(ref m_DynamicReferenceObstacles, vCount);
            MakeLength(ref m_DynamicOutputObstacles, vCount);
            ObstacleVertexData oData;
            int gIndex = 0, index = 0, vCountMinusOne, firstIndex, lastIndex;
            for (int i = 0; i < obsCount; i++) {
                o = m_dynamicObstacles[i];
                vCount = o.Vertices.Count;
                vCountMinusOne = vCount - 1;
                firstIndex = gIndex;
                lastIndex = gIndex + vCountMinusOne;
                if (!o.edge) {
                    //Obstacle is a closed polygon
                    for (int v = 0; v < vCount; v++) {
                        oData = new ObstacleVertexData()
                        {
                            infos = i,
                            index = index,
                            pos = new float2(o.Vertices[v].x, o.Vertices[v].z),
                            prev = v == 0 ? lastIndex : index - 1,
                            next = v == vCountMinusOne ? firstIndex : index + 1
                        };
                        m_DynamicReferenceObstacles[index++] = oData;
                    }
                } else {
                    //Obstacle is an open path
                    for (int v = 0; v < vCount; v++) {
                        oData = new ObstacleVertexData()
                        {
                            infos = i,
                            index = index,
                            pos = new float2(o.Vertices[v].x, o.Vertices[v].z),
                            prev = v == 0 ? index : index - 1,
                            next = v == vCountMinusOne ? index : index + 1
                        };
                        m_DynamicReferenceObstacles[index++] = oData;
                    }
                }
                gIndex += vCount;
            }
            m_DynamicReferenceObstacles.CopyTo(m_DynamicOutputObstacles);
        }

        private void DrawDebug(Obstacle def) {
#if UNITY_EDITOR
            if (def.Vertices == null || def.Vertices.Count <= 1) {
                return;
            }
            int vCount = def.Vertices.Count;
            float3 offset = transform.position;

            //Draw each segment
            for (int j = 1, count = vCount; j < count; j++) {
                Debug.DrawLine(def.Vertices[j - 1] + offset, def.Vertices[j] + offset, Color.red);
            }
            if (!def.edge)
                Debug.DrawLine(def.Vertices[vCount - 1] + offset, def.Vertices[0] + offset, Color.red);
#endif
        }

        public void PrepareRaycast() {
            // DrawRay();
            MakeLength(ref m_RaycastResults, m_RaycastDatas.Length);
        }


        private void DrawRay() {
#if UNITY_EDITOR
            for (int i = 0; i < m_RaycastDatas.Length; i++) {
                var raycastData = m_RaycastDatas[i];
                float2 pos = raycastData.position + raycastData.direction * raycastData.distance;
                Debug.DrawLine(new Vector3(raycastData.position.x, 0, raycastData.position.y),
                    new Vector3(pos.x, 0, pos.y), Color.red);
            }
#endif
            if (m_RaycastResults.Length > 0) {
                for (int i = 0; i < m_RaycastResults.Length; i++) {
                    Debug.Log("击中agent" + m_RaycastResults[i].hitAgent);
                    // Debug.Log("击中agent"+m_RaycastResults[i]);
                }
            }
        }


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

        private void OnDestroy() {
            m_StaticOutputObstacleInfos.Dispose();
            m_StaticReferenceObstacles.Dispose();
            m_StaticOutputObstacles.Dispose();
            m_DynamicOutputObstacleInfos.Dispose();
            m_DynamicReferenceObstacles.Dispose();
            m_DynamicOutputObstacles.Dispose();
            m_RaycastDatas.Dispose();
            m_RaycastResults.Dispose();
        }
    }
}
