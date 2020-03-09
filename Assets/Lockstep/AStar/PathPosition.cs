using Unity.Entities;
using Unity.Mathematics;

namespace Lockstep
{
    [InternalBufferCapacity(20)]
    public struct PathPosition : IBufferElementData
    {
        public int2 position;
    }
}
