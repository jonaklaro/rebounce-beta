using UnityEngine;

public class SweetspotHandler : MonoBehaviour
{
    public void ActivateSweetSpot(int sweetSpot)
    {
        SweepAttack sweepAttack = GetComponentInParent<SweepAttack>();
        if (sweepAttack != null)
        {
            sweepAttack.ActivateSweetSpot();
        }
    }

    public void DeactivateSweetSpot(int sweetSpot)
    {
        SweepAttack sweepAttack = GetComponentInParent<SweepAttack>();
        if (sweepAttack != null)
        {
            sweepAttack.DeactivateSweetSpot();
        }
    }
}
