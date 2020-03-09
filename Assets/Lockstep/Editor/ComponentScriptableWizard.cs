using UnityEditor;
using UnityEngine;
using System.IO;

namespace Lockstep.Editor
{
    public class ComponentScriptableWizard : ScriptableWizard
    {
        public const string SCRIPT_TEMPLATE =
@"using Unity.Entities;

namespace 命名空间
{
    [GenerateAuthoringComponent]
    public struct 脚本名 : IComponentData
    {

    }
}";
        public string scriptName;

        private readonly string s_Namespace = "Logic";

        private void OnWizardCreate()
        {
            string assetDirectory = AssetDatabase.GetAssetPath(Selection.activeObject).Substring(7);
            if (string.IsNullOrEmpty(assetDirectory)) return;

            // 检查目录
            string generateDirectory = $"{Application.dataPath}/{assetDirectory}";
            if (Directory.Exists(generateDirectory) == false)
            {
                Directory.CreateDirectory(generateDirectory);
            }

            // 写入脚本
            using (FileStream fs = new FileStream($"{generateDirectory}/{scriptName}.cs", FileMode.OpenOrCreate))
            {
                using (StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
                {
                    string code = SCRIPT_TEMPLATE;
                    code = code.Replace("脚本名", scriptName);
                    code = code.Replace("命名空间", s_Namespace);
                    sw.Write(code);
                }
            }

            // 刷新
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/代码模板/Component &1", priority = 10001)]
        private static void CreateWizard()
        {
            ScriptableWizard.DisplayWizard<ComponentScriptableWizard>("ComponentData 模板", "Create Script");
        }
    }
}
