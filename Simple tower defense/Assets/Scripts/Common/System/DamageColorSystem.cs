using Unity.Collections;
using Unity.Entities;
using YY.Enemy;

namespace YY.MainGame {
    public partial struct DamageColorSystem : ISystem {
        private void OnUpdate(ref SystemState state) {
            new DamageColorJob
            {
                time = SystemAPI.Time.DeltaTime
            }
            .Schedule();
        }
    }
    public partial struct DamageColorJob : IJobEntity {
        [ReadOnly]public float time;
        private void Execute(ref ShaderOverrideColor color, ref DamageColorData data) {
            if (data.IsChange) {
                if (data.CurrentTime <= 0) {
                    color.Value = data.CurrentColor;
                    data.IsChange = false;
                }
                data.CurrentTime = data.BaseTime;
            }
            if (data.CurrentTime > 0) {
                data.CurrentTime -= time;
                if (data.CurrentTime <= 0) {
                    color.Value = data.BaseColor;
                }
            }
        }
    }
}
