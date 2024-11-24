using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using YY.Enemy;
using YY.Turret;

namespace YY.MainGame {
    public partial class EnemyManagerSystemBase : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<GameControllerData>();
        }
        uint generatorNum = 0;
        float currentTime;
        bool flag;
        protected override void OnUpdate() {
            var data = SystemAPI.GetSingletonRW<GameControllerData>();
            if (data.ValueRO.Type == GameStateType.Over) return;

            //先考虑一个核心的情况
            SystemAPI.TryGetSingletonEntity<BaseCoreTag>(out var core);
            if (core == null) return;
            if (Input.GetKeyDown(KeyCode.F)) {
                flag = !flag;
            }
            if (flag && currentTime <= 0) {
                currentTime = 0.2f;
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

            currentTime -= SystemAPI.Time.DeltaTime;
        }
    }
}