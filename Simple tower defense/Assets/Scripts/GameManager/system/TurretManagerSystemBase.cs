using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using YY.Projectile;
using YY.Turret;

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


        if (Input.GetMouseButtonDown(1)) {
            var coreData = SystemAPI.GetSingletonEntity<TurretBaseCoreData>();
            EntityManager.AddBuffer<CreateProjectBuffer>(coreData).Add(new CreateProjectBuffer
            {
                Type = ProjectileType.MachineGunBaseProjectile,
                MoveDir = math.left(),
                EndPos = new float3(-10, 0, 0),
                StartPos = float3.zero,
                Speed = 2
            });
        }

    }
}
