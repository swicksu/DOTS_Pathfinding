using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEngine.AI;
using Lockstep.Math;

namespace Lockstep.Editor
{
    public class AStarMenu
    {
        private class Triangle
        {
            public Vector3 a;
            public Vector3 b;
            public Vector3 c;
            
            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                this.a = a;
                this.b = b;
                this.c = c;
            }
        }

        // NavMap 参数
        private static readonly string s_BuildNavMapPath = "Resources/NavMap";
        private static readonly int s_PerMeterCount = 2;

        // GameObject，仅用于测试，方便观察
        private static readonly string s_NavMapPath = "Prefabs/AStar/NavMap";
        private static readonly string s_NavWalkable = "Prefabs/AStar/NavWalkable";
        private static readonly string s_NavNotWalkable = "Prefabs/AStar/NavNotWalkable";
        private static readonly bool s_GenerateGameObject = true;

        [MenuItem("帧同步/AStar/Generate NavMesh Point", priority = 10001)]
        public static void GenerateNavPointMenu()
        {
            BuildNavMesh();
            GenerateNavPoint();
        }

        /// <summary>
        /// 生成寻路点
        /// </summary>
        private static void GenerateNavPoint()
        {
            List<string> lines = new List<string>();
            string objDir = $"{Application.dataPath}/{s_BuildNavMapPath.TrimStart('/').TrimEnd('/')}/";
            string objPath = $"{objDir}/{SceneManager.GetActiveScene().name}_NavMesh.obj";
            if (File.Exists(objPath) == false)
            {
                return;
            }

            // 读取obj
            using (FileStream fs = new FileStream(objPath, FileMode.Open))
            {
                using (StreamReader sr = new StreamReader(fs))
                {
                    string line = null;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }

            // 处理数据
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            float maxY = float.MinValue;
            List<Vector3> points = new List<Vector3>();
            List<Triangle> triangles = new List<Triangle>();

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                string[] splits = line.Split(' ');

                // 获取所有点
                if (splits[0] == "v")
                {
                    Vector3 point = new Vector3(float.Parse(splits[1]), float.Parse(splits[2]), float.Parse(splits[3]));
                    points.Add(point);
                    if (point.x < minX) minX = point.x;
                    if (point.x > maxX) maxX = point.x;
                    if (point.z < minZ) minZ = point.z;
                    if (point.z > maxZ) maxZ = point.z;
                    if (point.y > maxY) maxY = point.y;
                }
                // 获取所有三角形
                if (splits[0] == "f")
                {
                    // 点索引
                    int aIndex = int.Parse(splits[1]);
                    int bIndex = int.Parse(splits[2]);
                    int cIndex = int.Parse(splits[3]);
                    Vector3 aPoint = new Vector3(points[aIndex - 1].x, 0f, points[aIndex - 1].z);
                    Vector3 bPoint = new Vector3(points[bIndex - 1].x, 0f, points[bIndex - 1].z);
                    Vector3 cPoint = new Vector3(points[cIndex - 1].x, 0f, points[cIndex - 1].z);
                    triangles.Add(new Triangle(aPoint, bPoint, cPoint));
                }
            }

            // 巡航点数量
            int xCount = (int)((maxX - minX) * s_PerMeterCount) + 1;
            int zCount = (int)((maxZ - minZ) * s_PerMeterCount) + 1;
            Debug.Log($"共生成寻路点: ({xCount} <=> {zCount})");
            NavMap navMap = ScriptableObject.CreateInstance<NavMap>();
            navMap.xCount = xCount;
            navMap.yCount = zCount;
            GameObject navMeshGo = null;
            GameObject.DestroyImmediate(GameObject.Find("NavMap"));
            if (s_GenerateGameObject)
            {
                navMeshGo = GameObject.Instantiate(Resources.Load<GameObject>(s_NavMapPath));
                navMeshGo.name = "NavMap";
            }
            for (int z = 0; z < zCount; z++)
            {
                for (int x = 0; x < xCount; x++)
                {
                    NavMapPoint navMapPoint = new NavMapPoint
                    {
                        x = x,
                        y = z
                    };
                    float addDistance = 1f / (float)s_PerMeterCount;
                    Vector3 generatePoint = new Vector3(minX + x * addDistance, 0f, minZ + z * addDistance);
                    navMapPoint.position = new LVector2(new LFloat(true, generatePoint.x), new LFloat(true, generatePoint.z));
                    if (CheckPointInTriangle(generatePoint, triangles) == true)
                    {
                        navMapPoint.isWalkable = true;
                    }
                    else
                    {
                        navMapPoint.isWalkable = false;
                    }
                    navMap.navMapPoints.Add(navMapPoint);

                    #region 生成GameObject，用于观察

                    GameObject generateGo = null;
                    if (s_GenerateGameObject)
                    {
                        if (navMapPoint.isWalkable)
                        {
                            generateGo = GameObject.Instantiate(Resources.Load<GameObject>(s_NavWalkable));
                        }
                        else
                        {
                            generateGo = GameObject.Instantiate(Resources.Load<GameObject>(s_NavNotWalkable));
                        }
                        generateGo.transform.position = generatePoint;
                        generateGo.transform.parent = navMeshGo.transform;
                        generateGo.name = $"NavPoint ({x}, {z})";
                    }

                    #endregion
                }
            }
            string navMapAsset = $"Assets/{s_BuildNavMapPath}/{SceneManager.GetActiveScene().name}_NavMap.asset";
            AssetDatabase.CreateAsset(navMap, navMapAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 点是否在这些三角形内
        /// </summary>
        private static bool CheckPointInTriangle(Vector3 point, List<Triangle> triangles)
        {
            if (triangles == null)
            {
                return false;
            }

            for (int i = 0; i < triangles.Count; i++)
            {
                Triangle tri = triangles[i];
                float s1 = CalcArea(tri.a, tri.b, tri.c);
                float s2 = CalcArea(point, tri.a, tri.b);
                float s3 = CalcArea(point, tri.a, tri.c);
                float s4 = CalcArea(point, tri.b, tri.c);
                // 不能用“==”判断两个浮点类型的值是否相等，可使用如下，差小于等于某个精度值即可。
                if (Mathf.Abs(s1 - (s2 + s3 + s4)) <= 0.1f) return true;
            }

            return false;
        }

        /// <summary>
        /// 计算三角形的大小
        /// 海伦公式：p=(a+b+c)/2; S = √[p(p-a)(p-b)(p-c)] //这里a,b,c代表边长
        /// </summary>
        private static float CalcArea(Vector3 a, Vector3 b, Vector3 c)
        {
            float dab = Vector3.Distance(a, b);
            float dac = Vector3.Distance(a, c);
            float dbc = Vector3.Distance(b, c);
            float half = (dab + dac + dbc) / 2;
            return Mathf.Sqrt(half * (half - dab) * (half - dac) * (half - dbc));
        }

        /// <summary>
        /// 构建 NavMesh
        /// </summary>
        private static void BuildNavMesh()
        {
            string objDir = $"{Application.dataPath}/{s_BuildNavMapPath.TrimStart('/').TrimEnd('/')}/";
            if (Directory.Exists(objDir) == false)
            {
                Directory.CreateDirectory(objDir);
            }

            string objPath = $"{objDir}/{SceneManager.GetActiveScene().name}_NavMesh.obj";
            File.Delete(objPath);
            NavMeshTriangulation navMeshTriangulation = NavMesh.CalculateTriangulation();
            using (FileStream fs = new FileStream(objPath, FileMode.Create))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    //顶点
                    for (int i = 0; i < navMeshTriangulation.vertices.Length; i++)
                    {
                        sw.WriteLine($"v {navMeshTriangulation.vertices[i].x} {navMeshTriangulation.vertices[i].y} {navMeshTriangulation.vertices[i].z}");
                    }

                    sw.WriteLine("g pPlane1");

                    //索引  
                    for (int i = 0; i < navMeshTriangulation.indices.Length;)
                    {
                        sw.WriteLine($"f {navMeshTriangulation.indices[i] + 1} {navMeshTriangulation.indices[i + 1] + 1} {navMeshTriangulation.indices[i + 2] + 1}");
                        i = i + 3;
                    }
                }
            }

            AssetDatabase.Refresh();
        }
    }
}
