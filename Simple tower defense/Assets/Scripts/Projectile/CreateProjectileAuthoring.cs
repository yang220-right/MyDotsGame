using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace YY.Projectile {
    public class CreateProjectileAuthoring : MonoBehaviour {
        public List<GameObject> ProjectilePrefab;
    }
    public partial class CreateProjectileBaker : Baker<CreateProjectileAuthoring> {
        public override void Bake(CreateProjectileAuthoring authoring) {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new ProjectilePrefabData
            {
                MachineGunBaseProjectilePrefab = GetEntity(authoring.ProjectilePrefab[0], TransformUsageFlags.Dynamic),
                MortorProjectPrefab = GetEntity(authoring.ProjectilePrefab[1], TransformUsageFlags.Dynamic),
            });
        }
    }
    public partial struct ProjectilePrefabData : IComponentData {
        public Entity MachineGunBaseProjectilePrefab;
        public Entity MortorProjectPrefab;
    }
}
