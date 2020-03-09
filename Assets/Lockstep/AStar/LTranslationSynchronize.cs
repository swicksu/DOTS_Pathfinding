using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

namespace Lockstep
{
    public class LTranslationSynchronize : JobComponentSystem
    {
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            return Entities.ForEach((ref Translation translation, in LTranslation lTranslation) =>
            {
                float3 target = new float3(lTranslation.position.x.ToFloat(),
                                            lTranslation.position.y.ToFloat(),
                                            lTranslation.position.z.ToFloat());
                translation.Value = math.lerp(translation.Value, target, 0.5f);
            }).Schedule(inputDeps);
        }
    }
}