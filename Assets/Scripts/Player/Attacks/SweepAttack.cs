using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using FMODUnity;

public class SweepAttack : MonoBehaviour
{
    [SerializeField] GameObject sweepWeapon;
    [SerializeField] ComboHandler comboHandler;
    private Animator animator;

    [Header("Sweep Attack Settings General")]
    [SerializeField] float sweepDuration = 0.3f;
    [SerializeField] float sweepChargeTime = 0.5f;
    [SerializeField] float sweetspotMultiplier = 1.1f;
    [SerializeField] float sweetspotPercentAdd = 7f;
    [SerializeField] float knockbackForce = 7f;
    
    [SerializeField] private GameObject rightVisualEffect; // Optional visual indicator
    [SerializeField] private GameObject leftVisualEffect;

    private bool isSweetSpot = false;
    private bool isRightSweep = false; // Track which sweep is being performed

    private CapsuleController controller;
    private bool hasHit = false; // Prevent multiple hits per sweep

    float bonusKnockback = 0f;
    private EventReference hitEvent;
    private string eventPath = "event:/Hit";
    private EventReference swooshEvent;
    private string swooshEventPath = "event:/Swoosh";
    private EventReference sweetspotEvent;
    private string sweetspotEventPath = "event:/Sweetspot";
    private FMOD.Studio.EventInstance sweepStretchEvent;
    private string sweepStretchEventPath = "event:/SweepStretch";
    


    public float AttackDuration()
    {
        return sweepDuration + sweepChargeTime + 0.2f; // Extra buffer time
    }


    void Start()
    {
        controller = GetComponent<CapsuleController>();

        // hitEvent = RuntimeManager.PathToEventReference(eventPath);
        // swooshEvent = RuntimeManager.PathToEventReference(swooshEventPath);
        // sweetspotEvent = RuntimeManager.PathToEventReference(sweetspotEventPath);
        // sweepStretchEvent = FMODUnity.RuntimeManager.CreateInstance(sweepStretchEventPath);

    }

    public void PerformSpecialAttackRight(Animator animator)
    {
        this.animator = animator;
        hasHit = false;
        isRightSweep = true;
        
        // ✅ LOG: Attack usage
        GameplayLogger.Instance.LogAttackUsed((int)controller.playerNumber, AttackType.RSweep);
        
        StartCoroutine(ChargeRightSweep());
        // Show visual effect if available
        if (rightVisualEffect != null)
        {
            rightVisualEffect.GetComponent<VisualEffect>().Play();
        }
    }

    IEnumerator ChargeRightSweep()
    {
        sweepStretchEvent.start();
        animator.SetBool("isWalking", false);
        float newSpeed = 1f / sweepChargeTime;
        animator.SetFloat("sweepChargeSpeed", newSpeed);
        animator.SetBool("isRightSweepWind", true);
        // Debug.Log("Charging up sweep attack...");
        yield return new WaitForSeconds(sweepChargeTime);
        // Debug.Log("Sweep attack charged!");
        PerformRightSweep();
    }

    void PerformRightSweep()
    {
        ActivateCollider();
        float newSpeed = 1f / sweepDuration;
        animator.SetFloat("sweepSpeed", newSpeed);
        animator.SetBool("isRightSweep", true);
        sweepStretchEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        // RuntimeManager.PlayOneShot(swooshEvent);
        StartCoroutine(EndSweepAfterDuration());
    }

    public void PerformSpecialAttackLeft(Animator animator)
    {
        this.animator = animator;
        hasHit = false;
        isRightSweep = false;
        
        // ✅ LOG: Attack usage
        GameplayLogger.Instance.LogAttackUsed((int)controller.playerNumber, AttackType.LSweep);
        
        StartCoroutine(ChargeLeftSweep());
        
        // Show visual effect if available
        if (leftVisualEffect != null)
        {
            leftVisualEffect.GetComponent<VisualEffect>().Play();
        }
    }

    IEnumerator ChargeLeftSweep()
    {
        sweepStretchEvent.start();
        animator.SetBool("isWalking", false);
        float newSpeed = 1f / sweepChargeTime;
        animator.SetFloat("sweepChargeSpeed", newSpeed);
        animator.SetBool("isLeftSweepWind", true);
        // Debug.Log("Charging up sweep attack...");
        yield return new WaitForSeconds(sweepChargeTime);
        // Debug.Log("Sweep attack charged!");
        PerformLeftSweep();
    }

    void PerformLeftSweep()
    {
        ActivateCollider();
        float newSpeed = 1f / sweepDuration;
        animator.SetFloat("sweepSpeed", newSpeed);
        animator.SetBool("isLeftSweep", true);
        sweepStretchEvent.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        // RuntimeManager.PlayOneShot(swooshEvent);
        StartCoroutine(EndSweepAfterDuration());
    }

    IEnumerator EndSweepAfterDuration()
    {
        yield return new WaitForSeconds(sweepDuration);
        animator.SetBool("isLeftSweepWind", false);
        animator.SetBool("isLeftSweep", false);
        animator.SetBool("isRightSweepWind", false);
        animator.SetBool("isRightSweep", false);
        DeactivateCollider();
        controller.StopAttack();
    }

    private void ActivateCollider()
    {
        sweepWeapon.GetComponent<BoxCollider>().enabled = true;
    }

    private void DeactivateCollider()
    {
        sweepWeapon.GetComponent<BoxCollider>().enabled = false;
        DeactivateSweetSpot();
    }

    public void HitObject(GameObject obj)
    {
        if (obj == null || hasHit)
        {
            return;
        }

        Debug.Log("Sweep hit " + obj.name);

        // Don't hit yourself or your own collider
        if (obj == gameObject || obj == sweepWeapon)
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
            
            hasHit = true;
            return;
        }

        // Get target's percent meter
        PercentMeter targetPercent = obj.GetComponent<PercentMeter>();
        float targetPercentValue = (targetPercent != null) ? targetPercent.P : 0f;

        float hitDistance = Vector3.Distance(transform.position, obj.transform.position);
        Vector3 dir;
        float percentDamage = 0f;
        if (isRightSweep)
        {
            dir = Quaternion.Euler(0, -90, 0) * transform.forward;
            if(comboHandler.IsRightSweep)
            {
                comboHandler.ResetCombo();
            }
            comboHandler.IsRightSweep = true;
        }
        else
        {
            dir = Quaternion.Euler(0, 90, 0) * transform.forward;
            if(comboHandler.IsLeftSweep)
            {
                comboHandler.ResetCombo();
            }
            comboHandler.IsLeftSweep = true;
        }

        comboHandler.IncreaseCombo(targetController);
        comboHandler.GetComboBuffs(out int percentBuff, out float knockbackBuff);
        percentDamage += percentBuff;
        bonusKnockback = knockbackBuff;

        dir.y = 0;

        // Calculate knockback: V = 7 + (2 * P)
        float calculatedKnockback = knockbackForce + (4f * targetPercentValue) + bonusKnockback;
        bonusKnockback = 0f; // Reset bonus knockback after use
        
        float finalKnockback = calculatedKnockback;

        if (isSweetSpot)
        {
            Debug.Log("Sweetspot hit! Applying multiplier of " + sweetspotMultiplier);
            // RuntimeManager.PlayOneShot(sweetspotEvent);
            finalKnockback = calculatedKnockback * sweetspotMultiplier;
            percentDamage += sweetspotPercentAdd;

            // Add sweetspot percent damage
            if (targetPercent != null)
            {
                targetPercent.AddPercentFromSweetSpot(percentDamage);
            }
            
            // RuntimeManager.PlayOneShot(hitEvent);
            targetController.ApplyKnockback(dir * finalKnockback);
        
        }
        else
        {
            percentDamage += sweetspotPercentAdd / 2;

            // Add sweetspot percent damage
            if (targetPercent != null)
            {
                targetPercent.AddPercentFromSweetSpot(percentDamage);
            }

            // RuntimeManager.PlayOneShot(hitEvent);
            targetController.ApplyKnockback(dir * finalKnockback);
        }

        // ✅ LOG: Attack hit
        Vector2 hitPosition = new Vector2(obj.transform.position.x, obj.transform.position.z);
        AttackType sweepType = isRightSweep ? AttackType.RSweep : AttackType.LSweep;
        
        targetController.SetLastHitByPlayer((int)controller.playerInputNumber);

        GameplayLogger.Instance.LogAttackHit(
            attackerIndex: (int)controller.playerInputNumber,
            victimIndex: (int)targetController.playerInputNumber,
            attackType: sweepType,
            wasSweetSpot: isSweetSpot,
            knockbackGenerated: finalKnockback,
            percentDamage: percentDamage,
            hitPosition: hitPosition
        );
        
        hasHit = true; 
    }   

    public void ActivateSweetSpot()
    {
        isSweetSpot = true;
    }

    public void DeactivateSweetSpot()
    {
        isSweetSpot = false;
    }
}