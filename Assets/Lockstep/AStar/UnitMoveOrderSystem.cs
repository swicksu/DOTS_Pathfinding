using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Lockstep;
using Lockstep.Math;
using System.Collections.Generic;

namespace Lockstep.Demo
{
    public class UnitMoveOrderSystem : ComponentSystem
    {
        protected override void OnUpdate()
        {
            // 测试使用
            if (Input.GetMouseButtonDown(0))
            {
                Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hitInfo;
                if (Physics.Raycast(mouseRay, out hitInfo, 100f))
                {
                    LVector3 hitPoint = new LVector3(new LFloat(true, (int)(hitInfo.point.x * LFloat.Precision)),
                                                     LFloat.zero,
                                                     new LFloat(true, (int)(hitInfo.point.z * LFloat.Precision)));
                    List<NavMapPoint> navMapPoints = PathfindingManager.Instance.navMap.navMapPoints;
                    Entities.ForEach((Entity entity, DynamicBuffer<PathPosition> pathPositionBuffer, ref LTranslation translation) => {
                        LFloat startMinDistance = LFloat.MaxValue;
                        LFloat endMinDistance = LFloat.MaxValue;
                        int2 startPosition = new int2(0, 0);
                        int2 endPosition = new int2(0, 0);
                        for (int i = 1; i < navMapPoints.Count; i++)
                        {
                            LVector2 tempPos2 = navMapPoints[i].position;
                            LVector3 tempPos3 = new LVector3(tempPos2.x, 0, tempPos2.y);

                            // 寻找离当前位置最近的点
                            if ((tempPos3 - translation.position).magnitude < startMinDistance)
                            {
                                startPosition = new int2(navMapPoints[i].x, navMapPoints[i].y);
                                startMinDistance = (tempPos3 - translation.position).magnitude;
                            }

                            // 寻找离目标位置最近的点
                            if ((tempPos3 - hitPoint).magnitude < endMinDistance)
                            {
                                endPosition = new int2(navMapPoints[i].x, navMapPoints[i].y);
                                endMinDistance = (tempPos3 - hitPoint).magnitude;
                            }
                        }

                        Debug.Log($"位置: {startPosition}, {endPosition}");
                        EntityManager.AddComponentData(entity, new PathfindingParams
                        {
                            startPosition = startPosition,
                            endPosition = endPosition
                        });
                    });
                }
            }
        }
    }
}
