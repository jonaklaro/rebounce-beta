using System;
using UnityEngine;

/// <summary>
/// Manages the percent meter system for ReBounce.
/// Tracks damage accumulation and calculates percent increases based on collisions.
/// Percentage is stored as a float where 1.0 = 100%
/// </summary>
public class PercentMeter : MonoBehaviour
{
    [Header("Percent Meter Settings")]
    [SerializeField] private float currentPercent = 0f;
    [SerializeField] private float maxPercent = 45f; // 999% maximum

    [SerializeField] private float initialPercent;
    
    [Header("Balance Variables")]
    [SerializeField] private float balanceMultiplier = 1.0f; // B variable from GDD
    
    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;
    
    // Referenz auf das UI des Spielers
    [Header("UI Reference")]
    [SerializeField] public PercentUI percentUI;

    // Public property to get current percent value for knockback calculations
    // Returns P value (currentPercent / 100) as defined in GDD
    public float P => currentPercent;
    
    // Get percentage as display value (e.g., 1.14f returns 114f for UI)
    public float PercentDisplay => currentPercent * 10f - 50f;
    
    public event Action<float> PercentChanged;

    private CapsuleController player;

    private void Awake()
    {
        player = GetComponent<CapsuleController>();
        
        // Event -> UI verbinden
        PercentChanged += OnPercentChangedUI;
    }

    private void Start()
    {
        ResetPercent();
    }
    
    private void OnDestroy()
    {
        PercentChanged -= OnPercentChangedUI;
    }

    private void OnPercentChangedUI(float displayValue)
    {
        if (percentUI != null)
        {
            percentUI.UpdatePercent(displayValue);
        }
    }

    public void UpdateHealthUI()
    {
        if (percentUI != null)
        {
            percentUI.UpdateHealth(player.playerLives, player.startingLives);
        }
    }

    /// <summary>
    /// Adds percent based on collision with surface.
    /// Formula from GDD: 5 + (B * DnT) + (B * V)
    /// </summary>
    /// <param name="distanceAfterHit">DnT - Distance traveled after hit in meters (rounded down)</param>
    /// <param name="velocity">V - Velocity value (currentSpeed - baseSpeed, rounded down)</param>
    public void AddPercentFromCollision(int distanceAfterHit, int velocity)
    {
        float percentToAdd = 5f + (balanceMultiplier * distanceAfterHit) + (balanceMultiplier * velocity);
        
        // Convert to decimal format (5 damage = 0.05 in our system)
        percentToAdd /= 100f;
        
        AddPercent(percentToAdd);
        
        if (showDebugLogs)
        {
            Debug.Log($"[PercentMeter] Collision damage: {percentToAdd * 100f}% " +
                     $"(DnT: {distanceAfterHit}, V: {velocity}, B: {balanceMultiplier})");
        }
    }

    /// <summary>
    /// Adds percent from sweet spot hit.
    /// </summary>
    /// <param name="sweetSpotPercent">Percent to add from sweet spot (e.g., 10 for punch, 7 for sweep)</param>
    public void AddPercentFromSweetSpot(float sweetSpotPercent)
    {
        // Convert to decimal format
        float percentToAdd = sweetSpotPercent / 10f;
        AddPercent(percentToAdd);
        
        if (showDebugLogs)
        {
            Debug.Log($"[PercentMeter] Sweet spot damage: {sweetSpotPercent}%");
        }
    }

    /// <summary>
    /// Directly adds a percentage value to the meter.
    /// </summary>
    /// <param name="amount">Amount to add (as decimal, e.g., 0.10 = 10%)</param>
    public void AddPercent(float amount)
    {
        float oldPercent = currentPercent;
        currentPercent = Mathf.Min(currentPercent + amount, maxPercent);
        
        if (showDebugLogs)
        {
            Debug.Log($"[PercentMeter] Percent increased: {oldPercent * 100f}% -> {currentPercent * 100f}%");
        }
        
        // Trigger any events or UI updates here
        OnPercentChanged();
    }

    /// <summary>
    /// Resets the percent meter to zero.
    /// </summary>
    public void ResetPercent()
    {
        
        if (currentPercent > 0 && player.playerLives > 0)
        {
            GameplayLogger.Instance.LogPlayerLifeLost(
            player.playerInputNumber, 
            currentPercent, 
            player.playerLives  // Lives AFTER the knockout
        );
        }
        
        currentPercent = initialPercent;
        OnPercentChanged();
        
        if (showDebugLogs)
        {
            Debug.Log("[PercentMeter] Percent reset to 0%");
        }
    }

    /// <summary>
    /// Sets the percent meter to a specific value.
    /// </summary>
    /// <param name="newPercent">New percent value (as decimal, e.g., 1.14 = 114%)</param>
    public void SetPercent(float newPercent)
    {
        currentPercent = Mathf.Clamp(newPercent, 0f, maxPercent);
        OnPercentChanged();
    }

    /// <summary>
    /// Returns the current percent value (for use in knockback formulas).
    /// This is the P variable from the GDD.
    /// </summary>
    public float GetPercent()
    {
        return currentPercent;
    }

    /// <summary>
    /// Returns the current percent as a display value (0-999).
    /// </summary>
    public float GetPercentDisplay()
    {
        return PercentDisplay;
    }

    /// <summary>
    /// Checks if player is at high percent (for visual/gameplay effects).
    /// </summary>
    public bool IsHighPercent(float threshold = 1.0f)
    {
        return currentPercent >= threshold;
    }

    /// <summary>
    /// Sets the balance multiplier for testing/balancing purposes.
    /// </summary>
    public void SetBalanceMultiplier(float newMultiplier)
    {
        balanceMultiplier = newMultiplier;
        
        if (showDebugLogs)
        {
            Debug.Log($"[PercentMeter] Balance multiplier set to: {balanceMultiplier}");
        }
    }

    /// <summary>
    /// Called whenever the percent value changes.
    /// Override or add event here for UI updates, visual effects, etc.
    /// </summary>
    private void OnPercentChanged()
    {
        PercentChanged?.Invoke(PercentDisplay);

        GameplayLogger.Instance.UpdatePlayerPercent(player.playerInputNumber, currentPercent);
        // TODO: Trigger UI update event
        // TODO: Trigger visual effects based on percent level
        // Example: if (currentPercent >= 1.0f) TriggerHighPercentEffect();
    }

    // Debug visualization
    private void OnGUI()
    {
        if (showDebugLogs)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 20;
            style.normal.textColor = Color.white;
            
            GUI.Label(new Rect(10, 10 + (gameObject.name.Contains("2") ? 30 : 0), 200, 30), 
                     $"{gameObject.name}: {PercentDisplay:F0}%", style);
        }
    }

    public bool IsWallDestoying()
    {
        return currentPercent >= maxPercent;
    }
    
    public void SetHud(PercentUI ui)
    {
        percentUI = ui;
        // Beim Setzen direkt den aktuellen Wert anzeigen
        percentUI.UpdatePercent(PercentDisplay);
    }
}