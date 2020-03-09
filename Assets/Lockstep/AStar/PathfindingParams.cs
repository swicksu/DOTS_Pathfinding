using Unity.Entities;
using Unity.Mathematics;

namespace Lockstep
{
    public struct PathfindingParams : IComponentData
    {
        public int2 startPosition;
        public int2 endPosition;
    }
}