using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using YY.MainGame;
using YY.Projectile;
using YY.Turret;

public partial class TurretManagerSystemBase : SystemBase {
    private TurretType turretType = TurretType.GunTowers;
    protected override void OnCreate() {
        RequireForUpdate<GameControllerData>();
    }
    protected override void OnUpdate() {
        var data = SystemAPI.GetSingletonRW<GameControllerData>();
        if (data.ValueRO.Type == GameStateType.Over) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) {
            turretType = TurretType.GunTowers;
        }
        if (Input.GetKeyDown(KeyCode.Alpha2)) {
            turretType = TurretType.FireTowers;
        }
        if (Input.GetKeyDown(KeyCode.Alpha3)) {
            turretType = TurretType.MortorTowers;
        }
        if (Input.GetKeyDown(KeyCode.Alpha4)) {
            turretType = TurretType.SniperTowers;
        }
        if (Input.GetKeyDown(KeyCode.Alpha5)) {
            turretType = TurretType.GuideTowers;
        }
        if (Input.GetKeyDown(KeyCode.Alpha6)) {
            turretType = TurretType.PlagueTowers;
        }

        if (Input.GetMouseButtonDown(0)) {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up, 0).Raycast(ray, out var dis)) {
                var pos = ray.GetPoint(dis);
                var baseData = SystemAPI.GetSingletonBuffer<CreateTurretBuffer>();
                baseData.Add(new CreateTurretBuffer
                {
                    type = turretType,
                    Num = 1,
                    Pos = pos,
                });
            }
        }
    }
}
