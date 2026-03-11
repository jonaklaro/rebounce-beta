using UnityEngine;
using FMODUnity;

public class KillZone : MonoBehaviour
{
    private EventReference knockoutEvent;
    private string knockoutEventPath = "event:/Knockout";    

    private void Start()
    {
        // knockoutEvent = RuntimeManager.PathToEventReference(knockoutEventPath);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Is it a player?
        CapsuleController controller = other.GetComponent<CapsuleController>();
        if (controller != null)
        {
            // RuntimeManager.PlayOneShot(knockoutEvent);
            GameplayLogger.Instance.LogKnockout(controller.playerInputNumber, controller.GetPercent(),controller.transform.position, controller.GetLastHitByPlayer());
            controller.Die();
        }
    }
}