// Scandal Season — Runtime game layer.
// ContentRegistry: single ScriptableObject holding references to all imported
// content. Built by the editor (ContentRegistryBuilder); loaded by Boot from
// Resources. Keeps 2,400 scene assets out of Resources while staying loadable.

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ContentRegistry", menuName = "Scandal Season/Content Registry")]
public sealed class ContentRegistry : ScriptableObject
{
    [Tooltip("All Season One scene assets (1,200).")]
    public List<SceneDefinitionSO> seasonOneScenes = new List<SceneDefinitionSO>();
    [Tooltip("All Season Two scene assets (1,200).")]
    public List<SceneDefinitionSO> seasonTwoScenes = new List<SceneDefinitionSO>();
    [Tooltip("All merge-chain assets (Board F).")]
    public List<ItemChainDefinitionSO> mergeChains = new List<ItemChainDefinitionSO>();
}
