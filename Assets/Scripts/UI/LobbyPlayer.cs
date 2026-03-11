using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class LobbyPlayer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private Image background;
    
    public int playerIndex;
    public InputDevice device;
    private PlayerInput playerInput;
    private UIInputHandler uiInputHandler;
    
    private static Color[] playerColors = PlayerColors.playerColors;
    
    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        playerIndex = playerInput.playerIndex;
        device = playerInput.devices[0];
        Debug.Log($"Player {playerIndex} joined with {device.displayName}");
        
        // Find the UI input handler in the scene
        uiInputHandler = FindFirstObjectByType<UIInputHandler>();
    }
    
    public void Setup(int index, InputDevice device)
    {
        string deviceName =
            device is Keyboard ? "Keyboard" :
            device is Gamepad ? "Gamepad" :
            device.displayName;
        
        label.text = $"Player {index + 1} \n\n {device.displayName}";
        background.color = playerColors[index % playerColors.Length];
    }
    
    // public void OnLeave(InputAction.CallbackContext context)
    // {
    //     if (!context.performed)
    //         return;
        
    //     JoinManager.Instance.OnPlayerLeft(playerInput);
    // }

    public void OnCancel(InputValue value)
    {
        // Kick the player when they press cancel/back
        Debug.Log($"Player {playerIndex} leaving via cancel button");
        JoinManager.Instance.OnPlayerLeft(playerInput);
    }
    
    // Input System callbacks - these need InputValue parameter, not InputAction.CallbackContext
    public void OnMove(InputValue value)
    {
        if (uiInputHandler != null)
        {
            uiInputHandler.OnPlayerInput();
        }
    }
    
    public void OnNavigate(InputValue value)
    {
        if (uiInputHandler != null)
        {
            uiInputHandler.OnPlayerInput();
        }
    }
    
    public void OnSubmit(InputValue value)
    {
        if (uiInputHandler != null)
        {
            uiInputHandler.OnPlayerInput();
        }
    }
}