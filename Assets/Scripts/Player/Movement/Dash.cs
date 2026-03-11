using System;
using UnityEngine;
using FMODUnity;

public class Dash : MonoBehaviour
{
    [Header("Dash Settings")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.15f;
    public float dashCooldown = 1.5f;
    public int maxDashCharges = 3;
    
    public int CurrentDashCharges { get;private set; }
    public bool IsDashing { get;private set; }
    public Vector3 DashDirection { get;private set; }

    private float dashEndTime = 0f;
    private float dashRefillTimer = 0f;

    private Animator animator;

    private EventReference dashEvent;
    private string dashEventPath = "event:/Dash";

    // Add a helper to check if any charges remain
    public bool HasCharges() => CurrentDashCharges > 0;

    private CapsuleController player;
    
    private DashChargeParticleIndicator chargeIndicator;

    private void Awake()
    {
        CurrentDashCharges = maxDashCharges;
        chargeIndicator = GetComponent<DashChargeParticleIndicator>();
    }

    private void Start()
    {
        // dashEvent = FMODUnity.RuntimeManager.PathToEventReference(dashEventPath);
        player = GetComponent<CapsuleController>();
    }

    public void RefillAllCharges()
    {
        CurrentDashCharges = maxDashCharges;
        dashRefillTimer = 0f;
        //player.percentUI.UpdateCharges(CurrentDashCharges, maxDashCharges);
        if (chargeIndicator != null)
        {
            chargeIndicator.ResetIndicator();
            chargeIndicator.UpdateChargeDisplay(CurrentDashCharges);
        }
            
    }

    public void RefillCharges(int amount){
        if (CurrentDashCharges + amount > maxDashCharges)
            CurrentDashCharges = maxDashCharges;
        else
            CurrentDashCharges += amount;
        dashRefillTimer = 0f;
        //player.percentUI.UpdateCharges(CurrentDashCharges, maxDashCharges);
        if (chargeIndicator != null)
            chargeIndicator.UpdateChargeDisplay(CurrentDashCharges);
    }

    // Add a helper to consume a charge
    public void UseCharge()
    {
        if (CurrentDashCharges > 0)
        {
            CurrentDashCharges--;
            dashRefillTimer = 0f;
            //player.percentUI.UpdateCharges(CurrentDashCharges, maxDashCharges);
            if (chargeIndicator != null)
                chargeIndicator.UpdateChargeDisplay(CurrentDashCharges);
        }
        else
        {
            dashRefillTimer = 0f;
        }
    }
    
    // Versucht Dash zu starten. Input-Richtung wird bevorzugt, sonst Fallback auf Forward
    public void TryDash(Vector3 inputDir, Vector3 forwardDir, Animator animator)
    {
        if (player.isParryActive)
            return;

        this.animator = animator;
        if(CurrentDashCharges <= 0){
            StartCoroutine(player.percentUI.FlashCharges());
            return;
        }
        
        Vector3 dir = inputDir.sqrMagnitude > 0.01f ? inputDir.normalized : forwardDir.normalized;
        float newSpeed = 2;
        animator.SetFloat("dashSpeed", newSpeed);
        animator.SetBool("isDashing", true);
        DashDirection = dir;
        IsDashing = true;
        dashEndTime = Time.time + dashDuration;

        // RuntimeManager.PlayOneShot(dashEvent);
        
        CurrentDashCharges--;
        //player.percentUI.UpdateCharges(CurrentDashCharges, maxDashCharges);
        if (chargeIndicator != null)
            chargeIndicator.UpdateChargeDisplay(CurrentDashCharges);
        dashRefillTimer = 0f;
    }
    
    //Muss einmal pro Frame aufgerufen werden , damit Cooldown Charges wieder füllt
    public void HandleCooldown(float deltaTime)
    {
        if(CurrentDashCharges >= maxDashCharges)
            return;
        
        dashRefillTimer += deltaTime;

        while (dashRefillTimer >= dashCooldown && CurrentDashCharges < maxDashCharges)
        {
            dashRefillTimer -= dashCooldown;
            CurrentDashCharges++;
            //player.percentUI.UpdateCharges(CurrentDashCharges, maxDashCharges);
            if (chargeIndicator != null)
                chargeIndicator.UpdateChargeDisplay(CurrentDashCharges);
        }
    }
    
    // Muss in FixedUpdate damit der Dash automatisch endet
    public void UpdateDashState()
    {
        if(IsDashing && Time.time > dashEndTime)
        {
            IsDashing = false;
            animator.SetBool("isDashing", false);
        }
            
    }
}
