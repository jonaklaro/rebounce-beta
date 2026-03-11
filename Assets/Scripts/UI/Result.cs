using UnityEngine;
using TMPro;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Result : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private TextMeshProUGUI playerName;
    [SerializeField] private TextMeshProUGUI remainingLives;
    [SerializeField] private TextMeshProUGUI damageDealt;
    [SerializeField] private TextMeshProUGUI bounces;
    [SerializeField] private TextMeshProUGUI kills;
    [SerializeField] private TextMeshProUGUI successfulParries;
    [SerializeField] private TextMeshProUGUI placement;
    [SerializeField] private TextMeshProUGUI damageTaken;


    [SerializeField] private GameObject statsTable;
    [SerializeField] private GameObject statRowPrefab;
    [SerializeField] private GameObject statTablePrefab;
    [SerializeField] private Image background;

    [Header("Scrolling")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private float scrollSpeed = 2f;
    private int assignedPlayerIndex;
    
    private PlayerInput playerInput;

    private static Color[] playerColors = PlayerColors.playerColors;
    
    void Awake()
    {
        // Get the PlayerInput component on this Result UI
        playerInput = GetComponent<PlayerInput>();
    }

    public void Setup(PlayerMetrics metrics, MatchMetrics matchMetrics)
    {
        assignedPlayerIndex = metrics.playerIndex;
        
        // 1. Basic Setup
        if (background != null) background.color = playerColors[assignedPlayerIndex % playerColors.Length];
        if (playerName != null) playerName.text = $"PLAYER {assignedPlayerIndex + 1}";
        if (placement != null) placement.text = $"{metrics.finalPlacement}{GetOrdinalSuffix(metrics.finalPlacement)} PLACE";

        // 2. Clear existing stats
        foreach (Transform child in statsTable.transform) Destroy(child.gameObject);

        // 3. Create General Stats
        CreateStatRow("Placement", $"{metrics.finalPlacement}{GetOrdinalSuffix(metrics.finalPlacement)}");

        // Math for points
        int points = metrics.killedPlayers.Count + (metrics.remainingLives - matchMetrics.inititalLives);
        CreateStatRow("Points", points.ToString());

        // --- REUSABLE VARIABLES ---
        GameObject nestedTable;
        VerticalLayoutGroup layout;

        // 1. Points Details
        nestedTable = Instantiate(statTablePrefab, statsTable.transform);
        if (nestedTable.TryGetComponent(out layout)) layout.padding.left = 30;
        
        CreateStatRow("Knockouts", (metrics.remainingLives - matchMetrics.inititalLives).ToString(), nestedTable.transform);
        CreateStatRow("KOs", metrics.killedPlayers.Count.ToString(), nestedTable.transform);

        // General Stats
        CreateStatRow("Total Bounces", metrics.bounceSequences.Sum(s => s.sequence.Count).ToString());
        CreateStatRow("Success Parries", $"{metrics.successfulParries} / {metrics.successfulParries + metrics.failedParries}");

        // 2. Outgoing Damage
        float totalDealt = metrics.attackStats.Sum(a => a.totalPercentDamage);
        CreateStatRow("Total Damage Dealt", $"{totalDealt:F1}%");

        if (totalDealt > 0)
        {
            nestedTable = Instantiate(statTablePrefab, statsTable.transform);
            if (nestedTable.TryGetComponent(out layout)) layout.padding.left = 30;

            foreach (var attack in metrics.attackStats.Where(a => a.totalPercentDamage > 0))
            {
                CreateStatRow(attack.attackType.ToString(), $"{attack.totalPercentDamage:F1}%", nestedTable.transform);
            }
        }

        // 3. Incoming Damage
        float totalTaken = metrics.damageTakenFromPlayers.Values.Sum() + metrics.damageTakenFromEnvironment; 
        CreateStatRow("Total Damage Taken", $"{totalTaken:F1}%");

        if (totalTaken > 0)
        {
            nestedTable = Instantiate(statTablePrefab, statsTable.transform);
            if (nestedTable.TryGetComponent(out layout)) layout.padding.left = 30;
            
            foreach (var entry in metrics.damageTakenFromPlayers)
            {
                CreateStatRow($"From Player {entry.Key + 1}", $"{entry.Value:F1}%", nestedTable.transform);
            }

            if (metrics.damageTakenFromEnvironment > 0)
            {
                CreateStatRow("Environment", $"{metrics.damageTakenFromEnvironment:F1}%", nestedTable.transform);
            }
        }
    }

    private void CreateStatRow(string label, string value, Transform parent = null)
    {
        // Use statsTable if no specific parent (like a nested table) is provided
        Transform targetParent = parent != null ? parent : statsTable.transform;
        GameObject row = Instantiate(statRowPrefab, targetParent);
        
        // Assuming your row prefab has a script or specific child names
        var texts = row.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length >= 2)
        {
            texts[0].text = label; // Left Text
            texts[1].text = value; // Right Text
        }
    }

    // Helper to make placement look like "1st", "2nd", etc.
    private string GetOrdinalSuffix(int num)
    {
        if (num <= 0) return "";
        switch (num % 100)
        {
            case 11: case 12: case 13: return "th";
        }
        switch (num % 10)
        {
            case 1: return "st";
            case 2: return "nd";
            case 3: return "rd";
            default: return "th";
        }
    }

    private void Update()
    {
        if (playerInput != null && playerInput.devices.Count > 0)
        {
            Gamepad assignedGamepad = playerInput.devices[0] as Gamepad;
            
            if (assignedGamepad != null)
            {
                float stickY = assignedGamepad.leftStick.y.ReadValue();
                if (Mathf.Abs(stickY) > 0.1f)
                {
                    // Invert stickY if you want "Natural" scrolling
                    scrollRect.verticalNormalizedPosition += stickY * scrollSpeed * Time.deltaTime;
                    scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
                }
            }
        }
    }
}