using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class CreateTurretAuthoring : MonoBehaviour {
    public List<GameObject> TurretPrefab;
}
public class CreateTurretBaker : Baker<CreateTurretAuthoring> {
    public override void Bake(CreateTurretAuthoring authoring) {
        var e = GetEntity(TransformUsageFlags.None);
        AddComponent(e, new TurretPrefabData()
        {
            CorePrefab = GetEntity(authoring.TurretPrefab[0], TransformUsageFlags.Dynamic),
            MachineGunBasePrefab = GetEntity(authoring.TurretPrefab[1], TransformUsageFlags.Dynamic)
        });
        AddComponent<CreateTurretBuffer>(e);
    }
}
