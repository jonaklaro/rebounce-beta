using UnityEngine;
using FMODUnity;

[RequireComponent(typeof(Collider))]
public class BounceReflector : MonoBehaviour
{
    private Shake shakeEffect;  //Shake Skript 
    public enum BounceSurfaceType
    {
        StandardWall,
        SpecialWall,
        BouncePad,
        BreakableWall,
        BreakableObject
    }

    [Header("Surface Type")]
    public BounceSurfaceType surfaceType;

    [Header("Bounce Settings (Autofilled from GDD)")]
    public float reflectionMultiplier;
    public float verticalBoost = 0f;
    public float minImpactVelocity = 0.5f;

    [Header("Breakable Settings")]
    public bool isBreakable = false;
    public int maxHits = 5;
    private int currentHits = 0;

    [Header("Destroyed Replacement")]
    public GameObject destroyedReplacement;
    public bool hideOnBreak = true;
    private float reflectionPercent = 0.1f;

    // create fmod event reference that has the default path master Reflect
    private EventReference reflectEvent;
    private string eventPath = "event:/Reflect";
    private EventReference breakEvent;
    private string breakPath = "event:/Break";

    private void OnValidate()
    {
        switch (surfaceType)
        {
            case BounceSurfaceType.StandardWall:
                reflectionMultiplier = 0.4f;
                isBreakable = false;
                maxHits = 0;
                break;

            case BounceSurfaceType.SpecialWall:
                reflectionMultiplier = 0.8f;
                isBreakable = false;
                maxHits = 0;
                break;

            case BounceSurfaceType.BouncePad:
                reflectionMultiplier = 1.4f;
                isBreakable = false;
                maxHits = 0;
                reflectionPercent = 0.3f;
                eventPath = "event:/BouncePadReflect";
                break;

            case BounceSurfaceType.BreakableWall:
                reflectionMultiplier = 0.4f;
                isBreakable = true;
                maxHits = 3;
                break;

            case BounceSurfaceType.BreakableObject:
                reflectionMultiplier = 0.4f;
                isBreakable = true;

                if (maxHits < 1 || maxHits > 3)
                    maxHits = 1;

                break;
        }
    }

    private void Awake()
    {
        shakeEffect = Camera.main.GetComponent<Shake>();
    }

    private void Start()
    {
        // reflectEvent = FMODUnity.RuntimeManager.PathToEventReference(eventPath);
        // breakEvent = FMODUnity.RuntimeManager.PathToEventReference(breakPath);
    }

    private void OnCollisionEnter(Collision collision)
    {
        CapsuleController player = collision.gameObject.GetComponent<CapsuleController>();
        if (!player) return;

        int playerIndex = player.playerInputNumber;

        // Get incoming velocity and speed - Shake effect
        Vector3 incomingVel = player.LastVelocity;
        float speed = player.LastVelocity.magnitude;
        float intensity = Mathf.Clamp(speed / 100f, 0f, 100f); 

        // Local variable to store which type we are LOGGING
        BounceSurfaceType loggedType = surfaceType;
        float effectiveMultiplier = reflectionMultiplier;

        // --- NEW: BouncePad Downgrade Logic ---
        if (surfaceType == BounceSurfaceType.BouncePad)
        {
            // If not a valid attack, treat as a Standard Wall
            if (!player.isAttacking && !player.IsAttackVelocity) 
            {
                loggedType = BounceSurfaceType.StandardWall;
                effectiveMultiplier = 0.4f; // Standard wall multiplier
            }
        }

        if (speed < minImpactVelocity) return;
        bool canShake = player.IsAttackVelocity && !player.dashController.IsDashing;

        if (canShake && Shake.instance != null)
        {
            
            StartCoroutine(Shake.instance.ShakeCamera(intensity));
        }
            

        // 1. Logic for Breaking (unchanged)
        bool canDamage = (player.dashController != null && player.dashController.IsDashing) || player.IsAttackVelocity;
        bool isBreakableWall = isBreakable || (player.canDamageWalls && surfaceType == BounceSurfaceType.StandardWall);
        bool willBreak = isBreakableWall && canDamage && (currentHits + 1 >= maxHits);

        if (willBreak && player.canDamageWalls) 
        {
            // Log as the effective type (BouncePad or StandardWall)
            LogBounce(playerIndex, player.transform.position, speed, loggedType);
            BreakObject(); 
            return; 
        }

        // 2. Log the bounce using our effective type
        LogBounce(playerIndex, player.transform.position, speed, loggedType);
        
        // 3. Normal Reflection Logic
        Vector3 normal = collision.contacts[0].normal;
        // USE effectiveMultiplier instead of reflectionMultiplier
        Vector3 reflected = Vector3.Reflect(incomingVel, normal) * effectiveMultiplier;
        reflected.y += verticalBoost;

        if (!player.isAttacking)
            player.transform.rotation = Quaternion.LookRotation(reflected);

        if (isBreakableWall && canDamage)
        {
            currentHits++;
            if (currentHits >= maxHits)
                StartCoroutine(BreakObjectCoroutine(player, reflected)); 
        }

        if (!player.dashController.IsDashing && player.IsAttackVelocity){
            collision.gameObject.GetComponent<PercentMeter>().AddPercent(reflectionPercent);
            GameplayLogger.Instance?.LogEnvironmentDamage(playerIndex, reflectionPercent);
        }

        if (player.dashController.IsDashing || player.IsAttackVelocity){
            player.TriggerRumble();

            // FMODUnity.RuntimeManager.PlayOneShot(reflectEvent);
        }

        player.ApplyKnockback(reflected);

    }

    private System.Collections.IEnumerator BreakObjectCoroutine(CapsuleController player, Vector3 reflected)
    {
        // Apply knockback
        player.ApplyKnockback(reflected);
        
        // Wait one physics frame for everything to settle
        yield return new WaitForSeconds(0.1f);

        // Now actually break/hide the object
        BreakObject();
    }

    private void BreakObject()
    {
        // RuntimeManager.PlayOneShot(breakEvent);

        // Disable colliders IMMEDIATELY so player passes through
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

        GameplayLogger.Instance?.LogBreakableDestroyed(surfaceType);

        if (destroyedReplacement)
            Instantiate(destroyedReplacement, transform.position, transform.rotation);

        if (hideOnBreak)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LogBounce(int playerIndex, Vector3 position, float speed, BounceSurfaceType typeToLog)
    {
        if (GameplayLogger.Instance != null)
        {
            GameplayLogger.Instance.LogBounce(typeToLog, position, speed, playerIndex);
        }
    }
}