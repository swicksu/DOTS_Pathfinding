/* 
    ------------------- Code Monkey -------------------

    Thank you for downloading this package
    I hope you find it useful in your projects
    If you have any questions let me know
    Cheers!

               unitycodemonkey.com
    --------------------------------------------------
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;

namespace Lockstep
{
    public class Pathfinding : ComponentSystem
    {

        private const int MOVE_STRAIGHT_COST = 10;
        private const int MOVE_DIAGONAL_COST = 14;

        protected override void OnUpdate()
        {
            // 地图大小
            int gridWidth = PathfindingManager.Instance.navMap.xCount;
            int gridHeight = PathfindingManager.Instance.navMap.yCount;
            int2 gridSize = new int2(gridWidth, gridHeight);

            // 存放Job数据
            List<FindPathJob> findPathJobList = new List<FindPathJob>();
            // 存放Job句柄，用于下面的CompleteAll
            NativeList<JobHandle> jobHandleList = new NativeList<JobHandle>(Allocator.Temp);
            // 存放地图数据
            NativeArray<PathNode> pathNodeArray = GetPathNodeArray();

            Entities.ForEach((Entity entity, ref PathfindingParams pathfindingParams) =>
            {
                // 复制一份，因为每个寻路都会用到，并改变其值
                NativeArray<PathNode> tmpPathNodeArray = new NativeArray<PathNode>(pathNodeArray, Allocator.TempJob);
                FindPathJob findPathJob = new FindPathJob
                {
                    gridSize = gridSize,
                    pathNodeArray = tmpPathNodeArray,
                    startPosition = pathfindingParams.startPosition,
                    endPosition = pathfindingParams.endPosition,
                    entity = entity,
                };
                findPathJobList.Add(findPathJob);
                jobHandleList.Add(findPathJob.Schedule());
                // 移除寻路参数
                PostUpdateCommands.RemoveComponent<PathfindingParams>(entity);
            });
            // 完成所有寻路Job
            JobHandle.CompleteAll(jobHandleList);

            // 开启写入寻路数据的Job
            foreach (FindPathJob findPathJob in findPathJobList)
            {
                new SetBufferPathJob
                {
                    entity = findPathJob.entity,
                    gridSize = findPathJob.gridSize,
                    pathNodeArray = findPathJob.pathNodeArray,
                    pathfindingParamsComponentDataFromEntity = GetComponentDataFromEntity<PathfindingParams>(),
                    pathFollowComponentDataFromEntity = GetComponentDataFromEntity<PathFollow>(),
                    pathPositionBufferFromEntity = GetBufferFromEntity<PathPosition>(),
                }.Run();
            }

            // 记得释放非托管内存
            pathNodeArray.Dispose();
        }

        /// <summary>
        /// 获取到寻路节点数组
        /// </summary>
        private NativeArray<PathNode> GetPathNodeArray()
        {
            NavMap navMap = PathfindingManager.Instance.navMap;
            List<NavMapPoint> navMapPoints = navMap.navMapPoints;
            int2 gridSize = new int2(navMap.xCount, navMap.yCount);
            NativeArray<PathNode> pathNodeArray = new NativeArray<PathNode>(gridSize.x * gridSize.y, Allocator.TempJob);
            for (int x = 0; x < navMap.xCount; x++)
            {
                for (int y = 0; y < navMap.yCount; y++)
                {
                    PathNode pathNode = new PathNode();
                    pathNode.x = x;
                    pathNode.y = y;
                    pathNode.index = CalculateIndex(x, y, gridSize.x);
                    pathNode.gCost = int.MaxValue;
                    pathNode.isWalkable = navMapPoints[CalculateIndex(x, y, gridSize.x)].isWalkable;
                    pathNode.cameFromNodeIndex = -1;
                    pathNodeArray[pathNode.index] = pathNode;
                }
            }

            return pathNodeArray;
        }

        /// <summary>
        /// 把A*路径写入到DynamicBuffer里面
        /// </summary>
        [BurstCompile]
        private struct SetBufferPathJob : IJob
        {
            public int2 gridSize;
            [DeallocateOnJobCompletion]
            public NativeArray<PathNode> pathNodeArray;

            public Entity entity;
            public ComponentDataFromEntity<PathfindingParams> pathfindingParamsComponentDataFromEntity;
            public ComponentDataFromEntity<PathFollow> pathFollowComponentDataFromEntity;
            public BufferFromEntity<PathPosition> pathPositionBufferFromEntity;

            public void Execute()
            {
                DynamicBuffer<PathPosition> pathPositionBuffer = pathPositionBufferFromEntity[entity];
                pathPositionBuffer.Clear();

                PathfindingParams pathfindingParams = pathfindingParamsComponentDataFromEntity[entity];
                int endNodeIndex = CalculateIndex(pathfindingParams.endPosition.x, pathfindingParams.endPosition.y, gridSize.x);
                PathNode endNode = pathNodeArray[endNodeIndex];
                if (endNode.cameFromNodeIndex == -1)
                {
                    // Didn't find a path!
                    //Debug.Log("Didn't find a path!");
                    pathFollowComponentDataFromEntity[entity] = new PathFollow { pathIndex = -1 };
                }
                else
                {
                    // Found a path
                    CalculatePath(pathNodeArray, endNode, pathPositionBuffer);

                    pathFollowComponentDataFromEntity[entity] = new PathFollow { pathIndex = pathPositionBuffer.Length - 2 };
                }

            }
        }

        /// <summary>
        /// 得到一条从Start到End的可行路径
        /// </summary>
        [BurstCompile]
        private struct FindPathJob : IJob
        {
            public int2 gridSize;
            public NativeArray<PathNode> pathNodeArray;

            public int2 startPosition;
            public int2 endPosition;

            public Entity entity;

            //public BufferFromEntity<PathPosition> pathPositionBuffer;

            public void Execute()
            {
                for (int i = 0; i < pathNodeArray.Length; i++)
                {
                    PathNode pathNode = pathNodeArray[i];
                    pathNode.hCost = CalculateDistanceCost(new int2(pathNode.x, pathNode.y), endPosition);
                    pathNode.cameFromNodeIndex = -1;

                    pathNodeArray[i] = pathNode;
                }

                NativeArray<int2> neighbourOffsetArray = new NativeArray<int2>(8, Allocator.Temp);
                neighbourOffsetArray[0] = new int2(-1, 0); // Left
                neighbourOffsetArray[1] = new int2(+1, 0); // Right
                neighbourOffsetArray[2] = new int2(0, +1); // Up
                neighbourOffsetArray[3] = new int2(0, -1); // Down
                neighbourOffsetArray[4] = new int2(-1, -1); // Left Down
                neighbourOffsetArray[5] = new int2(-1, +1); // Left Up
                neighbourOffsetArray[6] = new int2(+1, -1); // Right Down
                neighbourOffsetArray[7] = new int2(+1, +1); // Right Up

                int endNodeIndex = CalculateIndex(endPosition.x, endPosition.y, gridSize.x);

                PathNode startNode = pathNodeArray[CalculateIndex(startPosition.x, startPosition.y, gridSize.x)];
                startNode.gCost = 0;
                startNode.CalculateFCost();
                pathNodeArray[startNode.index] = startNode;

                NativeList<int> openList = new NativeList<int>(Allocator.Temp);
                NativeList<int> closedList = new NativeList<int>(Allocator.Temp);

                openList.Add(startNode.index);

                while (openList.Length > 0)
                {
                    int currentNodeIndex = GetLowestCostFNodeIndex(openList, pathNodeArray);
                    PathNode currentNode = pathNodeArray[currentNodeIndex];

                    if (currentNodeIndex == endNodeIndex)
                    {
                        // Reached our destination!
                        break;
                    }

                    // Remove current node from Open List
                    for (int i = 0; i < openList.Length; i++)
                    {
                        if (openList[i] == currentNodeIndex)
                        {
                            openList.RemoveAtSwapBack(i);
                            break;
                        }
                    }

                    closedList.Add(currentNodeIndex);

                    for (int i = 0; i < neighbourOffsetArray.Length; i++)
                    {
                        int2 neighbourOffset = neighbourOffsetArray[i];
                        int2 neighbourPosition = new int2(currentNode.x + neighbourOffset.x, currentNode.y + neighbourOffset.y);

                        if (!IsPositionInsideGrid(neighbourPosition, gridSize))
                        {
                            // Neighbour not valid position
                            continue;
                        }

                        int neighbourNodeIndex = CalculateIndex(neighbourPosition.x, neighbourPosition.y, gridSize.x);

                        if (closedList.Contains(neighbourNodeIndex))
                        {
                            // Already searched this node
                            continue;
                        }

                        PathNode neighbourNode = pathNodeArray[neighbourNodeIndex];
                        if (!neighbourNode.isWalkable)
                        {
                            // Not walkable
                            continue;
                        }

                        int2 currentNodePosition = new int2(currentNode.x, currentNode.y);

                        int tentativeGCost = currentNode.gCost + CalculateDistanceCost(currentNodePosition, neighbourPosition);
                        if (tentativeGCost < neighbourNode.gCost)
                        {
                            neighbourNode.cameFromNodeIndex = currentNodeIndex;
                            neighbourNode.gCost = tentativeGCost;
                            neighbourNode.CalculateFCost();
                            pathNodeArray[neighbourNodeIndex] = neighbourNode;

                            if (!openList.Contains(neighbourNode.index))
                            {
                                openList.Add(neighbourNode.index);
                            }
                        }

                    }
                }

                //pathPositionBuffer.Clear();

                /*
                PathNode endNode = pathNodeArray[endNodeIndex];
                if (endNode.cameFromNodeIndex == -1) {
                    // Didn't find a path!
                    //Debug.Log("Didn't find a path!");
                    pathFollowComponentDataFromEntity[entity] = new PathFollow { pathIndex = -1 };
                } else {
                    // Found a path
                    //CalculatePath(pathNodeArray, endNode, pathPositionBuffer);
                    //pathFollowComponentDataFromEntity[entity] = new PathFollow { pathIndex = pathPositionBuffer.Length - 1 };
                }
                */

                neighbourOffsetArray.Dispose();
                openList.Dispose();
                closedList.Dispose();
            }


        }

        /// <summary>
        /// 获取到A*路径，存到PathPositionBuffer里面
        /// </summary>
        private static void CalculatePath(NativeArray<PathNode> pathNodeArray, PathNode endNode, DynamicBuffer<PathPosition> pathPositionBuffer)
        {
            if (endNode.cameFromNodeIndex == -1)
            {
                // Couldn't find a path!
            }
            else
            {
                // Found a path
                pathPositionBuffer.Add(new PathPosition { position = new int2(endNode.x, endNode.y) });

                PathNode currentNode = endNode;
                while (currentNode.cameFromNodeIndex != -1)
                {
                    PathNode cameFromNode = pathNodeArray[currentNode.cameFromNodeIndex];
                    pathPositionBuffer.Add(new PathPosition { position = new int2(cameFromNode.x, cameFromNode.y) });
                    currentNode = cameFromNode;
                }
            }
        }

        /// <summary>
        /// 判断位置是否合法
        /// </summary>
        private static bool IsPositionInsideGrid(int2 gridPosition, int2 gridSize)
        {
            return
                gridPosition.x >= 0 &&
                gridPosition.y >= 0 &&
                gridPosition.x < gridSize.x &&
                gridPosition.y < gridSize.y;
        }

        /// <summary>
        /// 计算索引值
        /// </summary>
        private static int CalculateIndex(int x, int y, int gridWidth)
        {
            return x + y * gridWidth;
        }

        /// <summary>
        /// 计算两点之间的开销
        /// </summary>
        private static int CalculateDistanceCost(int2 aPosition, int2 bPosition)
        {
            int xDistance = math.abs(aPosition.x - bPosition.x);
            int yDistance = math.abs(aPosition.y - bPosition.y);
            int remaining = math.abs(xDistance - yDistance);
            return MOVE_DIAGONAL_COST * math.min(xDistance, yDistance) + MOVE_STRAIGHT_COST * remaining;
        }

        /// <summary>
        /// 获取到开销最小的点
        /// </summary>
        private static int GetLowestCostFNodeIndex(NativeList<int> openList, NativeArray<PathNode> pathNodeArray)
        {
            PathNode lowestCostPathNode = pathNodeArray[openList[0]];
            for (int i = 1; i < openList.Length; i++)
            {
                PathNode testPathNode = pathNodeArray[openList[i]];
                if (testPathNode.fCost < lowestCostPathNode.fCost)
                {
                    lowestCostPathNode = testPathNode;
                }
            }
            return lowestCostPathNode.index;
        }

        /// <summary>
        /// 寻路节点，在A*计算中使用
        /// </summary>
        private struct PathNode
        {
            public int x;
            public int y;

            public int index;

            public int gCost;
            public int hCost;
            public int fCost;

            public bool isWalkable;

            public int cameFromNodeIndex;

            public void CalculateFCost()
            {
                fCost = gCost + hCost;
            }

            public void SetIsWalkable(bool isWalkable)
            {
                this.isWalkable = isWalkable;
            }
        }
    }
}
