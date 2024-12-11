using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using YY.MainGame;

public partial class FFManagerSystemBase : SystemBase {
    protected override void OnCreate() {
        RequireForUpdate<FFControllerData>();
    }
    protected override void OnUpdate() {
        var data = SystemAPI.GetSingleton<FFControllerData>();
        var entity = SystemAPI.GetSingletonEntity<FFControllerData>();
        if (Input.GetKeyDown(KeyCode.Alpha1)) {
            data.BeginInit = true;
            data.Column = 10;
            data.Row = 10;
            EntityManager.SetComponentData(entity, data);
        }
        if (Input.GetKeyDown(KeyCode.Alpha2)) {
            EntityManager.AddBuffer<FFPosBuffer>(entity).Add(new FFPosBuffer()
            {
                Pos = new int2(2, 5)
            });
        }
        if (Input.GetKeyDown(KeyCode.Alpha3)) {
            foreach (var item in data.allMapData) {
                Debug.Log($"{item.Pos} -- {item.Value}");
            }
        }
        MonoGameManager.Ins.cb = () => {
            var style = new GUIStyle();
            style.normal.textColor = Color.red;
            foreach (var item in data.allMapData) {
                Handles.Label(new float3(item.Pos.x, 0, item.Pos.y), $"{item.Value}", style);
            }
        };
    }
}
