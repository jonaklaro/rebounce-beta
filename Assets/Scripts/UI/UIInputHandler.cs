using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIInputHandler : MonoBehaviour
{
    [SerializeField] private Button startButton;

    void Start()
    {
        // Make sure no button is selected initially
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // Call this method when any player performs input
    public void OnPlayerInput()
    {
        if (EventSystem.current == null || startButton == null) return;

        // Check if nothing is currently selected (e.g., after a mouse click)
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);
            Debug.Log("Selection restored by player input");
        }
    }
    // Optional: Reset selection when needed (e.g., when returning to this scene)
    public void ResetSelection()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}