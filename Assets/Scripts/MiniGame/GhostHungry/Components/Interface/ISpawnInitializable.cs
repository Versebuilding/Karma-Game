using UnityEngine;

/// <summary>
/// A modifier for scripts on a spawnable object which allows post-instantiation data collection
/// </summary>
/// <remarks>
/// <b>Use When:</b> (all true)<br/>
/// - The attached object is required to be instantiated at runtime<br/>
/// - The script requires data that is not guaranteed to be present on instantiation<br/>
/// - The required data is present in the instantiator<br/>
/// - The spawnable object will miss or be unable to get the initial data pass<br/>
/// <br/>
/// <b>Primary Use:</b> <see cref="DynamicObjectSpawner"/> system
/// </remarks>
public interface ISpawnInitializable {
    /// <summary>
    /// Initialize an unfinished object through the provided raw instantiation data
    /// </summary>
    /// <param name="data">A data container which holds all required instantiation data, potentially in an unsanitized, original state</param>
    void InitializeSpawnedInstance(ScriptableObject data);
}