using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using FMOD.Studio;

public class PlayerSpawnManager : MonoBehaviour
{
    [Header("Player Setup")]
    public GameObject playerPrefab;
    public int numberOfPlayers = 2; // scalable later
    
    [Header("Spawnpoints")]
    public List<Transform> spawnPoints = new List<Transform>();
    private List<Transform> availableSpawns = new List<Transform>();

    public static PlayerSpawnManager Instance;
    public List<GameObject> players = new List<GameObject>();
    
    [Header("HUD Slots")]
    public PercentUI[] playerHudSlots; // Größe 4, Reihenfolge P1,P2,P3,P4 im Inspector setzen

    // Store player input data for respawning
    private Dictionary<CapsuleController, JoinedPlayerData> playerInputData = new Dictionary<CapsuleController, JoinedPlayerData>();
    
    // Store which renderers were originally enabled for each player
    private Dictionary<CapsuleController, List<Renderer>> playerRenderers = new Dictionary<CapsuleController, List<Renderer>>();

    private bool matchEnded = false;

    private EventReference ambient;
    private EventInstance ambientInstance;
    private string ambientPath = "event:/Amb/OfficeAmbience";

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        Debug.Log("Number of joined players: " + GameData.JoinedPlayers.Count);
        numberOfPlayers = GameData.JoinedPlayers.Count;
        //TODO
        //For Debugging: create test players if none exist
        var joined = JoinManager.Instance.GetSortedJoinedPlayers();
        /*if (joined.Count == 0)
        {
            Debug.LogWarning("GameData.JoinedPlayers missing → starting in TEST MODE");
            CreateTestPlayers();
            joined = JoinManager.Instance.GetSortedJoinedPlayers();
        }*/

        GameData.JoinedPlayers = joined; // sicherstellen, dass sortiert ist
        numberOfPlayers = joined.Count;

        for (int i = 0; i < playerHudSlots.Length; i++)
        {
            bool shouldBeActive = i < numberOfPlayers;
            if (playerHudSlots[i] != null)
            {
                playerHudSlots[i].gameObject.SetActive(shouldBeActive);
            }
        }

        if (GameData.selectedLevel != "OfficeLevel")
        {
            ambientPath = "event:/Amb/PirateAmbience";
        }

        // ambient = RuntimeManager.PathToEventReference(ambientPath);
        // ambientInstance = RuntimeManager.CreateInstance(ambient);

        // ambientInstance.start();
        
        StartGameLoop();
    }

    //TODO
    //For Debugging: create test players if none exist
    void CreateTestPlayers()
    {
        GameData.JoinedPlayers = new List<JoinedPlayerData>();
        
        // Player 1 → Keyboard
        GameData.JoinedPlayers.Add(new JoinedPlayerData
        {
            playerIndex = 0,
            controlScheme = "Player1", // your keyboard scheme
            devices = new InputDevice[] { Keyboard.current }
        });

        // Player 2 → Gamepad (if available)
        if (Gamepad.current != null)
        {
            GameData.JoinedPlayers.Add(new JoinedPlayerData
            {
                playerIndex = 1,
                controlScheme = "Player2", // gamepad scheme
                devices = new InputDevice[] { Gamepad.current }
            });
        }
    }

    public void StartGameLoop()
    {
        matchEnded = false;
        players.Clear();

        availableSpawns = new List<Transform>(spawnPoints);

        for (int i = 0; i < numberOfPlayers; i++)
        {
            SpawnPlayer(i);
        }

        // ✅ START LOGGING AFTER ALL PLAYERS EXIST
        GameplayLogger.Instance.StartMatchFromGameObjects(players);
    }

    void SpawnPlayer(int index)
    {
        Transform chosen = availableSpawns[Random.Range(0, availableSpawns.Count)];
        availableSpawns.Remove(chosen);

        GameObject p = Instantiate(playerPrefab, chosen.position, chosen.rotation);
        CapsuleController controller = p.GetComponent<CapsuleController>();
        
        controller.playerNumber = (CapsuleController.PlayerNumber)index;

        controller.playerInputNumber = index;

        PlayerInput pi = p.GetComponent<PlayerInput>();
        JoinedPlayerData data = GameData.JoinedPlayers[index];
        Debug.Log(data.playerIndex + " " + data.controlScheme + " " + data.devices[0]);
        pi.SwitchCurrentControlScheme(data.controlScheme, data.devices);
        pi.defaultControlScheme = data.controlScheme;

        // Store the input data for this player
        playerInputData[controller] = data;
        
        PercentMeter meter = p.GetComponent<PercentMeter>();
        if (meter != null && index < playerHudSlots.Length)
        {
            PercentUI hud = playerHudSlots[index];
            if (hud != null && hud.gameObject.activeSelf)
            {
                // Jetzt liefert PlayerColor die richtige Farbe für diesen Index
                hud.Init(index, controller.PlayerColor,controller.startingLives);
                meter.SetHud(hud);
            }
            else
            {
                Debug.LogWarning($"Kein HUD-Slot für Player {index} gesetzt!");
            }
        }
        else
        {
            Debug.LogWarning($"PercentMeter oder HUD-Slot für Player {index} fehlt!");
        }

        // Store which renderers are enabled by default
        Renderer[] renderers = p.GetComponentsInChildren<Renderer>();
        List<Renderer> enabledRenderers = new List<Renderer>();
        foreach (Renderer r in renderers)
        {
            if (r.enabled)
            {
                enabledRenderers.Add(r);
            }
        }
        playerRenderers[controller] = enabledRenderers;

        players.Add(p);

    }

    public void RespawnPlayer(CapsuleController deadPlayer)
    {
        StartCoroutine(RespawnPlayerCoroutine(deadPlayer));
    }

    private IEnumerator RespawnPlayerCoroutine(CapsuleController deadPlayer)
    {
        // Wait for respawn delay
        yield return new WaitForSeconds(2f);

        deadPlayer.dashController.RefillAllCharges();

        // Choose a new spawn point
        int r = Random.Range(0, spawnPoints.Count);
        Transform spawn = spawnPoints[r];

        // Move the player
        deadPlayer.transform.position = spawn.position;
        deadPlayer.transform.rotation = spawn.rotation;

        // Re-enable only the renderers that were originally enabled
        if (playerRenderers.ContainsKey(deadPlayer))
        {
            foreach (Renderer rend in playerRenderers[deadPlayer])
            {
                if (rend != null) // Safety check
                {
                    rend.enabled = true;
                }
            }
        }

        // Re-enable collision
        Collider col = deadPlayer.GetComponent<Collider>();
        if (col != null) col.enabled = true;

        // Re-enable player input
        PlayerInput pi = deadPlayer.GetComponent<PlayerInput>();
        if (pi != null)
        {
            pi.ActivateInput();
        }
        
        deadPlayer.ReactivateVFX();
    }

    public void CheckIfAllDead()
    {
        if (matchEnded) return;

        int alivePlayers = 0;
        int lastAliveIndex = -1;

        for (int i = 0; i < players.Count; i++)
        {
            CapsuleController controller = players[i].GetComponent<CapsuleController>();
            if (!controller.isDead)
            {
                alivePlayers++;
                lastAliveIndex = i;
            }
            if (controller.gamepad != null)
                controller.gamepad.ResetHaptics();
        }

        if (alivePlayers == 1)
        {
            matchEnded = true;
            EndMatch(lastAliveIndex);
        }
    }

    public void EndMatch(int winnerIndex)
    {
        Debug.Log("Match ended");
        ambientInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);

        foreach (GameObject p in players)
        {
            CapsuleController controller = p.GetComponent<CapsuleController>();
            if (controller.gamepad != null){
                controller.gamepad.ResetHaptics();
            }
        }

        GameData.currentMatch = GameplayLogger.Instance.currentMatch;
        
        // Pass all players to the logger
        GameplayLogger.Instance.EndMatchFromGameObjects(winnerIndex, players);

        // Load Join Scene mit fresh Reload -> GameData reset
        UnityEngine.SceneManagement.SceneManager.LoadScene("ResultScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}