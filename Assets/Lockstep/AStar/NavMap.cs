using UnityEngine;
using System;
using System.Collections.Generic;
using Lockstep.Math;

namespace Lockstep
{
    [Serializable]
    public struct NavMapPoint
    {
        public int x;
        public int y;
        public LVector2 position;
        public bool isWalkable;
        [NonSerialized]
        public bool hasObstacle;
    }
     
    [CreateAssetMenu(fileName = "New NavMap", menuName = "帧同步/NavMap", order = 1)]
    public class NavMap : ScriptableObject
    {
        public int xCount;
        public int yCount;
        public List<NavMapPoint> navMapPoints = new List<NavMapPoint>();
    }
}
