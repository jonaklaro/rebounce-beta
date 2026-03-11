using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using UnityEngine.EventSystems;
using TMPro;
using FMODUnity;
using FMOD.Studio;

public class ResultManager : MonoBehaviour
{
    public GameObject resultPrefab;
    public GameObject resultPanel;
    public GameObject firstSelectedButton;
    public TMP_Text gametimeText;
    private EventReference resultSound;
    [SerializeField] private string resultPath = "event:/Results";

    void Start()
    {
        for (int i = 0; i < GameData.JoinedPlayers.Count; i++)
        {
            JoinedPlayerData playerData = GameData.JoinedPlayers[i];

            // 1. Spawn the Result UI
            GameObject resultObj = Instantiate(resultPrefab, resultPanel.transform);
            
            // 2. Setup Data
            if (GameData.playerMetrics.TryGetValue(i, out PlayerMetrics metrics))
            {
                resultObj.GetComponent<Result>().Setup(metrics, GameData.currentMatch);
            }

            // 3. Re-assign Devices
            PlayerInput pi = resultObj.GetComponent<PlayerInput>();
            if (pi != null && playerData.devices != null && playerData.devices.Length > 0)
            {
                // Unpair default devices assigned on Awake
                pi.user.UnpairDevices();

                // Pair the player data device
                InputUser.PerformPairingWithDevice(playerData.devices[0], pi.user);

                // Set the scheme
                pi.SwitchCurrentControlScheme(playerData.controlScheme, playerData.devices);
                
                Debug.Log($"Player {i + 1} Result UI paired to {playerData.devices[0]}");
            }

        }

        // Set Match Duration (is a float only containing seconds) -> Convert to MM:SS::MS
        int minutes = (int)GameData.currentMatch.matchDuration / 60;
        int seconds = (int)GameData.currentMatch.matchDuration % 60;
        int milliseconds = (int)(GameData.currentMatch.matchDuration * 1000) % 1000;
        gametimeText.text = string.Format("{0:00}:{1:00}:{2:000}", minutes, seconds, milliseconds);

        // Force UI Focus
        // Without this, controllers won't do anything until you click the mouse once
        if (firstSelectedButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(firstSelectedButton);
        }

        // resultSound = RuntimeManager.PathToEventReference(resultPath);
        // RuntimeManager.PlayOneShot(resultSound);

    }

    void Update()
    {
        // If a player moves a stick but nothing is selected (due to a mouse click)
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null)
        {
            // If any joined player tries to navigate, snap selection back to the button
            if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
            {
                EventSystem.current.SetSelectedGameObject(firstSelectedButton);
            }
        }
    }

    public void Continue()
    {
        // Reload the scene
        GameData.JoinedPlayers.Clear();
        UnityEngine.SceneManagement.SceneManager.LoadScene("JoinScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }
}