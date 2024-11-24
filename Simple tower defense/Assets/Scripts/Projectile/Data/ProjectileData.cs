using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace YY.Projectile {
    public enum ProjectileType {
        MachineGunBaseProjectile,
    }

    public partial struct CreateProjectBuffer : IBufferElementData {
        public ProjectileType Type;
        public float3 MoveDir;
        public float3 EndPos;
        public float3 StartPos;
        public float Speed;
    }
    public partial struct ProjectileData : IComponentData {
        public bool Init;
        public float Speed;
        public float3 MoveDir;
        public float3 EndPos;
        public float DeadTime;
    }

}
