using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using YY.Enemy;
using YY.Turret;

public partial class EnemyManagerSystemBase : SystemBase {
    protected override void OnCreate() {

    }
    uint generatorNum = 0;
    protected override void OnUpdate() {
        //先考虑一个核心的情况
        SystemAPI.TryGetSingletonEntity<BaseCoreTag>(out var core);
        if (core == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) {
            var coreData = EntityManager.GetComponentData<BasicAttributeData>(core);
            var random = Unity.Mathematics.Random.CreateFromIndex(generatorNum);

            float baseDis = 30;
            float minDis = 10;
            float maxDis = 20;
            var baseData = SystemAPI.GetSingletonBuffer<CreateEnemyBuffer>();
            var dir =  random.NextFloat2Direction();
            baseData.Add(new CreateEnemyBuffer
            {
                EnemyType = EnemyType.BaseCube,
                Num = 1,
                Pos = new float3(dir.x, 0, dir.y) * (random.NextFloat(minDis, maxDis) + baseDis),
            });

            ++generatorNum;
        }
    }
}