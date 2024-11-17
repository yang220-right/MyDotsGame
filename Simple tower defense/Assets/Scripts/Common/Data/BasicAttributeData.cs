using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
[Flags]
public enum DataType {
    Turret = 1,     //防御塔
    Enemy = 1 << 1,      //敌人
    Core = 1 << 2,       //核心
}
public partial struct BasicAttributeData : IComponentData {
    public int EntityID;
    public bool Init;
    public DataType Type;
    public float MaxHP;
    public float CurrentHP;

    public float BaseAttack;//攻击力
    public float CurrentAttack;//攻击力

    public float BaseAttackRange;
    public float CurrentAttackRange;//攻击范围

    public float CurrentAttackInterval;//攻击间隔 0则为持续攻击
    public float BaseAttackInterval;//攻击间隔 0则为持续攻击
    public float RemainAttackIntervalTime;//剩余攻击间隔时间
    public bool IsBeAttack;

    public float3 CurrentPos;

}
