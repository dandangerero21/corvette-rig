using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class URPMaterialUpgrader : EditorWindow
{
    [MenuItem("Tools/Upgrade All Materials to URP (Fix Magenta-Pink)")]
    public static void UpgradeMaterialsToURP()
    {
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            EditorUtility.DisplayDialog("URP Material Upgrader", "Universal Render Pipeline/Lit shader not found! Make sure URP package is installed.", "OK");
            return;
        }

        string[] allMaterialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Simple Garage", "Assets/interior-9-free-with-cars" });
        int upgradedCount = 0;

        foreach (string guid in allMaterialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Check if material is using Built-in Standard or broken/missing shader
            if (mat.shader == null || 
                mat.shader.name == "Standard" || 
                mat.shader.name == "Standard (Specular setup)" || 
                mat.shader.name.StartsWith("Legacy Shaders") || 
                mat.shader.name == "Hidden/InternalErrorShader")
            {
                Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;

                mat.shader = urpLitShader;

                if (mainTex != null && mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", mainTex);
                }
                if (mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", color);
                }
                if (bumpMap != null && mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", bumpMap);
                }

                EditorUtility.SetDirty(mat);
                upgradedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("URP Material Upgrader", 
            $"Successfully upgraded {upgradedCount} materials to Universal Render Pipeline (URP/Lit)!\n\n" +
            "All pink/magenta shaders on the garage model have been fixed!", 
            "OK");
    }
}
