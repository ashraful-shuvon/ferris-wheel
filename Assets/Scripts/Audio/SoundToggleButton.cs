using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to your sound on/off toggle button.
/// Swaps between 2 sprites and mutes/unmutes all game audio.
///
/// Setup:
///   1. Attach this script to your sound toggle Button GameObject.
///   2. Drag the Button's Image into buttonImage.
///   3. Drag your sound ON sprite into soundOnSprite.
///   4. Drag your sound OFF sprite into soundOffSprite.
///   5. Drag your GameAudioManager reference in.
///   6. Wire this button's OnClick to the Toggle() method.
/// </summary>
public class SoundToggleButton : MonoBehaviour
{
    [Header("References")]
    public Image            buttonImage;
    public GameAudioManager audioManager;

    [Header("Sprites")]
    public Sprite soundOnSprite;
    public Sprite soundOffSprite;

    bool isSoundOn = true;

    void Awake()
    {
        if (buttonImage == null)
            buttonImage = GetComponent<Image>();

        // Start in the ON state.
        UpdateSprite();
    }

    /// <summary>Wire this to the Button's OnClick event in the Inspector.</summary>
    public void Toggle()
    {
        isSoundOn = !isSoundOn;

        if (audioManager != null)
        {
            // Mute/unmute both audio sources.
            AudioListener.volume = isSoundOn ? 1f : 0f;
        }

        UpdateSprite();
    }

    void UpdateSprite()
    {
        if (buttonImage == null) return;
        buttonImage.sprite = isSoundOn ? soundOnSprite : soundOffSprite;
    }
}
