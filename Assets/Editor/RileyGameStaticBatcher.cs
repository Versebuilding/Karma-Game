using UnityEditor;
using UnityEngine;

/// <summary>
/// Marks all Riley Game environment objects as Static for batching,
/// occlusion culling, and lightmapping — critical for mobile perf.
/// Run after placing assets in the scene.
/// </summary>
public class RileyGameStaticBatcher
{
    [MenuItem("Riley Game/Mark Scene Objects as Static")]
    static void MarkAllStatic()
    {
        string[] prefixes = {
            "Track_", "Calm_", "Build_", "Spike_", "Chaos_",
            "Recovery_", "Reward_", "Transition_",
            "Stomach_", "Bloodstream_", "Brain_", "Heart_", "Lungs_",
            "Blood_Island", "Brain_Island", "Heart_Island", "Lungs_Island",
            "Prop_", "Portal_", "Zone_"
        };

        int count = 0;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            foreach (string prefix in prefixes)
            {
                if (go.name.StartsWith(prefix))
                {
                    // Mark as static for batching + occlusion + lightmap
                    GameObjectUtility.SetStaticEditorFlags(go,
                        StaticEditorFlags.BatchingStatic |
                        StaticEditorFlags.OccludeeStatic |
                        StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.ContributeGI);

                    // Disable shadows on small props (saves draw calls)
                    var renderer = go.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        bool isSmallProp = go.name.StartsWith("Prop_") ||
                                           go.name.Contains("Cloud") ||
                                           go.name.Contains("Spark") ||
                                           go.name.Contains("Lollipop");

                        if (isSmallProp)
                        {
                            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                            renderer.receiveShadows = false;
                        }
                        else
                        {
                            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                            renderer.receiveShadows = true;
                        }
                    }

                    count++;
                    break;
                }
            }
        }

        Debug.Log($"[RileyGame] Marked {count} objects as Static (batching + occlusion + GI)");
    }

    [MenuItem("Riley Game/Setup LOD Groups (Terrain + Islands)")]
    static void SetupLODGroups()
    {
        // For terrain and large islands, add simple LOD that culls at distance
        string[] lodTargets = { "Terrain_", "Island_Large", "Island_Medium" };
        int count = 0;

        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            foreach (string target in lodTargets)
            {
                if (!go.name.Contains(target)) continue;

                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer == null) continue;

                // Skip if already has LOD
                if (go.GetComponent<LODGroup>() != null) continue;

                var lod = go.AddComponent<LODGroup>();
                lod.SetLODs(new LOD[] {
                    new LOD(0.02f, new Renderer[] { renderer }) // cull below 2% screen
                });
                lod.RecalculateBounds();
                count++;
                break;
            }
        }

        Debug.Log($"[RileyGame] Added LOD cull groups to {count} terrain/island objects");
    }
}
