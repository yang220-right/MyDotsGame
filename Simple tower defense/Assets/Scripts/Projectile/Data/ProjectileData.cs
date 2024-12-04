using CustomQuadTree;
using Unity.Entities;
using Unity.Mathematics;
using YY.MainGame;

namespace YY.Projectile {
    public enum ProjectileType {
        MachineGunBaseProjectile,
        MortorProjectile
    }
    public enum ProjectileMoveType {
        Linear,
        Curve,
    }
    public struct CreateProjectBufferData {
        public ProjectileType Type;
        public ProjectileMoveType MoveType;
        public float3 StartPos;
        public float3 EndPos;
        public float3 MoveDir;
        public float Speed;


        public float3 ControlePos;//控制点
        public float CurrentTime;
        public float DeadTime;

        public int MaxQueryNum;//最大查询人数
        public BasicAttributeData BasicData;
        public QueryInfo QueryInfo;//查询信息
    }
    public partial struct CreateProjectBuffer : IBufferElementData {
        public ProjectileType Type;
        public ProjectileMoveType MoveType;
        public float3 StartPos;
        public float3 EndPos;
        public float3 MoveDir;
        public float Speed;


        public float3 ControlePos;//控制点
        public float CurrentTime;
        public float DeadTime;

        public int MaxQueryNum;//最大查询人数
        public BasicAttributeData BasicData;
        public QueryInfo QueryInfo;//查询信息
    }
    public partial struct ProjectileData : IComponentData {
        public bool Init;
        public CreateProjectBuffer Data;
    }

}
