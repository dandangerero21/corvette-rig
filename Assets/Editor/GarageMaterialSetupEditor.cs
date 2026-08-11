using System.IO;
using UnityEditor;
using UnityEngine;

public class GarageMaterialSetupEditor : EditorWindow
{
    [MenuItem("Tools/Fix Garage Materials & Textures")]
    public static void FixGarageMaterials()
    {
        string texturesFolderPath = "Assets/interior-9-free-with-cars/textures";
        string matFolderPath = "Assets/interior-9-free-with-cars/Materials";

        if (!AssetDatabase.IsValidFolder(texturesFolderPath))
        {
            EditorUtility.DisplayDialog("Garage Material Setup", $"Could not find textures folder at '{texturesFolderPath}'.", "OK");
            return;
        }

        if (!AssetDatabase.IsValidFolder(matFolderPath))
        {
            AssetDatabase.CreateFolder("Assets/interior-9-free-with-cars", "Materials");
        }

        // Find FBX model
        string fbxPath = "Assets/interior-9-free-with-cars/source/скетч.fbx";
        GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);

        // Load all texture assets from textures folder
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesFolderPath });
        int matCreatedCount = 0;

        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null) urpShader = Shader.Find("Standard");

        foreach (string guid in textureGuids)
        {
            string texPath = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) continue;

            // Skip normal maps for material creation (they get linked to albedo materials)
            if (tex.name.ToLower().Contains("normalmap") || tex.name.ToLower().Contains("_normal"))
            {
                // Ensure texture import type is set to Normal map if it's a normal map
                TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                continue;
            }

            // Create material for Albedo texture
            string matPath = Path.Combine(matFolderPath, tex.name + "_Mat.mat").Replace("\\", "/");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(urpShader);
                AssetDatabase.CreateAsset(mat, matPath);
                matCreatedCount++;
            }

            // Set main texture (Base Map in URP or MainTex in Standard)
            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", tex);

            // Try to find matching Normal map
            string matchingNormalPath = findMatchingNormalMap(tex.name, textureGuids);
            if (!string.IsNullOrEmpty(matchingNormalPath))
            {
                Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(matchingNormalPath);
                if (normalTex != null)
                {
                    if (mat.HasProperty("_BumpMap"))
                        mat.SetTexture("_BumpMap", normalTex);
                }
            }

            EditorUtility.SetDirty(mat);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Garage Material Setup", 
            $"Successfully created/updated {matCreatedCount} Materials in '{matFolderPath}'!\n\n" +
            "You can now drag & drop these materials directly onto your garage walls, floor, and roof in the Scene view!", 
            "OK");
    }

    private static string findMatchingNormalMap(string albedoName, string[] allGuids)
    {
        foreach (string guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.ToLower().Contains("normal") && name.Contains(albedoName))
            {
                return path;
            }
        }
        return null;
    }
}
