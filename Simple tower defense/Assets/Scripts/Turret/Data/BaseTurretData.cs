using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace YY.Turret {
    /// <summary>
    /// 攻击类型
    /// </summary>
    public enum AttackRangeType {
        Single,     //单个
        Box,        //矩形
        Circle,     //原型
        Fans,       //扇形
    }

    public partial struct TurretTag : IComponentData { }
    public partial struct BaseTurretData : IComponentData {
        public float3 NearEnemy;
        public TurretType Type;
        public AttackRangeType AttackType;
    }
}