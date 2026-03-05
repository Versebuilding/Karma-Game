using UnityEngine;

/// <summary>
/// Configuration asset for the Karma progression system.
/// Create via: Right-click > Create > Karma > Karma Config.
///
/// The karma system has layered levels: each time the bar fills (xpPerLevel points),
/// the player levels up and a new petal lights up on the Flower of Life.
/// </summary>
[CreateAssetMenu(fileName = "KarmaConfig", menuName = "Karma/Karma Config", order = 2)]
public class KarmaConfig : ScriptableObject
{
    [Header("Leveling")]
    [Tooltip("Maximum karma level (number of flower petals)")]
    [Range(1, 12)] public int maxLevel = 7;

    [Tooltip("Karma points required to fill the bar and level up")]
    [Range(100, 5000)] public int xpPerLevel = 500;

    [Tooltip("Starting karma points for new game")]
    public int startingKarma = 167; // ~1/3 of 500 xpPerLevel

    [Header("Flower of Life Visuals")]
    [Tooltip("Sprite for the unlit flower base (empty state)")]
    public Sprite flowerBaseSprite;

    [Tooltip("Sprites for each lit petal (index = level - 1). First petal lights at level 1.")]
    public Sprite[] petalSprites;

    [Tooltip("Color of the karma progress bar")]
    public Color barColor = new Color(1f, 0.6f, 0.2f, 1f); // warm orange

    [Tooltip("Color flash on level up")]
    public Color levelUpFlashColor = new Color(1f, 0.9f, 0.4f, 1f); // golden

    [Header("Audio")]
    [Tooltip("Sound played when karma increases")]
    public AudioClip karmaGainClip;

    [Tooltip("Sound played when karma decreases")]
    public AudioClip karmaLossClip;

    [Tooltip("Sound played on level up (petal bloom)")]
    public AudioClip levelUpClip;
}
