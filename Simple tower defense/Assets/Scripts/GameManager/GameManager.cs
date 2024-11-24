using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace YY.MainGame {
    public class GameManager : MonoBehaviour {

    }
    public class GameManagerBaker : Baker<GameManager> {
        public override void Bake(GameManager authoring) {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new GameControllerData
            {
                Type = GameStateType.Begin,
            });
        }
    }
}