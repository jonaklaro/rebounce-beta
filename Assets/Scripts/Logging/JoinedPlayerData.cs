using UnityEngine.InputSystem;

[System.Serializable]
public class JoinedPlayerData
{
    public int playerIndex;
    public string controlScheme;
    public int deviceId;
    public InputDevice[] devices;
}
