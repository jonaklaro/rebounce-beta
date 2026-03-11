using UnityEngine;

public class CollisionDetector : MonoBehaviour
{
    [SerializeField] PunchAttack punchAttack;
    [SerializeField] SweepAttack sweepAttack;
    void OnTriggerEnter(Collider other)
    {
        if (punchAttack != null)
        {
            punchAttack.HitObject(other.gameObject);
        }
            

        if (sweepAttack != null)
        {
            sweepAttack.HitObject(other.gameObject);
        }
    }  
} 