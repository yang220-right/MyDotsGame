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
            MachineGunBasePrefab = GetEntity(authoring.TurretPrefab[1], TransformUsageFlags.Dynamic),
            FireTowersPrefab = GetEntity(authoring.TurretPrefab[2], TransformUsageFlags.Dynamic),
            MortorTowersPrefab = GetEntity(authoring.TurretPrefab[3], TransformUsageFlags.Dynamic),
            SniperTowersPrefab = GetEntity(authoring.TurretPrefab[4], TransformUsageFlags.Dynamic),
            GuideTowerPrefab = GetEntity(authoring.TurretPrefab[5], TransformUsageFlags.Dynamic),
            PlagueTowersPrefab = GetEntity(authoring.TurretPrefab[6], TransformUsageFlags.Dynamic),
        });
        AddComponent<CreateTurretBuffer>(e);
    }
}
