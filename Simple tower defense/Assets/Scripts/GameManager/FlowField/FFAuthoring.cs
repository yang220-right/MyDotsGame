using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace YY.MainGame {
    public class FFAuthoring : MonoBehaviour {

    }
    public partial class FFBaker : Baker<FFAuthoring> {
        public override void Bake(FFAuthoring authoring) {
            var e = GetEntity(TransformUsageFlags.None);
            var data = new FFControllerData();
            data.BeginInit = false;
            data.EndInit = false;
            data.NeedUpdate = false;
            AddComponent(e,data);
            AddBuffer<FFPosBuffer>(e);
        }
    }
}