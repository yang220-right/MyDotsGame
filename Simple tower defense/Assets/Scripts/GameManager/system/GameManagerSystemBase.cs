using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using YY.Enemy;
using YY.MainGame;
using YY.Turret;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class GameManagerSystemBase : SystemBase {
    protected override void OnCreate() {
        RequireForUpdate<GameControllerData>();
    }
    protected override void OnUpdate() {
        var gameControl = SystemAPI.GetSingletonRW<GameControllerData>();
        var createGroup = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CreateBasicAttributeSystemGroup>();
        if (Input.GetKeyDown(KeyCode.P)) {
            gameControl.ValueRW.Type = GameStateType.Over;
            //获取存在的系统
            if (createGroup != null) {
                var handles = createGroup.GetUnmanagedSystems();
                foreach (var item in handles) {
                    ref SystemState redSysState = ref World.Unmanaged.ResolveSystemStateRef(item);
                    redSysState.Enabled = false;
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.B)) {
            gameControl.ValueRW.Type = GameStateType.Begin;
            if (createGroup != null) {
                var handles = createGroup.GetUnmanagedSystems();
                foreach (var item in handles) {
                    ref SystemState redSysState = ref World.Unmanaged.ResolveSystemStateRef(item);
                    redSysState.Enabled = true;
                }
            }
        }
    }
}