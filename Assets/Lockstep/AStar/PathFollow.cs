using Unity.Entities;

namespace Lockstep
{
    [GenerateAuthoringComponent]
    public struct PathFollow : IComponentData
    {
        public int pathIndex;
    }
}
