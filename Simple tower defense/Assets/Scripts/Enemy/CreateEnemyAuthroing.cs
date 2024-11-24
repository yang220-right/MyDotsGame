using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using YY.MainGame;

namespace YY.Enemy {
    public class CreateEnemyAuthroing : MonoBehaviour {
        public List<GameObject> EnemyList;
        public Vector3 GeneratorPos;
    }
    public partial class CreateEnemyBaker : Baker<CreateEnemyAuthroing> {
        public override void Bake(CreateEnemyAuthroing authoring) {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent<BaseEnemyData>(e);
            AddComponent(e, new EnemyPrefabData
            {
                BaseCubePrefab = GetEntity(authoring.EnemyList[0], TransformUsageFlags.Dynamic)
            });
            AddComponent<CreateEnemyBuffer>(e);
        }
    }

    public partial struct EnemyPrefabData : IComponentData {
        public Entity BaseCubePrefab;
    }
}