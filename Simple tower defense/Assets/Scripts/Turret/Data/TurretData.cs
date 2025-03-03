using Unity.Entities;
using Unity.Mathematics;

public partial struct TurretPrefabData : IComponentData {
    public Entity CorePrefab;
    public Entity MachineGunBasePrefab;
    public Entity FireTowersPrefab;
    public Entity MortorTowersPrefab;
    public Entity SniperTowersPrefab;
    public Entity GuideTowerPrefab;
    public Entity PlagueTowersPrefab;
}
public enum TurretType {
    Core,
    GunTowers,
    FireTowers,
    MortorTowers,
    SniperTowers,
    GuideTowers,
    PlagueTowers,

}
public partial struct CreateTurretBuffer : IBufferElementData {
    public TurretType type;
    public int Num;
    public float3 Pos;
}

