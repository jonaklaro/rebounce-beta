using System.Collections;
using UnityEngine;
using UnityEngine.VFX;
using FMODUnity;

public class PunchAttack : MonoBehaviour
{
    [SerializeField] float m_MaxDistance;
    [SerializeField] float m_MaxQuickDistance;
    [SerializeField] GameObject punchCollider;
    [SerializeField] GameObject quickPunchCollider;
    [SerializeField] ComboHandler comboHandler;
    [SerializeField] float m_ChargeTime = 1.5f;
    [SerializeField] float m_QuickChargeTime;
    [SerializeField] float punchTime = 0.3f;
    [SerializeField] float quickPunchTime;
    [SerializeField] float chargeSweetspotRange;
    [SerializeField] float quickSweetSpotRange;
    private float sweetSpotRange;
    [SerializeField] float punchweetspotMultiplier = 1.5f;
    [SerializeField] float quickPunchSweetspotMultiplier = 1.3f;
    private float sweetspotMultiplier;
    [SerializeField] float knockbackForce = 5f;
    [SerializeField] float sweetspotPercentAdd = 20f;
    [SerializeField] float quickPunchSweetspotPercentAdd = 10f;
    private float percentAdd;
    [SerializeField] private Color normalAttackColor = Color.yellow;
    [SerializeField] private Color sweetspotColor = Color.red;

    private CapsuleController controller;
    private bool hasHit = false; // Prevent multiple hits per punch
    float bonusKnockback = 0f;

    private bool isQuickPunch = false;
    private bool isChargedPunch = false;

	[SerializeField] private GameObject straightPunchVisualEffect;
    [SerializeField] private GameObject quickStraightPunchVisualEffect;

    
    private EventReference hitEvent;
    private string eventPath = "event:/Hit";
    private FMOD.Studio.EventInstance windupEvent;
    private string windupEventPath = "event:/Windup";
    private EventReference stopWindupEvent;
    private string stopWindupEventPath = "event:/StopWindup";
    private EventReference sweetspotEvent;
    private string sweetspotEventPath = "event:/Sweetspot";


    void Start()
    {
        controller = GetComponent<CapsuleController>();

        // hitEvent = FMODUnity.RuntimeManager.PathToEventReference(eventPath);
        // windupEvent = FMODUnity.RuntimeManager.CreateInstance(windupEventPath);
        // stopWindupEvent = FMODUnity.RuntimeManager.PathToEventReference(stopWindupEventPath);
        // sweetspotEvent = FMODUnity.RuntimeManager.PathToEventReference(sweetspotEventPath);
    }

    public float AttackDuration()
    {
        return punchTime + m_ChargeTime + 0.2f; // Extra buffer time
    }

    public float QuickAttackDuration()
    {
        return quickPunchTime + m_QuickChargeTime + 0.2f; // Extra buffer time
    }


    public void HitObject(GameObject obj)
    {
        if (obj == null || hasHit)
        {
            return;
        }
        
        
        // Don't hit yourself or your own collider
        if (obj == gameObject || obj == punchCollider)
        {
            return;
        }
        
        CapsuleController targetController = obj.GetComponent<CapsuleController>();
        if (targetController == null)
        {
            return;
        }
        
        // Check if target is parrying
        Parry targetParry = obj.GetComponent<Parry>();
        if (targetParry != null && targetParry.TryParry(controller))
        {
            // Parry successful - attacker gets knocked back
            Debug.Log($"{controller.playerNumber} was parried by {targetController.playerNumber}!");
            
            hasHit = true;
            return;
        }
        
        // Get target's percent meter
        PercentMeter targetPercent = obj.GetComponent<PercentMeter>();
        float targetPercentValue = (targetPercent != null) ? targetPercent.P : 0f;
        
        float hitDistance = Vector3.Distance(transform.position, obj.transform.position);
        
        Vector3 dir = (obj.transform.position - transform.position).normalized;
        dir.y = 0;

        // Debug.Log("NormalPunch" + isChargedPunch + " QuickPunch " + isQuickPunch);
        if (isChargedPunch)
        {
            sweetspotMultiplier = punchweetspotMultiplier;
            percentAdd = sweetspotPercentAdd;
            sweetSpotRange = m_MaxDistance - chargeSweetspotRange;
            if (comboHandler.IsChargedPunch)
            {
                comboHandler.ResetCombo();
            }
            comboHandler.IsChargedPunch = true;
        }
        else if (isQuickPunch)
        {
            sweetspotMultiplier = quickPunchSweetspotMultiplier;
            percentAdd = quickPunchSweetspotPercentAdd;
            sweetSpotRange = m_MaxQuickDistance - quickSweetSpotRange;
            if (comboHandler.IsQuickPunch)
            {
                comboHandler.ResetCombo();
            }
            comboHandler.IsQuickPunch = true;
        }
        comboHandler.IncreaseCombo(targetController);
        comboHandler.GetComboBuffs(out int percentBuff, out float knockbackBuff);
        percentAdd += percentBuff;
        bonusKnockback = knockbackBuff;

        // Calculate knockback based on percent - Formula: V = 5 + (4 * P)
        float calculatedKnockback = knockbackForce + (4f * targetPercentValue) + bonusKnockback;
        bonusKnockback = 0f; // Reset bonus knockback after use
        
        bool wasSweetspot = false;
        float finalKnockback = calculatedKnockback;
        float percentDamage = 0f;
        
        if (hitDistance > sweetSpotRange)
        {
            // SWEETSPOT HIT
            Debug.Log("Sweetspot hit! Applying multiplier of " + sweetspotMultiplier);
            // RuntimeManager.PlayOneShot(sweetspotEvent);
            wasSweetspot = true;
            finalKnockback = calculatedKnockback * sweetspotMultiplier;
            percentDamage = percentAdd;
            // Add sweetspot percent damage
            if (targetPercent != null)
            {
                targetPercent.AddPercentFromSweetSpot(percentDamage);
            }

            targetController.ApplyKnockback(dir * finalKnockback);
        }
        else if (hitDistance <= sweetSpotRange && hitDistance > 0)
        {
            // NORMAL HIT
            wasSweetspot = false;
            percentDamage = percentAdd / 2;
            // Add sweetspot percent damage
            if (targetPercent != null)
            {
                targetPercent.AddPercentFromSweetSpot(percentDamage);
            }
            
            targetController.ApplyKnockback(dir * finalKnockback);
        }

        AttackType attackType = AttackType.PunchCharged;
        if (isQuickPunch)
        {
            attackType = AttackType.PunchQuick;
        }

        targetController.SetLastHitByPlayer((int)controller.playerInputNumber);
        
        // LOG: Attack hit
        GameplayLogger.Instance.LogAttackHit(
            attackerIndex: (int)controller.playerInputNumber,
            victimIndex: (int)targetController.playerInputNumber,
            attackType: attackType,
            wasSweetSpot: wasSweetspot,
            knockbackGenerated: finalKnockback,
            percentDamage: percentDamage,
            hitPosition: obj.transform.position
        );

        // FMODUnity.RuntimeManager.PlayOneShot(hitEvent);
    
        hasHit = true;
    }

    public void PunchPerform(Animator animator)
    {
        hasHit = false; // Reset for new punch
        isChargedPunch = true;
        
        // ✅ LOG: Attack usage (punch initiated)
        GameplayLogger.Instance.LogAttackUsed((int)controller.playerNumber, AttackType.PunchCharged);
        
        // Show visual effect if available
        if (straightPunchVisualEffect != null)
        {
            straightPunchVisualEffect.GetComponent<VisualEffect>().Play();
        }
        
        StartCoroutine(ChargeUp(animator, punchCollider, m_MaxDistance));
    }

    public void QuickPunchPerform(Animator animator)
    {
        hasHit = false; // Reset for new punch
        isQuickPunch = true;
        
        // ✅ LOG: Attack usage (punch initiated)
        GameplayLogger.Instance.LogAttackUsed((int)controller.playerNumber, AttackType.PunchQuick);
        
        // Show visual effect if available
        if (quickStraightPunchVisualEffect != null)
        {
            quickStraightPunchVisualEffect.GetComponent<VisualEffect>().Play();
        }
        
        StartCoroutine(QuickChargeUp(animator, quickPunchCollider, m_MaxQuickDistance));
    }

    IEnumerator ChargeUp(Animator animator,GameObject collider, float maxDistance)
    {
        animator.SetBool("isPunchWindUp", true);
        // windupEvent.start();
        // windupEvent.setParameterByName("Progress", 0f);

        float elapsedTime = 0f;

        while (elapsedTime < m_ChargeTime)
        {
            // windupEvent.setParameterByName("Progress", elapsedTime / m_ChargeTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Debug.Log("Charging up punch attack...");
        // yield return new WaitForSeconds(m_ChargeTime);
        // Debug.Log("Punch attack charged!");
        float newSpeed = 1 / punchTime;
        animator.SetFloat("punchSpeed", newSpeed);
        animator.SetBool("isPunching", true);
        StartCoroutine(MoveCollider(animator, collider, maxDistance));
    }

    IEnumerator QuickChargeUp(Animator animator, GameObject collider, float maxDistance)
    {
        float newSpeed = 1 / m_QuickChargeTime;
        animator.SetFloat("quickChargeSpeed", newSpeed);
        animator.SetBool("isQuickPunch", true);
        // windupEvent.start();
        // windupEvent.setParameterByName("Progress", 0f);

        float elapsedTime = 0f;

        while (elapsedTime < m_QuickChargeTime)
        {
            // windupEvent.setParameterByName("Progress", elapsedTime / m_QuickChargeTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        // Debug.Log("Charging up punch attack...");
        // yield return new WaitForSeconds(m_QuickChargeTime);
        // Debug.Log("Punch attack charged!");
        StartCoroutine(MoveCollider(animator, collider, maxDistance));
    }


    IEnumerator MoveCollider(Animator animator,GameObject collider, float maxDistance)
    {
        // windupEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        collider.SetActive(true);
        
        float elapsedTime = 0f;
        float newSpeed = 1 / quickPunchTime;
        animator.SetFloat("punchSpeed", newSpeed);
        animator.SetBool("isPunching", true);
        
        while (elapsedTime < punchTime)
        {
            // Recalculate target position each frame based on current player position and forward direction
            Vector3 targetPosition = transform.position + transform.forward * maxDistance;
            collider.transform.position = Vector3.Lerp(collider.transform.position, targetPosition, elapsedTime / punchTime);
            elapsedTime += Time.deltaTime;
            yield return null;
            if (collider.transform.position == targetPosition)
            {
                collider.SetActive(false);
            }
        }
        
        collider.SetActive(false);
        collider.transform.localPosition = Vector3.zero;
        animator.SetBool("isPunchWindUp", false);
        animator.SetBool("isQuickPunch", false);
        animator.SetBool("isPunching", false);
        controller.StopAttack();
        isQuickPunch = false;
        isChargedPunch = false;

        

    }

}