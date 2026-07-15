using System.IO;
using UnityEditor;
using UnityEngine;

public class GLTFMaterialExtractor : EditorWindow
{
    [MenuItem("Tools/Extract GLTF Materials")]
    public static void ExtractMaterials()
    {
        Object[] selectedObjects = Selection.GetFiltered<Object>(SelectionMode.Assets);
        if (selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("Extract Materials", "Please select a GLTF/GLB model file in the Project window first.", "OK");
            return;
        }

        foreach (Object obj in selectedObjects)
        {
            string assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath) || (!assetPath.EndsWith(".glb") && !assetPath.EndsWith(".gltf")))
            {
                continue;
            }

            // Create target folder next to model
            string dirPath = Path.GetDirectoryName(assetPath);
            string folderName = Path.GetFileNameWithoutExtension(assetPath) + "_Materials";
            string targetFolder = Path.Combine(dirPath, folderName);

            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                AssetDatabase.CreateFolder(dirPath, folderName);
            }

            // Load all sub-assets (including the internal read-only materials)
            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            int count = 0;

            // Start editing asset import settings
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            
            foreach (Object subAsset in subAssets)
            {
                if (subAsset is Material srcMaterial)
                {
                    // Clone the material to make it editable
                    Material clonedMaterial = new Material(srcMaterial);
                    string cleanName = srcMaterial.name.Replace(":", "_").Replace("/", "_");
                    string destPath = Path.Combine(targetFolder, cleanName + ".mat").Replace("\\", "/");

                    // Save the new editable material asset
                    AssetDatabase.CreateAsset(clonedMaterial, destPath);

                    // Rebind the importer to use the newly created external material
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(srcMaterial), clonedMaterial);
                    count++;
                }
            }

            // Save and reimport model
            importer.SaveAndReimport();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Extract Materials", $"Successfully extracted {count} materials from {obj.name} to:\n{targetFolder}\n\nThey have been automatically assigned to the model!", "OK");
        }
    }
}
