// Scandal Season — Runtime game layer.
// Boot: entry point. Loads the ContentRegistry from Resources, initializes the
// GameManager session, then hands off to the title state.

using UnityEngine;

public sealed class Boot : MonoBehaviour
{
    private void Start()
    {
        var registry = Resources.Load<ContentRegistry>("ContentRegistry");
        if (registry == null)
        {
            Debug.LogError("[Boot] ContentRegistry not found in Resources. " +
                "Run Scandal Season > Rebuild Content Registry in the editor.");
            return;
        }

        var game = GameManager.Instance;
        if (game == null)
        {
            Debug.LogError("[Boot] GameManager instance missing. Add it to the Boot scene.");
            return;
        }

        game.InitializeSession(
            registry.seasonOneScenes,
            registry.seasonTwoScenes,
            registry.seasonThreeScenes,
            registry.mergeChains);

        Debug.Log($"[Boot] Session ready: S1={registry.seasonOneScenes.Count}, " +
                  $"S2={registry.seasonTwoScenes.Count}, S3={registry.seasonThreeScenes.Count}, " +
                  $"chains={registry.mergeChains.Count}.");
    }
}
