using System;
using Unity.Entities;
using Unity.Mathematics;

namespace YY.MainGame {
    [Flags]
    public enum DataType {
        Turret = 1,     //防御塔
        Enemy = 1 << 1,      //敌人
        Core = 1 << 2,       //核心
    }
    public partial struct BasicAttributeData : IComponentData {
        public bool Init;
        public DataType Type;

        public int EntityID;
        public float MaxHP;
        public float CurrentHP;

        public float BaseAttack;//攻击力
        public float CurrentAttack;//攻击力
        public float BaseAttackCircle;
        public float CurrentAttackCircle;//攻击
        public float AttackAngle;//攻击角度
        public float3 CurrentAttackDir;

        public float CurrentAttackInterval;//攻击间隔 0则为持续攻击
        public float BaseAttackInterval;//攻击间隔 0则为持续攻击
        public float RemainAttackIntervalTime;//剩余攻击间隔时间

        public bool isFixedDir;//攻击时固定方向
        public bool IsBeAttack;

        public float3 CurrentPos;
    }

    public partial struct ReduceHPBuffer : IBufferElementData {
        public float HP;
    }
    public partial struct DamageColorData : IComponentData {
        public bool IsChange;
        public float BaseTime;
        public float CurrentTime;
        public float4 BaseColor;
        public float4 CurrentColor;
    }
}
