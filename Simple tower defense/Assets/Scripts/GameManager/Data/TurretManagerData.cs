using Unity.Entities;
using Unity.Mathematics;

public partial struct TurretPrefabData : IComponentData {
    public Entity CorePrefab;
    public Entity MachineGunBasePrefab;
}
public enum TurretType {
    Core,
    MachineGun,
}
public partial struct CreateTurretBuffer : IBufferElementData {
    public TurretType type;
    public int Num;
    public float3 Pos;
}

