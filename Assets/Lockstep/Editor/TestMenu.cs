using UnityEngine;
using UnityEditor;
using Unity.Mathematics;

namespace Lockstep.Testing
{
    public class TestMenu
    {
        [MenuItem("帧同步/测试/Test Asset Path", priority = 20001)]
        public static void TestPathfinding()
        {
            Object[] arr = Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.TopLevel);
            if (arr.Length == 0) return;
            Debug.Log (AssetDatabase.GetAssetPath(Selection.activeObject).Substring(7));
        }
    }
}
