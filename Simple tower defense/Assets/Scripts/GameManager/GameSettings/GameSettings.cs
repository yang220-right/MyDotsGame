using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(menuName = "Settigs/GameSettings")]
public class GameSettings : ScriptableObject {
    public float GeneratorEnemyPerSeconds;
    public int MapColumn;
    public int MapRow;
}
