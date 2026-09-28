// Scandal Season — Editor tools.
// ContentRegistryBuilder: scans the Generated folders and (re)builds the
// ContentRegistry asset in Resources. Run via the menu after re-importing.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ContentRegistryBuilder
{
    private const string RegistryPath = "Assets/Resources/ContentRegistry.asset";

    [MenuItem("Scandal Season/Rebuild Content Registry")]
    public static void RebuildRegistry()
    {
        var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(RegistryPath);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<ContentRegistry>();
            AssetDatabase.CreateAsset(registry, RegistryPath);
        }

        registry.seasonOneScenes = FindScenes("s1-");
        registry.seasonTwoScenes = FindScenes("s2-");
        registry.seasonThreeScenes = FindScenes("s3-");
        registry.mergeChains = FindChains();

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Content] Registry rebuilt: S1={registry.seasonOneScenes.Count}, " +
                  $"S2={registry.seasonTwoScenes.Count}, S3={registry.seasonThreeScenes.Count}, " +
                  $"chains={registry.mergeChains.Count}.");
    }

    private static List<SceneDefinitionSO> FindScenes(string prefix)
    {
        var result = new List<SceneDefinitionSO>();
        string[] guids = AssetDatabase.FindAssets("t:SceneDefinitionSO",
            new[] { "Assets/Scripts/Runtime/Generated/Scenes" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!file.StartsWith(prefix)) continue;
            var so = AssetDatabase.LoadAssetAtPath<SceneDefinitionSO>(path);
            if (so != null) result.Add(so);
        }
        result.Sort((a, b) =>
        {
            int c = a.chapter.CompareTo(b.chapter);
            return c != 0 ? c : a.sceneNumber.CompareTo(b.sceneNumber);
        });
        return result;
    }

    private static List<ItemChainDefinitionSO> FindChains()
    {
        var result = new List<ItemChainDefinitionSO>();
        string[] guids = AssetDatabase.FindAssets("t:ItemChainDefinitionSO",
            new[] { "Assets/Scripts/Runtime/Generated/MergeChains" });
        foreach (string guid in guids)
        {
            var so = AssetDatabase.LoadAssetAtPath<ItemChainDefinitionSO>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (so != null) result.Add(so);
        }
        result.Sort((a, b) => string.Compare(a.chainId, b.chainId, System.StringComparison.Ordinal));
        return result;
    }
}
