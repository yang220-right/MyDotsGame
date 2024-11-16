using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct TurretTag : IComponentData { }
public partial struct BaseTurretData : IComponentData {
    public float3 NearEnemy;
}
