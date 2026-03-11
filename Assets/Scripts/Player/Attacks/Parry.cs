using UnityEngine;
using UnityEngine.VFX;
using System.Collections;
using FMODUnity;
using FMOD.Studio;

public class Parry : MonoBehaviour
{
    [Header("Parry Settings")]
    [SerializeField] private float parryWindowDuration = 0.5f; // How long the parry is active
    [SerializeField] private float parryKnockbackForce = 15f; // Force applied to attacker
    [SerializeField] private GameObject parryVisualEffect; // Optional visual indicator

    [SerializeField] private float percentDamage = 0f;
    
    private bool isParryActive = false;
    private bool isPlayerLocked = false;

    [SerializeField] private float parryPushTime = 1.5f;

    private float parryEndTime = 0f;
    private CapsuleController controller;

    private ComboHandler comboHandler;
    private PercentMeter percentMeter;

    private Animator animator;

    private EventReference shieldEvent;
    private EventInstance shieldEventInstance;
    private EventReference shieldSuccess;
    private EventReference shieldFail;
    private EventInstance shieldFailInstance;

    private string shieldEventPath = "event:/Shield";
    private string shieldSuccessPath = "event:/ShieldSuccess";
    private string shieldFailPath = "event:/ShieldFail";

    void Start()
    {
        controller = GetComponent<CapsuleController>();
        comboHandler = controller.comboHandler;
        percentMeter = GetComponent<PercentMeter>();
        
        // shieldEvent = RuntimeManager.PathToEventReference(shieldEventPath);
        // shieldEventInstance = RuntimeManager.CreateInstance(shieldEvent);
        // shieldSuccess = RuntimeManager.PathToEventReference(shieldSuccessPath);
        // shieldFail = RuntimeManager.PathToEventReference(shieldFailPath);
        // shieldFailInstance = RuntimeManager.CreateInstance(shieldFail);
    }

    void Update()
    {
        // Check if parry window has expired
        if (isParryActive && Time.time >= parryEndTime)
        {
            DeactivateParry();
        }
    }

    public void ParryPerform(Animator animator)
    {
        // Check if the shared dash counter has charges
        if (controller.dashController == null || !controller.dashController.HasCharges())
            return;

        this.animator = animator;

        // shieldEventInstance.start();
        
        // Consume one charge from the shared counter
        controller.dashController.UseCharge();

        isParryActive = true;
        isPlayerLocked = true;
        parryEndTime = Time.time + parryWindowDuration;

        float newParrySpeed = 1 / parryWindowDuration;
        animator.SetFloat("parrySpeed", newParrySpeed);
        animator.SetBool("isParrying", true);
        
        // Show visual effect if available
        if (parryVisualEffect != null)
        {
            parryVisualEffect.GetComponent<VisualEffect>().Play();
        }
    }

    private void DeactivateParry()
    {
        if (isParryActive)
        {
            StartCoroutine(PunishPlayer());
            GameplayLogger.Instance.LogParry(controller.playerInputNumber, false);
        }

        isParryActive = false;
        animator.SetBool("isParrying", false);
        // Hide visual effect
    }

    private IEnumerator PunishPlayer()
    {
        // set isPlayerLocked to true
        // shieldEventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        // shieldFailInstance.start();
        isPlayerLocked = true;
        controller.ResetPlayerMovement();
        yield return new WaitForSeconds(parryPushTime);
        // set isPlayerLocked to false
        isPlayerLocked = false;
        // shieldFailInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        
    }


    /// <summary>
    /// Call this from attack scripts to check if the target is currently parrying
    /// Returns true if parry was successful (attacker should be knocked back)
    /// </summary>
    public bool TryParry(CapsuleController attacker)
    {
        if (!isParryActive) return false;

        if (controller.dashController != null)
        {
            controller.dashController.RefillCharges(3);
        }

        // Get the parrying player's percent to calculate "reflected" force
        PercentMeter myPercent = GetComponent<PercentMeter>();
        float p = (myPercent != null) ? myPercent.P : 0f;

        // Use a unified formula (Example: Sweep's formula or a new standard)
        // Here we use a base parry force + a scaler based on the parrier's damage
        float reflectedForce = parryKnockbackForce + (2f * p); 

        if (comboHandler.IsParry){
            comboHandler.ResetCombo();
        }
        comboHandler.IsParry = true;

        comboHandler.IncreaseCombo(attacker);
        comboHandler.GetComboBuffs(out int percentBuff, out float knockbackBuff);

        reflectedForce += knockbackBuff;

        float percentToAdd = percentDamage + percentBuff;

        attacker.percentMeter.AddPercentFromSweetSpot(percentToAdd);

        Vector3 knockbackDirection = (attacker.transform.position - transform.position).normalized;
        attacker.ApplyKnockback(knockbackDirection * reflectedForce);

        // Logging and State reset
        GameplayLogger.Instance.LogParry((int)controller.playerNumber, true);
        isParryActive = false;
        isPlayerLocked = false;
        animator.SetBool("isParrying", false);

        attacker.SetLastHitByPlayer((int)controller.playerNumber);

        // RuntimeManager.PlayOneShot(shieldSuccess);

        return true;
    }

    // Public getter for other scripts to check parry state
    public bool IsParryActive()
    {
        return isPlayerLocked;
    }

    // Optional: Force deactivate parry (useful for game state changes)
    public void CancelParry()
    {
        if (isParryActive)
        {
            DeactivateParry();
        }
    }
}