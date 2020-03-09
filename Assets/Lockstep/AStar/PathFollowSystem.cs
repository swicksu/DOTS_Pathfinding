using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using Lockstep;
using System.Collections.Generic;
using Lockstep.Math;
using Unity.Collections;

namespace Lockstep
{
    public class PathFollowSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            LFloat deltaTime = new LFloat(true, (int)(Time.DeltaTime * LFloat.Precision));
            int width = PathfindingManager.Instance.navMap.xCount;
            List<NavMapPoint> navMapPoints = PathfindingManager.Instance.navMap.navMapPoints;

            Entities.ForEach((Entity entity, DynamicBuffer<PathPosition> pathPositionBuffer, ref LTranslation translation, ref PathFollow pathFollow) => {
                if (pathFollow.pathIndex >= 0)
                {
                    // Has path to follow
                    PathPosition pathPosition = pathPositionBuffer[pathFollow.pathIndex];
                    int targetIndex = pathPosition.position.x + pathPosition.position.y * width;
                    LVector3 targetPos = new LVector3(navMapPoints[targetIndex].position.x, 0, navMapPoints[targetIndex].position.y);
                    LVector3 moveDir = (targetPos - translation.position).normalized;
                    LFloat moveSpeed = LFloat.one * 5;

                    translation.position += moveDir * moveSpeed * deltaTime;

                    if ((translation.position - targetPos).magnitude < LFloat.FromThousand(100))
                    {
                        pathFollow.pathIndex--;
                    }
                }
            });
        }
    }
}

[UpdateAfter(typeof(PathFollowSystem))]
[DisableAutoCreation]
public class PathFollowGetNewPathSystem : JobComponentSystem {
    
    private Unity.Mathematics.Random random;

    private EndSimulationEntityCommandBufferSystem endSimulationEntityCommandBufferSystem;

    protected override void OnCreate() {
        random = new Unity.Mathematics.Random(56);

        endSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps) {
        int mapWidth = PathfindingManager.Instance.navMap.xCount;
        int mapHeight = PathfindingManager.Instance.navMap.yCount;
        float3 originPosition = float3.zero;
        float cellSize = 1f;
        Unity.Mathematics.Random random = new Unity.Mathematics.Random(this.random.NextUInt(1, 10000));
        
        EntityCommandBuffer.Concurrent entityCommandBuffer = endSimulationEntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        JobHandle jobHandle = Entities.WithNone<PathfindingParams>().ForEach((Entity entity, int entityInQueryIndex, in PathFollow pathFollow, in Translation translation) => { 
            if (pathFollow.pathIndex == -1) {
                
                GetXY(translation.Value + new float3(1, 1, 0) * cellSize * +.5f, originPosition, cellSize, out int startX, out int startY);

                ValidateGridPosition(ref startX, ref startY, mapWidth, mapHeight);

                int endX = random.NextInt(0, mapWidth);
                int endY = random.NextInt(0, mapHeight);

                entityCommandBuffer.AddComponent(entityInQueryIndex, entity, new PathfindingParams { 
                    startPosition = new int2(startX, startY), endPosition = new int2(endX, endY) 
                });
            }
        }).Schedule(inputDeps);

        endSimulationEntityCommandBufferSystem.AddJobHandleForProducer(jobHandle);

        return jobHandle;
    }

    private static void ValidateGridPosition(ref int x, ref int y, int width, int height) {
        x = math.clamp(x, 0, width - 1);
        y = math.clamp(y, 0, height - 1);
    }

    private static void GetXY(float3 worldPosition, float3 originPosition, float cellSize, out int x, out int y) {
        x = (int)math.floor((worldPosition - originPosition).x / cellSize);
        y = (int)math.floor((worldPosition - originPosition).y / cellSize);
    }

}
