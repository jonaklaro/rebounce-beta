using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class PercentUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI percentText;
    [SerializeField] private TextMeshProUGUI chargesText;
    [SerializeField] private TextMeshProUGUI playerLabelText;
    [SerializeField] private int playerIndex; // 0..3
    [SerializeField] private Image backgroundPanel;
    
    [Header("Heart Display")]
    [SerializeField] private Transform heartContainer;
    [SerializeField] private GameObject heartPrefab;
    [SerializeField] private float heartSpacing = 10f;

    private Color textColor;

    private void Awake()
    {
        if (percentText == null)
            percentText = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Init(int index, Color playerColor, int maxHealth = 3)
    {
        playerIndex = index;

        // Label: Player 1 / 2 / 3 / 4
        if (playerLabelText != null)
            playerLabelText.text = $"Player {playerIndex + 1}";

        if (chargesText != null)
            chargesText.text = "3 / 3";

        textColor = chargesText.color;

        // Hintergrund einfärben
        ApplyPlayerColor(playerColor);
        
        // Initialize hearts at start
        CreateHearts(maxHealth);
    }

    public void UpdatePercent(float displayValue)
    {
        if (percentText != null)
            if (displayValue > 350) {
                percentText.color = Color.red;
            }
            else {
                percentText.color = Color.white;
            }

            percentText.text = $"{displayValue:F1}%";
    }

    private void ApplyPlayerColor(Color color)
    {
        if (backgroundPanel != null)
        {
            backgroundPanel.color = new Color(color.r, color.g, color.b);
        }
    }

    /// <summary>
    /// Update health display by destroying or creating hearts
    /// </summary>
    public void UpdateHealth(int currentHealth, int maxHealth)
    {
        // Clear all existing hearts
        foreach (Transform child in heartContainer)
        {
            Destroy(child.gameObject);
        }

        // Create hearts based on current health
        CreateHearts(currentHealth);
    }

    /// <summary>
    /// Creates heart UI elements
    /// </summary>
    private void CreateHearts(int heartCount)
    {
        if (heartContainer == null)
        {
            Debug.LogError($"[PercentUI] Heart container not assigned for Player {playerIndex + 1}!");
            return;
        }

        if (heartPrefab == null)
        {
            Debug.LogError($"[PercentUI] Heart prefab not assigned for Player {playerIndex + 1}!");
            return;
        }

        // Create hearts - sprite is already on the prefab
        for (int i = 0; i < heartCount; i++)
        {
            Instantiate(heartPrefab, heartContainer);
        }

        // If using a Horizontal Layout Group, the spacing is handled automatically
        HorizontalLayoutGroup layoutGroup = heartContainer.GetComponent<HorizontalLayoutGroup>();
        if (layoutGroup != null)
        {
            layoutGroup.spacing = heartSpacing;
        }
    }

    public void UpdateCharges(int currentCharges, int maxCharges)
    {
        if (chargesText != null)
            chargesText.text = $"{currentCharges} / {maxCharges}";

        if (currentCharges == 0)
        {
            // let text flash red and then back to normal
            StartCoroutine(FlashCharges());
        }
    }

    public IEnumerator FlashCharges()
    {
        chargesText.color = Color.red;
        yield return new WaitForSeconds(0.2f);
        chargesText.color = textColor;
    }
}
