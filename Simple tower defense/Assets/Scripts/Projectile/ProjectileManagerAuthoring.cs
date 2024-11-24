using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

namespace YY.Projectile {
    public class ProjectileManagerAuthoring : MonoBehaviour {
        public List<GameObject> ProjectilePrefab;
    }
    public partial class ProjectileManagerBaker : Baker<ProjectileManagerAuthoring> {
        public override void Bake(ProjectileManagerAuthoring authoring) {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new ProjectilePrefabData
            {
                MachineGunBaseProjectilePrefab = GetEntity(authoring.ProjectilePrefab[0], TransformUsageFlags.Dynamic),
            });
        }
    }
    public partial struct ProjectilePrefabData : IComponentData {
        public Entity MachineGunBaseProjectilePrefab;
    }
}
