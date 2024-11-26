using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using YY.MainGame;
using YY.Projectile;
using YY.Turret;

public partial class TurretManagerSystemBase : SystemBase {
    protected override void OnCreate() {
        RequireForUpdate<GameControllerData>();
    }
    protected override void OnUpdate() {
        var data = SystemAPI.GetSingletonRW<GameControllerData>();
        if (data.ValueRO.Type == GameStateType.Over) return;

        if (Input.GetMouseButtonDown(0)) {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up, 0).Raycast(ray, out var dis)) {
                var pos = ray.GetPoint(dis);
                var baseData = SystemAPI.GetSingletonBuffer<CreateTurretBuffer>();
                baseData.Add(new CreateTurretBuffer
                {
                    type = TurretType.FireTowers,
                    Num = 1,
                    Pos = pos,
                });
            }
        }
    }
}
