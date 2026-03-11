using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using UnityEngine.InputSystem;
using BounceType = BounceReflector.BounceSurfaceType;


// ============================================================================
// ENUMS
// ============================================================================
public enum AttackType
{
    PunchCharged,
    PunchQuick,
    LSweep,
    RSweep
}


// ============================================================================
// DATA STRUCTURES
// ============================================================================

// Serializable wrapper for attack stats (JsonUtility can't serialize Dictionaries)
[System.Serializable]
public class AttackStats
{
    public AttackType attackType;
    public int usageCount;
    public int hitCount;
    public int sweetSpotCount;
    public float totalKnockback;
    public float totalPercentDamage;
}

[System.Serializable]
public class BounceTypeStat
{
    public BounceType bounceType;
    public int count;
}

[System.Serializable]
public class PlayerMetrics
{
    public string playerID;
    public int playerIndex;
    
    // Attack Effectiveness (internal dictionaries for easy access)
    [System.NonSerialized]
    public Dictionary<AttackType, int> attackUsageCount = new Dictionary<AttackType, int>();
    [System.NonSerialized]
    public Dictionary<AttackType, int> attackHitCount = new Dictionary<AttackType, int>();
    [System.NonSerialized]
    public Dictionary<AttackType, int> sweetSpotHitCount = new Dictionary<AttackType, int>();
    [System.NonSerialized]
    public Dictionary<AttackType, float> totalKnockbackGenerated = new Dictionary<AttackType, float>();
    [System.NonSerialized]
    public Dictionary<AttackType, float> totalPercentDamageDealt = new Dictionary<AttackType, float>();
    public List<BounceSequence> bounceSequences = new List<BounceSequence>();
    [System.NonSerialized] public float lastBounceTime = -100f; 
    [System.NonSerialized] public BounceSequence currentActiveSequence = null;

    [System.NonSerialized]
    public Dictionary<int, float> damageTakenFromPlayers = new Dictionary<int, float>(); 
    [System.NonSerialized]
    public float damageTakenFromEnvironment = 0f;
    
    // Serializable list for JSON export
    public List<AttackStats> attackStats = new List<AttackStats>();
    
    public int successfulParries = 0;
    public int failedParries = 0;

    // List to store killed players
    public List<int> killedPlayers = new List<int>();
    public List<int> killedByPlayers = new List<int>();
    
    // Resource Management
    public int dashUsageCount = 0;
    public List<float> dashStockSnapshots = new List<float>(); // Sampled periodically
    public int dashesToHit = 0;
    public int dashesToDodge = 0;
    
    // Percent Meter Data
    public List<float> percentAtKnockout = new List<float>(); // Player's percent when they got KO'd
    public List<float> percentAtKnockoutDealt = new List<float>(); // Opponent's percent when this player KO'd them
    public List<PerLifePercentData> percentMilestonesByLife = new List<PerLifePercentData>();
    
    // Spatial Data
    public List<Vector2> hitPositions = new List<Vector2>();
    public List<Vector2> knockoutPositions = new List<Vector2>();
    public List<Vector2> movementSamples = new List<Vector2>(); // Sampled periodically
    
    // Match outcome
    public int remainingLives = 0;
    public int finalPlacement = 0; // 1st, 2nd, 3rd, etc.
    
    public PlayerMetrics(string id, int index)
    {
        playerID = id;
        playerIndex = index;
        
        // Initialize attack dictionaries
        foreach (AttackType type in Enum.GetValues(typeof(AttackType)))
        {
            attackUsageCount[type] = 0;
            attackHitCount[type] = 0;
            sweetSpotHitCount[type] = 0;
            totalKnockbackGenerated[type] = 0f;
            totalPercentDamageDealt[type] = 0f;
        }
    }
}

[System.Serializable]
public class PerLifePercentData
{
    public int lifeNumber;
    public float timeTo100Percent = -1f; // -1 means not reached
    public float timeTo200Percent = -1f;
    public float timeTo300Percent = -1f;
    public float lifeDuration = 0f;
    public float finalPercent = 0f;
}

[System.Serializable]
public class MatchMetrics
{
    public string matchID;
    public string levelName;
    public DateTime matchStartTime;
    public DateTime matchEndTime;
    public float matchDuration;
    
    // Match Outcome
    public int playerCount = 0;
    public int winnerIndex = -1;
    public List<int> finalPlacements = new List<int>(); // Ordered list of player indices by placement
    
    // Percent Meter Milestones (tracked globally)
    public List<float> timeToReach100Percent = new List<float>();
    public List<float> timeToReach200Percent = new List<float>();
    public List<float> timeToReach400Percent = new List<float>();
    
    // Bounce Dynamics
    public List<BounceTypeStat> bounceTypeStats = new List<BounceTypeStat>();
    public int totalBounces = 0;
    [System.NonSerialized]
    public Dictionary<BounceType, int> bounceTypeCount = new Dictionary<BounceType, int>();
    
    // Map Interaction
    public int breakableWallsDestroyed = 0;
    public int breakableObjectsDestroyed = 0;
    public int normalWallsDestroyed = 0;
    // public int bouncepadActivations = 0;
    // public int normalWallBounces = 0;
    // public int specialWallBounces = 0;
    // public int breakableWallBounces = 0;
    // public int breakableObjectBounces = 0;

    public int knockoutsByOutOfBounds = 0;

    public int inititalLives = 0;
    
    public MatchMetrics(int numPlayers)
    {
        matchID = Guid.NewGuid().ToString();
        matchStartTime = DateTime.Now;
        playerCount = numPlayers;
        
        // Initialize the dictionary for easy logic during the game
        foreach (BounceType type in Enum.GetValues(typeof(BounceType)))
        {
            bounceTypeCount[type] = 0;
        }
    }
}

[System.Serializable]
public class BounceSequence
{
    public List<BounceType> sequence = new List<BounceType>();
    public List<Vector2> positions = new List<Vector2>();
    public List<float> velocities = new List<float>();
    public bool endedInKnockout = false;
    public int affectedPlayerIndex = -1;
}

// ============================================================================
// MAIN LOGGING MANAGER (SINGLETON)
// ============================================================================
public class GameplayLogger : MonoBehaviour
{
    private static GameplayLogger _instance;
    public static GameplayLogger Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("GameplayLogger");
                _instance = go.AddComponent<GameplayLogger>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // Current match data
    public MatchMetrics currentMatch;
    private Dictionary<int, PlayerMetrics> playerMetrics = new Dictionary<int, PlayerMetrics>();
    private Dictionary<int, PerLifeTracker> lifeTrackers = new Dictionary<int, PerLifeTracker>();
    
    // Sampling settings
    [SerializeField] private float movementSampleInterval = 0.5f; // Sample movement every 0.5s
    [SerializeField] private float dashStockSampleInterval = 1f; // Sample dash stock every 1s
    private float movementSampleTimer = 0f;
    private float dashStockSampleTimer = 0f;
    
    // File management
    [SerializeField] private string logDirectory = "GameLogs";
    
    // private BounceSequence currentBounceSequence;

    private class PerLifeTracker
    {
        public int currentLife = 1;
        public float lifeStartTime;
        public float lastPercent = 0f;
        private static readonly float[] milestones = { 10f, 20f, 30f };
        
        public PerLifeTracker()
        {
            lifeStartTime = Time.time;
        }
        
        public void CheckMilestones(float newPercent, PerLifePercentData lifeData)
        {
            // Check each milestone threshold
            for (int i = 0; i < milestones.Length; i++)
            {
                // Did we just cross this milestone?
                if (lastPercent < milestones[i] && newPercent >= milestones[i])
                {
                    float timeElapsed = Time.time - lifeStartTime;
                    
                    switch (i)
                    {
                        case 0: lifeData.timeTo100Percent = timeElapsed; break;
                        case 1: lifeData.timeTo200Percent = timeElapsed; break;
                        case 2: lifeData.timeTo300Percent = timeElapsed; break;
                    }
                }
            }
            
            lastPercent = newPercent;
        }
        
        public void StartNewLife()
        {
            currentLife++;
            lifeStartTime = Time.time;
            lastPercent = 0f;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (currentMatch == null) return;
        
        // Sample movement periodically
        movementSampleTimer += Time.deltaTime;
        if (movementSampleTimer >= movementSampleInterval)
        {
            movementSampleTimer = 0f;
            // Movement sampling happens when you call SamplePlayerPosition from external scripts
        }
        
        // Sample dash stock periodically
        dashStockSampleTimer += Time.deltaTime;
        if (dashStockSampleTimer >= dashStockSampleInterval)
        {
            dashStockSampleTimer = 0f;
            // Dash sampling happens when you call SamplePlayerDashStock from external scripts
        }
    }

    // ========================================================================
    // MATCH LIFECYCLE
    // ========================================================================
    
    /// <summary>
    /// Start a new match with any number of players
    /// </summary>
    /// <param name="playerIDs">Array of player identifiers (can be names, IDs, etc.)</param>
    public void StartMatch(params string[] playerIDs)
    {
        if (playerIDs == null || playerIDs.Length == 0)
        {
            Debug.LogError("StartMatch requires at least one player ID!");
            return;
        }
        
        currentMatch = new MatchMetrics(playerIDs.Length);
        playerMetrics.Clear();
        lifeTrackers.Clear();
        
        for (int i = 0; i < playerIDs.Length; i++)
        {
            playerMetrics[i] = new PlayerMetrics(playerIDs[i], i);
            lifeTrackers[i] = new PerLifeTracker();
            
            // Initialize first life
            playerMetrics[i].percentMilestonesByLife.Add(new PerLifePercentData { lifeNumber = 1 });
        }

        currentMatch.levelName = GameData.selectedLevel;
        
        Debug.Log($"Match started: {currentMatch.matchID} with {playerIDs.Length} players");
    }

    public void UpdatePlayerPercent(int playerIndex, float currentPercent)
    {
        PlayerMetrics player = GetPlayerMetrics(playerIndex);
        if (player == null || !lifeTrackers.ContainsKey(playerIndex)) return;
        
        var tracker = lifeTrackers[playerIndex];
        var currentLifeData = player.percentMilestonesByLife[tracker.currentLife - 1];
        
        tracker.CheckMilestones(currentPercent, currentLifeData);
    }
    
    public void LogPlayerLifeLost(int playerIndex, float finalPercent, int livesRemaining)
    {
        PlayerMetrics player = GetPlayerMetrics(playerIndex);
        if (player == null || !lifeTrackers.ContainsKey(playerIndex)) return;
        
        var tracker = lifeTrackers[playerIndex];
        var currentLifeData = player.percentMilestonesByLife[tracker.currentLife - 1];
        
        if (livesRemaining >= currentMatch.inititalLives)
        {
            currentMatch.inititalLives = livesRemaining + 1;
        }

        // Finalize current life
        currentLifeData.lifeDuration = Time.time - tracker.lifeStartTime;
        currentLifeData.finalPercent = finalPercent;
        
        // Only start new life if player has lives remaining
        if (livesRemaining > 0)
        {
            tracker.StartNewLife();
        }

        player.percentMilestonesByLife.Add(new PerLifePercentData { lifeNumber = tracker.currentLife });
    }

    /// <summary>
    /// Start a new match using Unity PlayerInput device IDs
    /// </summary>
    /// <param name="playerInputs">List of PlayerInput components from spawned players</param>
    public void StartMatchFromPlayerInputs(List<PlayerInput> playerInputs)
    {
        if (playerInputs == null || playerInputs.Count == 0)
        {
            Debug.LogError("StartMatchFromPlayerInputs requires at least one PlayerInput!");
            return;
        }

        string[] playerIDs = new string[playerInputs.Count];
        for (int i = 0; i < playerInputs.Count; i++)
        {
            // Use Unity's player index as identifier
            playerIDs[i] = $"Player {playerInputs[i].playerIndex}";
        }

        StartMatch(playerIDs);
    }

    public void StartMatchFromGameObjects(List<GameObject> players)
    {
        if (players == null || players.Count == 0)
        {
            Debug.LogError("StartMatchFromGameObjects requires players!");
            return;
        }

        string[] playerIDs = new string[players.Count];

        for (int i = 0; i < players.Count; i++)
        {
            PlayerInput pi = players[i].GetComponent<PlayerInput>();
            playerIDs[i] = pi != null
                ? $"Player {pi.playerIndex}"
                : $"Player {i}";
        }

        StartMatch(playerIDs);
    }


    /// <summary>
    /// End the match with final placements and remaining lives
    /// </summary>
    /// <param name="placements">Dictionary mapping player index to placement (1st, 2nd, etc.)</param>
    /// <param name="remainingLives">Dictionary mapping player index to remaining lives</param>
    public void EndMatch(Dictionary<int, int> placements, Dictionary<int, int> remainingLives)
    {
        if (currentMatch == null)
        {
            Debug.LogWarning("EndMatch called but no match is active!");
            return;
        }
        
        currentMatch.matchEndTime = DateTime.Now;
        currentMatch.matchDuration = (float)(currentMatch.matchEndTime - currentMatch.matchStartTime).TotalSeconds;
        
        // Find winner (placement == 1)
        foreach (var kvp in placements)
        {
            if (kvp.Value == 1)
            {
                currentMatch.winnerIndex = kvp.Key;
                break;
            }
        }
        
        // Store placements sorted by rank
        currentMatch.finalPlacements = placements
            .OrderBy(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToList();
        
        // Update player metrics with final data
        foreach (var kvp in playerMetrics)
        {
            int playerIndex = kvp.Key;
            PlayerMetrics metrics = kvp.Value;
            
            if (placements.ContainsKey(playerIndex))
                metrics.finalPlacement = placements[playerIndex];
            
            if (remainingLives.ContainsKey(playerIndex))
                metrics.remainingLives = remainingLives[playerIndex];
        }
        
        SaveMatchData();

        GameData.playerMetrics = new Dictionary<int, PlayerMetrics>(playerMetrics);
        GameData.currentMatch = currentMatch;
        Debug.Log($"Saved {GameData.playerMetrics.Count} players to GameData.");        
        // Clear for next match
        currentMatch = null;
        playerMetrics.Clear();
        // currentBounceSequence = null;
    }

    /// <summary>
    /// End match by providing all player GameObjects. Winner is determined by who is still alive.
    /// Assumes you have a CapsuleController component with isDead and lives properties.
    /// </summary>
    /// <param name="winnerIndex">Index of the winning player</param>
    /// <param name="allPlayers">List of all player GameObjects</param>
    public void EndMatchFromGameObjects(int winnerIndex, List<GameObject> allPlayers)
    {
        if (allPlayers == null || allPlayers.Count == 0)
        {
            Debug.LogError("EndMatchFromGameObjects requires player GameObjects!");
            return;
        }

        var placements = new Dictionary<int, int>();
        var remainingLives = new Dictionary<int, int>();

        // Extract data from each player GameObject
        for (int i = 0; i < allPlayers.Count; i++)
        {
            CapsuleController controller = allPlayers[i].GetComponent<CapsuleController>();
            
            if (controller != null)
            {
                // Winner gets 1st place, all others get 2nd, 3rd, etc.
                placements[i] = (i == winnerIndex) ? 1 : (i < winnerIndex ? i + 2 : i + 1);
                
                // Get remaining lives from controller
                // Adjust this based on your actual CapsuleController implementation
                remainingLives[i] = controller.playerLives; // Assumes you have a 'lives' property
            }
            else
            {
                Debug.LogWarning($"Player {i} missing CapsuleController component!");
                placements[i] = allPlayers.Count; // Put at end if no controller
                remainingLives[i] = 0;
            }
        }

        // Sort placements: winner first, then by remaining lives (descending)
        var sortedPlayers = allPlayers
            .Select((player, index) => new { 
                Index = index, 
                Lives = player.GetComponent<CapsuleController>()?.playerLives ?? 0,
                IsWinner = index == winnerIndex
            })
            .OrderByDescending(p => p.IsWinner)
            .ThenByDescending(p => p.Lives)
            .ToList();

        // Reassign placements based on sorted order
        for (int placement = 1; placement <= sortedPlayers.Count; placement++)
        {
            placements[sortedPlayers[placement - 1].Index] = placement;
        }

        EndMatch(placements, remainingLives);
    }

    /// <summary>
    /// Simplified EndMatch for 1v1 scenarios (kept for backward compatibility)
    /// </summary>
    public void EndMatch(int winnerIndex, int player0Lives, int player1Lives)
    {
        var placements = new Dictionary<int, int>
        {
            { winnerIndex, 1 },
            { 1 - winnerIndex, 2 }
        };
        
        var lives = new Dictionary<int, int>
        {
            { 0, player0Lives },
            { 1, player1Lives }
        };
        
        EndMatch(placements, lives);
    }

    // ========================================================================
    // ATTACK LOGGING
    // ========================================================================
    
    public void LogAttackUsed(int playerIndex, AttackType attackType)
    {
        PlayerMetrics player = GetPlayerMetrics(playerIndex);
        if (player == null) return;
        
        player.attackUsageCount[attackType]++;
    }

    public void LogAttackHit(int attackerIndex, int victimIndex, AttackType attackType, bool wasSweetSpot, 
                             float knockbackGenerated, float percentDamage, Vector2 hitPosition)
    {
        PlayerMetrics attacker = GetPlayerMetrics(attackerIndex);
        if (attacker == null) return;
        
        attacker.attackHitCount[attackType]++;
        if (wasSweetSpot) attacker.sweetSpotHitCount[attackType]++;
        
        attacker.totalKnockbackGenerated[attackType] += knockbackGenerated;
        attacker.totalPercentDamageDealt[attackType] += percentDamage;
        attacker.hitPositions.Add(hitPosition);

        PlayerMetrics victim = GetPlayerMetrics(victimIndex);
        if (victim != null) {
            if (!victim.damageTakenFromPlayers.ContainsKey(attackerIndex))
                victim.damageTakenFromPlayers.Add(attackerIndex, 0f);

            victim.damageTakenFromPlayers[attackerIndex] += percentDamage;
        }
    }

    public void LogEnvironmentDamage(int victimIndex, float amount)
    {
        PlayerMetrics victim = GetPlayerMetrics(victimIndex);
        if (victim != null)
        {
            victim.damageTakenFromEnvironment += amount*10f;
        }
    }

    public void LogParry(int playerIndex, bool successful)
    {
        PlayerMetrics player = GetPlayerMetrics(playerIndex);
        if (player == null) return;
        
        if (successful) player.successfulParries++;
        else player.failedParries++;
    }

    // ========================================================================
    // KNOCKOUT & PERCENT LOGGING
    // ========================================================================
    
    public void LogKnockout(int knockedOutPlayerIndex, float theirPercent, Vector2 koPosition, int attackerIndex)
    /// <summary>
    /// Log a knockout event
    /// </summary>
    /// <param name="knockedOutPlayerIndex">Index of the player who was knocked out</param>

    {
        PlayerMetrics victim = GetPlayerMetrics(knockedOutPlayerIndex);
        PlayerMetrics attacker = GetPlayerMetrics(attackerIndex);
        
        if (victim != null)
        {
            victim.percentAtKnockout.Add(theirPercent);
            victim.knockoutPositions.Add(koPosition);
        }

        if (attackerIndex != -1 && attacker != null) {
            attacker.killedPlayers.Add(knockedOutPlayerIndex);
            victim.killedByPlayers.Add(attackerIndex);
        }
        
        // if (attacker != null)
        // {
            // attacker.percentAtKnockoutDealt.Add(theirPercent); // Store opponent's percent
        // }
    }

    public void LogPercentMilestone(float milestone, float timeElapsed)
    {
        if (currentMatch == null) return;
        
        if (Mathf.Approximately(milestone, 100f))
            currentMatch.timeToReach100Percent.Add(timeElapsed);
        else if (Mathf.Approximately(milestone, 200f))
            currentMatch.timeToReach200Percent.Add(timeElapsed);
        else if (Mathf.Approximately(milestone, 400f))
            currentMatch.timeToReach400Percent.Add(timeElapsed);
    }

    // ========================================================================
    // BOUNCE LOGGING
    // ========================================================================
    
    // public void StartBounceSequence(int playerIndex)
    // {
    //     currentBounceSequence = new BounceSequence();
    //     currentBounceSequence.affectedPlayerIndex = playerIndex;
    // }

    // public void LogBounce(BounceType bounceType, Vector2 position, float velocity)
    // {
    //     // if (currentBounceSequence == null) return;
        
    //     // currentBounceSequence.sequence.Add(bounceType);
    //     // currentBounceSequence.positions.Add(position);
    //     // currentBounceSequence.velocities.Add(velocity);
        
    //     if (currentMatch != null)
    //     {
    //         currentMatch.totalBounces++;
    //         currentMatch.bounceTypeCount[bounceType]++;
            
    //         if (bounceType == BounceType.BouncePad)
    //             currentMatch.bouncepadActivations++;
    //         else if (bounceType == BounceType.StandardWall)
    //             currentMatch.normalWallBounces++;
    //         else if (bounceType == BounceType.SpecialWall)
    //             currentMatch.specialWallBounces++;
    //         else if (bounceType == BounceType.BreakableWall)
    //             currentMatch.breakableWallBounces++;
    //         else if (bounceType == BounceType.BreakableObject)
    //             currentMatch.breakableObjectBounces++;
            
    //     }
    // }

    [SerializeField] private float sequenceTimeout = 1.0f;

    public void LogBounce(BounceType bounceType, Vector2 position, float velocity, int playerIndex)
    {
        PlayerMetrics player = GetPlayerMetrics(playerIndex);
        if (player == null || currentMatch == null) return;

        // 1. Update Global Match Stats
        currentMatch.totalBounces++;
        currentMatch.bounceTypeCount[bounceType]++;

        // 2. Handle Sequence Logic
        float currentTime = Time.time;
        
        // If it's been too long since the last bounce, or no sequence exists, start a new one
        if (player.currentActiveSequence == null || (currentTime - player.lastBounceTime) > sequenceTimeout)
        {
            player.currentActiveSequence = new BounceSequence();
            player.currentActiveSequence.affectedPlayerIndex = playerIndex;
            player.bounceSequences.Add(player.currentActiveSequence);
        }

        // 3. Log data to the player's current sequence
        player.currentActiveSequence.sequence.Add(bounceType);
        player.currentActiveSequence.positions.Add(position);
        player.currentActiveSequence.velocities.Add(velocity);
        
        player.lastBounceTime = currentTime;
    }

    // public void EndBounceSequence(bool endedInKO)
    // {
    //     if (currentBounceSequence == null) return;
        
    //     currentBounceSequence.endedInKnockout = endedInKO;
        
    //     if (currentMatch != null)
    //         currentMatch.bounceSequences.Add(currentBounceSequence);
        
    //     currentBounceSequence = null;
    // }

    // ========================================================================
    // RESOURCE & MAP LOGGING
    // ========================================================================
    
    public void LogDashUsed(int playerIndex, bool ledToHit, bool ledToDodge)
    {
        PlayerMetrics player = GetPlayerMetrics(playerIndex);
        if (player == null) return;
        
        player.dashUsageCount++;
        if (ledToHit) player.dashesToHit++;
        if (ledToDodge) player.dashesToDodge++;
    }

    public void LogBreakableDestroyed(BounceType surfaceType)
    {
        if (currentMatch == null) return;
        
        // if (isWall) currentMatch.breakableWallsDestroyed++;
        // else currentMatch.breakableObjectsDestroyed++;

        if (surfaceType == BounceType.BreakableWall)
            currentMatch.breakableWallsDestroyed++;
        else if (surfaceType == BounceType.BreakableObject)
            currentMatch.breakableObjectsDestroyed++;
        else if (surfaceType == BounceType.StandardWall)
            currentMatch.normalWallsDestroyed++;
    }

    public void LogOutOfBoundsKO()
    {
        if (currentMatch == null) return;
        currentMatch.knockoutsByOutOfBounds++;
    }

    // ========================================================================
    // SAMPLING (Called from external scripts)
    // ========================================================================
    
    public void SamplePlayerPosition(int playerIndex, Vector2 position)
    {
        PlayerMetrics player = GetPlayerMetrics(playerIndex);
        if (player == null) return;
        
        player.movementSamples.Add(position);
    }

    public void SamplePlayerDashStock(int playerIndex, float dashStock)
    {
        PlayerMetrics player = GetPlayerMetrics(playerIndex);
        if (player == null) return;
        
        player.dashStockSnapshots.Add(dashStock);
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================
    
    private PlayerMetrics GetPlayerMetrics(int playerIndex)
    {
        if (playerMetrics.ContainsKey(playerIndex))
            return playerMetrics[playerIndex];
        
        Debug.LogWarning($"Invalid player index: {playerIndex}");
        return null;
    }

    public int GetPlayerCount()
    {
        return playerMetrics.Count;
    }

    // ========================================================================
    // FILE SAVING
    // ========================================================================
    
    private void SaveMatchData()
    {
        string directoryPath = Path.Combine(Application.persistentDataPath, logDirectory);
        
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);
        
        string timestamp = currentMatch.matchStartTime.ToString("yyyy-MM-dd_HH-mm-ss");
        string matchFolder = Path.Combine(directoryPath, 
            $"Match_{currentMatch.playerCount}P_{timestamp}_{currentMatch.matchID.Substring(0, 8)}");
        
        Directory.CreateDirectory(matchFolder);
        
        SaveAsJSON(matchFolder);
        SaveAsCSV(matchFolder);
        
        Debug.Log($"Match data saved to: {matchFolder}");
    }

    private void SaveAsJSON(string folderPath)
    {
        if (currentMatch != null)
        {
            currentMatch.bounceTypeStats.Clear();
            foreach (var kvp in currentMatch.bounceTypeCount)
            {
                currentMatch.bounceTypeStats.Add(new BounceTypeStat 
                { 
                    bounceType = kvp.Key, 
                    count = kvp.Value 
                });
            }
        }

        // Convert dictionaries to serializable lists for each player
        foreach (var kvp in playerMetrics)
        {
            kvp.Value.attackStats.Clear();
            
            foreach (AttackType type in Enum.GetValues(typeof(AttackType)))
            {
                kvp.Value.attackStats.Add(new AttackStats
                {
                    attackType = type,
                    usageCount = kvp.Value.attackUsageCount[type],
                    hitCount = kvp.Value.attackHitCount[type],
                    sweetSpotCount = kvp.Value.sweetSpotHitCount[type],
                    totalKnockback = kvp.Value.totalKnockbackGenerated[type],
                    totalPercentDamage = kvp.Value.totalPercentDamageDealt[type]
                });
            }
        }
        
        // Save match summary
        string matchJson = JsonUtility.ToJson(currentMatch, true);
        File.WriteAllText(Path.Combine(folderPath, "match_summary.json"), matchJson);
        
        // Save player metrics for each player
        foreach (var kvp in playerMetrics)
        {
            string playerJson = JsonUtility.ToJson(kvp.Value, true);
            File.WriteAllText(Path.Combine(folderPath, $"player{kvp.Key}_metrics.json"), playerJson);
        }
    }

    private void SaveAsCSV(string folderPath)
    {
        // Match Summary CSV
        StringBuilder matchSB = new StringBuilder();
        matchSB.AppendLine("MatchID,StartTime,EndTime,Duration,PlayerCount,Winner,TotalBounces,WallsDestroyed,ObjectsDestroyed,OOBKnockouts");
        matchSB.AppendLine($"{currentMatch.matchID},{currentMatch.matchStartTime},{currentMatch.matchEndTime}," +
                          $"{currentMatch.matchDuration},{currentMatch.playerCount},{currentMatch.winnerIndex}," +
                          $"{currentMatch.totalBounces},{currentMatch.breakableWallsDestroyed}," +
                          $"{currentMatch.breakableObjectsDestroyed},{currentMatch.knockoutsByOutOfBounds}");
        File.WriteAllText(Path.Combine(folderPath, "match_summary.csv"), matchSB.ToString());
        
        // Player Standings CSV
        StringBuilder standingsSB = new StringBuilder();
        standingsSB.AppendLine("PlayerIndex,PlayerID,Placement,RemainingLives");
        foreach (var kvp in playerMetrics.OrderBy(p => p.Value.finalPlacement))
        {
            standingsSB.AppendLine($"{kvp.Key},{kvp.Value.playerID},{kvp.Value.finalPlacement},{kvp.Value.remainingLives}");
        }
        File.WriteAllText(Path.Combine(folderPath, "player_standings.csv"), standingsSB.ToString());
        
        // Individual player stats
        foreach (var kvp in playerMetrics)
        {
            SavePlayerAttackStatsCSV(folderPath, kvp.Value, $"player{kvp.Key}");
            SaveSpatialDataCSV(folderPath, kvp.Value, $"player{kvp.Key}");
        }
        
        // Bounce Sequences CSV
        SaveBounceSequencesCSV(folderPath);
    }

    private void SavePlayerAttackStatsCSV(string folderPath, PlayerMetrics player, string filename)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("AttackType,UsageCount,HitCount,SweetSpotCount,TotalKnockback,TotalPercentDamage,HitRate,SweetSpotRate");
        
        foreach (AttackType type in Enum.GetValues(typeof(AttackType)))
        {
            int usage = player.attackUsageCount[type];
            int hits = player.attackHitCount[type];
            int sweetSpots = player.sweetSpotHitCount[type];
            
            float hitRate = usage > 0 ? (float)hits / usage : 0f;
            float sweetSpotRate = hits > 0 ? (float)sweetSpots / hits : 0f;
            
            sb.AppendLine($"{type},{usage},{hits},{sweetSpots}," +
                         $"{player.totalKnockbackGenerated[type]}," +
                         $"{player.totalPercentDamageDealt[type]}," +
                         $"{hitRate:F3},{sweetSpotRate:F3}");
        }
        
        // Add summary stats
        sb.AppendLine();
        sb.AppendLine($"SuccessfulParries,{player.successfulParries}");
        sb.AppendLine($"FailedParries,{player.failedParries}");
        sb.AppendLine($"DashUsage,{player.dashUsageCount}");
        sb.AppendLine($"DashesToHit,{player.dashesToHit}");
        sb.AppendLine($"DashesToDodge,{player.dashesToDodge}");
        
        File.WriteAllText(Path.Combine(folderPath, $"{filename}_attacks.csv"), sb.ToString());
    }

    private void SaveSpatialDataCSV(string folderPath, PlayerMetrics player, string filename)
    {
        // Hit Positions
        StringBuilder hitSB = new StringBuilder();
        hitSB.AppendLine("X,Y");
        foreach (var pos in player.hitPositions)
            hitSB.AppendLine($"{pos.x},{pos.y}");
        File.WriteAllText(Path.Combine(folderPath, $"{filename}_hit_positions.csv"), hitSB.ToString());
        
        // Knockout Positions
        StringBuilder koSB = new StringBuilder();
        koSB.AppendLine("X,Y");
        foreach (var pos in player.knockoutPositions)
            koSB.AppendLine($"{pos.x},{pos.y}");
        File.WriteAllText(Path.Combine(folderPath, $"{filename}_ko_positions.csv"), koSB.ToString());
        
        // Movement Samples
        StringBuilder moveSB = new StringBuilder();
        moveSB.AppendLine("X,Y,SampleIndex");
        for (int i = 0; i < player.movementSamples.Count; i++)
        {
            var pos = player.movementSamples[i];
            moveSB.AppendLine($"{pos.x},{pos.y},{i}");
        }
        File.WriteAllText(Path.Combine(folderPath, $"{filename}_movement.csv"), moveSB.ToString());
    }

    private void SaveBounceSequencesCSV(string folderPath)
    {
        StringBuilder sb = new StringBuilder();
        // Added PlayerID to the header for better clarity in the CSV
        sb.AppendLine("SequenceID,PlayerIndex,PlayerID,BounceCount,EndedInKO,SequenceTypes,AvgVelocity,MaxVelocity");
        
        int globalSequenceCounter = 0;

        // Loop through each player to find their specific sequences
        foreach (var kvp in playerMetrics)
        {
            int pIndex = kvp.Key;
            PlayerMetrics pMetrics = kvp.Value;

            for (int i = 0; i < pMetrics.bounceSequences.Count; i++)
            {
                var seq = pMetrics.bounceSequences[i];
                
                // Format the sequence of types (e.g., "Wall>Wall>BouncePad")
                string types = string.Join(">", seq.sequence);
                
                float avgVel = seq.velocities.Count > 0 ? seq.velocities.Average() : 0f;
                float maxVel = seq.velocities.Count > 0 ? seq.velocities.Max() : 0f;
                
                sb.AppendLine($"{globalSequenceCounter},{pIndex},{pMetrics.playerID},{seq.sequence.Count}," +
                            $"{seq.endedInKnockout},\"{types}\",{avgVel:F2},{maxVel:F2}");
                
                globalSequenceCounter++;
            }
        }
        
        File.WriteAllText(Path.Combine(folderPath, "bounce_sequences.csv"), sb.ToString());
    }
}