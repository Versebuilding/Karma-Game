using UnityEngine;

public class SkyboxController : MonoBehaviour
{
    [Header("Skybox Materials")]
    public Material daySky;
    public Material nightSky;

    [Header("Optional: animated params")]
    public float cloudSpeed = 0.2f;
    public string cloudSpeedProperty = "_CloudSpeed"; // change to the exact property name in the material!

    void Start()
    {
        SetSkybox(daySky);
    }

    public void SetSkybox(Material sky)
    {
        RenderSettings.skybox = sky;

        // If you use GI / reflection probes, update environment:
        DynamicGI.UpdateEnvironment();
    }

    void Update()
    {
        // Example: animate “wind” (cloud speed) if the shader has it
        if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty(cloudSpeedProperty))
        {
            RenderSettings.skybox.SetFloat(cloudSpeedProperty, cloudSpeed);
        }
    }

    public void SwitchToNight() => SetSkybox(nightSky);
    public void SwitchToDay() => SetSkybox(daySky);
}
