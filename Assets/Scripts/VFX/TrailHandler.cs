using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class TrailHandler : MonoBehaviour
{
    public float minimumSpeed = 12f;
    
    private TrailRenderer trail;
    [SerializeField] public Dash dashController;
    
    //Spieler Infos
    private CapsuleController controller;   // Infos vom Spieler
    private Rigidbody rb;

    void Awake()
    {
        trail = GetComponent<TrailRenderer>();

        dashController = GetComponentInParent<Dash>();
        // Spieler-Script im Parent suchen
        controller = GetComponentInParent<CapsuleController>();
        if (controller != null)
        {
            rb = controller.GetComponent<Rigidbody>();
        }
        else
        {
            Debug.LogWarning("TrailHandler: CapsuleController nicht gefunden.");
        }
    }

    void Update()
    {
        if (rb == null)
            return;

        float speed = rb.linearVelocity.magnitude; // oder rb.velocity je nach Version

        bool enableTrail = speed > minimumSpeed && !dashController.IsDashing;
        if (trail.emitting != enableTrail)
            trail.emitting = enableTrail;
    }
}