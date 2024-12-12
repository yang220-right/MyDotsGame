using System.Collections;
using Unity.Collections;
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

        if (Input.GetMouseButtonDown(0)) {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up, 0).Raycast(ray, out float dis)) {
                float3 pos = ray.GetPoint(dis);
                pos = math.floor(pos);
                pos += new float3(50, 0, 50);

                if (pos.x < 100 && pos.z < 100) {
                    EntityManager.AddBuffer<FFPosBuffer>(entity).Add(new FFPosBuffer()
                    {
                        Pos = new int2((int)pos.x, (int)pos.z)
                    });
                }
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha3)) {
            var list = new NativeList<int2>(Allocator.Temp);
            for (int i = 0; i < data.NeedCalculateData.Length - 1; i++) {
                list.Add(data.NeedCalculateData[i]);
            }
            data.Reset = true;
            data.NeedCalculateData = list.ToArray(Allocator.TempJob);
            EntityManager.SetComponentData(entity, data);
        }

        //MonoGameManager.Ins.cb = () => {
        //    var style = new GUIStyle();
        //    style.normal.textColor = Color.red;
        //    foreach (var item in data.AllMapData) {
        //        Handles.Label(new float3(item.Pos.x - 50, 0, item.Pos.y - 50), $"{item.Value}", style);
        //    }
        //};
    }
}
