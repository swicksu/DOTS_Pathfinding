using Unity.Entities;
using Lockstep.Math;
using System;

namespace Lockstep
{
    [GenerateAuthoringComponent]
    public struct LTranslation : IComponentData
    {
        public LVector3 position;
    }
}
