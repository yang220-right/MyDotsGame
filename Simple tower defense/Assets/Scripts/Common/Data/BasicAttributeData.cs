using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct BasicAttributeData : IComponentData {
    public bool Init;
    public float MaxHP;
    public float CurrentHP;

    public float CurrentAttack;//攻击力
    public float BaseAttack;//攻击力

    public float CurrentRange;//攻击范围
    public float BaseRange;

    public float CurrentAttackInterval;//攻击间隔 0则为持续攻击
    public float BaseAttackInterval;//攻击间隔 0则为持续攻击
    public float RemainAttackIntervalTime;//剩余攻击间隔时间
    public bool IsBeAttack;

    public float3 CurrentPos;
}
