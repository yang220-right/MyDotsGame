using Unity.Entities;
using UnityEngine;

public class TurretManagerAuthroing : MonoBehaviour {

}
public class TurretManagerBaker : Baker<TurretManagerAuthroing> {
    public override void Bake(TurretManagerAuthroing authoring) {
        var e = GetEntity(TransformUsageFlags.None);
        AddComponent<TurretManagerTag>(e);
    }
}
public partial struct TurretManagerTag : IComponentData { }