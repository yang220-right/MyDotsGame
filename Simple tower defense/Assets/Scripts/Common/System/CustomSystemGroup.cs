using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace YY.MainGame {
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    public partial class CustomFixedStep025SimulationSystemGroup : ComponentSystemGroup {
        public CustomFixedStep025SimulationSystemGroup() {
            RateManager = new RateUtils.VariableRateManager(250, true);//设置速率为0.25秒 执行一次
        }
    }
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    public partial class CreateBasicAttributeSystemGroup : ComponentSystemGroup { }
}