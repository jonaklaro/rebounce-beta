using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using FMOD.Studio;

public class JoinManager : MonoBehaviour
{
    public static JoinManager Instance;
    public List<PlayerInput> joinedPlayers = new List<PlayerInput>();
    private PlayerInputManager inputManager;
    public Transform playerListParent;

    private EventReference musicEvent;
    private EventInstance musicEventInstance;
    private string musicEventPath = "event:/LobbyMusic";

    private int selectedLevelIndex = 0;

    
    void Awake()
    {
        Instance = this;
        inputManager = GetComponent<PlayerInputManager>();
    }



    void Start()
    {
        // musicEvent = RuntimeManager.PathToEventReference(musicEventPath);
        // musicEventInstance = RuntimeManager.CreateInstance(musicEvent);
        // musicEventInstance.start();
    }
    
    public void OnPlayerJoined(PlayerInput player)
    {
        // Mouse-only joins are invalid → reject
        if (player.devices.Count == 1 && player.devices[0] is Mouse)
        {
            Debug.LogWarning("Rejected mouse-only join");
            Destroy(player.gameObject);
            return;
        }
        
        InputDevice device = player.devices[0];
        string scheme;
        
        if (device is Gamepad)
        {
            scheme = "Player1"; // gamepad scheme
        }
        else
            return;
        
        int logicalIndex = GameData.JoinedPlayers.Count; // 0,1,2,...
        
        JoinedPlayerData data = new JoinedPlayerData
        {
            playerIndex = player.playerIndex,
            controlScheme = scheme,
            deviceId = device.deviceId,
            devices = new InputDevice[] { device }
        };
        
        GameData.JoinedPlayers.Add(data);
        joinedPlayers.Add(player);
        
        // Reparent UI under the list
        player.transform.SetParent(playerListParent, false);
        
        // Update text - use the LIST INDEX for display
        LobbyPlayer lobbyUI = player.GetComponent<LobbyPlayer>();
        lobbyUI.Setup(logicalIndex, player.devices[0]);
    }
    
    public void OnPlayerLeft(PlayerInput player)
    {
        Debug.Log("OnPlayerLeft called");
        if (player == null) return;
        
        int playerIndex = player.playerIndex;
        
        // Remove from local list
        joinedPlayers.Remove(player);
        
        // Remove from GameData
        JoinedPlayerData dataToRemove = GameData.JoinedPlayers.Find(p => p.devices[0] == player.devices[0]);
        if (dataToRemove != null)
        {
            GameData.JoinedPlayers.Remove(dataToRemove);
            Debug.Log($"Removed player {playerIndex} from GameData");
        }

        // RE-INDEX
        for (int i = 0; i < GameData.JoinedPlayers.Count; i++)
        {
            GameData.JoinedPlayers[i].playerIndex = i;
            
            // Update the visual/UI for remaining players
            LobbyPlayer lp = joinedPlayers[i].GetComponent<LobbyPlayer>();
            if (lp != null) lp.Setup(i, joinedPlayers[i].devices[0]);
        }
        
        // Destroy the GameObject
        Destroy(player.gameObject);
        
        Debug.Log($"Player {playerIndex} left. Remaining players: {joinedPlayers.Count}");
    }

    public void SetLevelSelection(int index)
    {
        selectedLevelIndex = index;
        Debug.Log($"Level selection changed to: {index}");
    }
    
    public void StartGame()
    {
        if (GameData.JoinedPlayers.Count < 1)
        {
            // Debug.LogWarning("Not enough players to start");
            // return;
        }

        // musicEventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        inputManager.DisableJoining();
        GameData.JoinedPlayers.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));

        // 4. Revised level selection logic
        if (selectedLevelIndex == 0) // Random
        {
            int randomLevel = Random.Range(0, 2);
            GameData.selectedLevel = (randomLevel == 0) ? "OfficeLevel" : "PirateLevel";
        }
        else if (selectedLevelIndex == 1) // Office
        {
            GameData.selectedLevel = "OfficeLevel";
        }
        else if (selectedLevelIndex == 2) // Pirate
        {
            GameData.selectedLevel = "PirateLevel";
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(GameData.selectedLevel);
    }

    public void ShowLogs()
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, "GameLogs");

        // Ensure the directory exists before trying to open it
        if (!System.IO.Directory.Exists(path))
        {
            System.IO.Directory.CreateDirectory(path);
        }

        // Replace backslashes with forward slashes for cross-platform path compatibility
        path = path.Replace(@"\", "/");

        if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer)
        {
            System.Diagnostics.Process.Start("explorer.exe", path.Replace("/", @"\"));
        }
        else if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
        {
            System.Diagnostics.Process.Start("open", path);
        }
        else if (Application.platform == RuntimePlatform.LinuxEditor || Application.platform == RuntimePlatform.LinuxPlayer)
        {
            System.Diagnostics.Process.Start("xdg-open", path);
        }
        else
        {
            Debug.LogWarning("File explorer opening is not supported on this platform.");
        }
    }

    public void ExitGame()
    {
        Application.Quit();
    }
    
    public List<JoinedPlayerData> GetSortedJoinedPlayers()
    {
        // Sicherheitscheck: falls Liste noch null ist
        if (GameData.JoinedPlayers == null)
            return new List<JoinedPlayerData>();

        // Kopie erstellen, damit du die Original-Liste nicht veränderst
        List<JoinedPlayerData> result = new List<JoinedPlayerData>(GameData.JoinedPlayers);
        result.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
        return result;
    }

    public int GetPlayerCount()
    {
        return GameData.JoinedPlayers != null ? GameData.JoinedPlayers.Count : 0;
    }
}