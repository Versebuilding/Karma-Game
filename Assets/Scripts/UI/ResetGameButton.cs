using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Simple reset button that restores all game state to defaults and reloads the scene.
///
/// Resets:
///   - Karma → starting value (from KarmaConfig)
///   - Coins → starting value (from WalletManager)
///   - VariableStore → all flags, counters, relationships cleared
///   - Active dialogue → ended
///   - Scene → reloaded (re-creates player, NPCs, world objects)
///
/// Managers use DontDestroyOnLoad, so state is reset BEFORE scene reload
/// and the managers survive with their reset values.
///
/// Setup:
///   1. Add to a UI Button in the HUD Canvas
///   2. Wire Button.onClick → ResetGame()
///   3. UISetupTool creates this automatically
/// </summary>
public class ResetGameButton : MonoBehaviour
{
    /// <summary>Reset all game state and reload the current scene.</summary>
    public void ResetGame()
    {
        Debug.Log("ResetGameButton: Resetting game to defaults...");

        // Reset karma to starting value
        if (KarmaManager.Instance != null && KarmaManager.Instance.Config != null)
        {
            KarmaManager.Instance.SetKarma(KarmaManager.Instance.Config.startingKarma);
            Debug.Log($"  Karma → {KarmaManager.Instance.Config.startingKarma}");
        }

        // Reset coins to starting value
        if (WalletManager.Instance != null)
        {
            WalletManager.Instance.SetCoins(WalletManager.Instance.StartingCoins);
            Debug.Log($"  Coins → {WalletManager.Instance.StartingCoins}");
        }

        // Clear all flags, counters, relationships (re-enables one-time rewards)
        if (VariableStore.Instance != null)
        {
            VariableStore.Instance.ResetAll();
            Debug.Log("  VariableStore → cleared");
        }

        // Clear one-time reward tracking (re-enables all dialogue rewards)
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ClearRewardedChoices();
            Debug.Log("  Rewarded choices → cleared");

            // End any active dialogue
            if (DialogueManager.Instance.IsDialogueActive)
            {
                DialogueManager.Instance.EndDialogue();
                Debug.Log("  Dialogue → ended");
            }
        }

        Debug.Log("ResetGameButton: Reloading scene...");

        // Reload the current scene (re-creates non-persistent objects)
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
