using Unity.Entities;
using UnityEngine;

public partial class TurretManagerSystemBase : SystemBase {
    protected override void OnUpdate() {
        if (Input.GetMouseButtonDown(0)) {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up, 0).Raycast(ray, out var dis)) {
                var pos = ray.GetPoint(dis);
                var baseData = SystemAPI.GetSingletonBuffer<CreateTurretBuffer>();
                baseData.Add(new CreateTurretBuffer
                {
                    type = TurretType.MachineGun,
                    Num = 1,
                    Pos = pos,
                });
            }
        }
    }
}
