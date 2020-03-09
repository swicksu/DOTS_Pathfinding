using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lockstep
{
    public class PathfindingManager : MonoBehaviour
    {
        public static PathfindingManager Instance { private set; get; }
        public NavMap navMap;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            Instance = null;
        }
    }
}
