
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.VFX;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class CapsuleController : MonoBehaviour
{
    [SerializeField] public Dash dashController;
    [SerializeField] private GameObject playerAsset;
    private Animator animator;

    [Header("Player Visuals")]
    [SerializeField] private Renderer cylinderRenderer;
    private static readonly int ColorProp = Shader.PropertyToID("_BaseColor");
    
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float acceleration = 10f;
    public float friction = 3f;
    public float knockbackFriction = 1f; // Lower = more sliding when knocked back

    [Header("Player Settings")]
    public PlayerNumber playerNumber = PlayerNumber.Player1;
    public int playerInputNumber;
    [SerializeField] public int playerLives = 3;
    public int startingLives;
    public bool isDead { get; private set; }

    [Header("Attack Settings")]
    public float attackRange = 2f;
    public float attackCooldown = 1f;
    public float knockbackDuration = 0.3f;
    public float parryCooldown = 2f;

    [SerializeField] PunchAttack punchAttack;
    [SerializeField] SweepAttack sweepAttack;
    [SerializeField] Parry parry;
    [SerializeField] public PercentMeter percentMeter;
    public ComboHandler comboHandler;

    [Header("Y-Lock Settings")]
    public float lockedYPosition = 1f;
    public bool lockYOnStart = true;

    [Header("Knockback Lock Settings")]
    public float movementLockThreshold = 10f; // Adjust this value to set the knockback force threshold

    private Rigidbody rb;
    private Vector3 velocity = Vector3.zero;
    private Vector3 knockbackVelocity = Vector3.zero; // NEW: Separate knockback velocity
    private float lastAttackTime = -999f;
    private float lastParryTime = -999f;
    private float knockbackEndTime = 0f;
    private bool isMovementLocked = false;
    private bool isRotationLocked = false;

    private VisualEffect[] allVFX;

    public bool FirstBounce = true;
    public Vector3 LastVelocity { get; private set; }
    public bool IsAttackVelocity { get; private set; }

    // Input System
    private Vector2 moveInput;
    private bool attackPressed;
    private bool quickAttackPressed;
    private bool specialAttackRightPressed;
    private bool specialAttackLeftPressed;
    private bool parryPressed;
    private PlayerInput playerInput;

    [SerializeField] public bool isAttacking = false;
    public bool canDamageWalls => percentMeter.IsWallDestoying();
    public float GetPercent() => percentMeter.GetPercent();
    public PercentUI percentUI => percentMeter.percentUI;
    public bool isParryActive => parry.IsParryActive();

    public Color PlayerColor => GetColorForPlayer();

    private int lastHitByPlayer = -1;
    private Coroutine resetHitCoroutine;

    public Gamepad gamepad;

    public enum PlayerNumber
    {
        Player1,
        Player2
    }

    void Awake(){
        percentMeter = GetComponent<PercentMeter>();
        if (percentMeter == null)
            percentMeter = gameObject.AddComponent<PercentMeter>();
            
        if (cylinderRenderer == null)
        {
            // Namen im Prefab checken: z.B. "Cylinder" oder "PlayerRing"
            Transform cyl = transform.Find("PlayerIndicator");
            if (cyl != null)
                cylinderRenderer = cyl.GetComponent<Renderer>();
        }
        
        allVFX = gameObject.GetComponentsInChildren<VisualEffect>(true);
        
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = playerAsset.GetComponent<Animator>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        isDead = false;
        startingLives = playerLives;
        comboHandler = GetComponent<ComboHandler>();

        // OPTIONAL: PlayerNumber is set on PlayerInput used
        playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            playerInputNumber = playerInput.playerIndex;
        }

        if (lockYOnStart)
        {
            Vector3 pos = transform.position;
            pos.y = lockedYPosition;
            transform.position = pos;
        }

        ApplyPlayerColor();
    }

    void Update()
    {
        HandleInput();
        HandleAttack();
        
        HandleParry();

        if (dashController != null)
        {
            dashController.HandleCooldown(Time.deltaTime);
        }
    }

    void FixedUpdate()
    {
        // Capture before physics modifies it
        LastVelocity = rb.linearVelocity;

        if (dashController != null)
        {
            dashController.UpdateDashState();
        }

        if (dashController != null && dashController.IsDashing)
        {
            Vector3 dashDir = dashController.DashDirection;
            rb.linearVelocity = new Vector3(dashDir.x * dashController.dashSpeed, 0f, dashDir.z * dashController.dashSpeed);
        }
        else
        {
            ApplyMovement();
        }
        
        EnforceYLock();
        if (knockbackVelocity.magnitude < 2f)
        {
            IsAttackVelocity = false;
        }
        // Reset attack-velocity and movement lock if knockback duration ended
        if (Time.time > knockbackEndTime && !parry.IsParryActive())
        {
            isMovementLocked = false;
            if (lastHitByPlayer != -1 && resetHitCoroutine == null){
                resetHitCoroutine = StartCoroutine(ResetLastHitByPlayer());
            }
            // Don't zero knockbackVelocity here - let friction handle it naturally
        } 
    }

    private IEnumerator ResetLastHitByPlayer(){
        yield return new WaitForSeconds(1f);
        lastHitByPlayer = -1;
        resetHitCoroutine = null;
    }

    public void SetLastHitByPlayer(int player){
        lastHitByPlayer = player;

        if (resetHitCoroutine != null){
            StopCoroutine(resetHitCoroutine);
            resetHitCoroutine = null;
        }
    }

    public int GetLastHitByPlayer(){
        return lastHitByPlayer;
    }

    //----------------------------------------------------------------------
    //  COLOR SETTING
    //----------------------------------------------------------------------
    private void ApplyPlayerColor()
    {
        if (cylinderRenderer == null) return;

        Color c = GetColorForPlayer();

        // eigene Material-Instanz pro Spieler
        Material mat = cylinderRenderer.material;
        mat.SetColor(ColorProp, c);
        
    }

    private Color GetColorForPlayer()
    {
         return playerInputNumber switch
         {
            
             0 => PlayerColors.playerColors[0],
             1 => PlayerColors.playerColors[1],
             2 => PlayerColors.playerColors[2],
             3 => PlayerColors.playerColors[3],
            _ => Color.white,   
         };
    }
//
    //----------------------------------------------------------------------
    //  MOVEMENT
    //----------------------------------------------------------------------

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            attackCooldown = punchAttack.AttackDuration();
            attackPressed = true;
        }
    }
    
    public void OnQuickAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            attackCooldown = punchAttack.QuickAttackDuration();
            quickAttackPressed = true;
        }
    }

    public void OnSpecialAttackRight(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            attackCooldown = sweepAttack.AttackDuration();
            specialAttackRightPressed = true;
        }
    }

    public void OnSpecialAttackLeft(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            attackCooldown = sweepAttack.AttackDuration();
            specialAttackLeftPressed = true;
        }
    }

    public void OnParry(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            parryPressed = true;
            isMovementLocked = true;
        }
    }

    void HandleInput()
    {
        // Don't process movement input if locked
        if (isMovementLocked)
        {
            animator.SetBool("isWalking", false);
            return;
        }

        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        // Smooth velocity change from input
        if (inputDir.magnitude > 0.1f)
        {
            IsAttackVelocity = false;
            velocity = Vector3.Lerp(velocity, inputDir * moveSpeed, Time.deltaTime * acceleration);
            if ((!isRotationLocked && isAttacking) || !isAttacking)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(inputDir), 0.3f);
            }
            animator.SetBool("isWalking", true);
        }
        else
        {
            // Apply friction when no input
            velocity = Vector3.Lerp(velocity, Vector3.zero, Time.deltaTime * friction);
            animator.SetBool("isWalking", false);
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;
        
        if(dashController == null)
            return;
        Vector3 inputDir = new Vector3(moveInput.x, 0f, moveInput.y);
        dashController.TryDash(inputDir,transform.forward, animator);
    }

    void PerformParry()
    {
        parry.ParryPerform(animator);
    }

    private void SpecialAttackRight()
    {
        sweepAttack.PerformSpecialAttackRight(animator);
    }

    private void SpecialAttackLeft()
    {
        sweepAttack.PerformSpecialAttackLeft(animator);
    }

    void PerformPunch()
    {
        punchAttack.PunchPerform(animator);
    }

    void PerformQuickPunch()
    {
        punchAttack.QuickPunchPerform(animator);
    }

    void ApplyMovement()
    {
        // Combine movement velocity with knockback velocity
        Vector3 finalVelocity;
        
        if (isMovementLocked)
        {
            // Only apply knockback velocity when locked
            finalVelocity = knockbackVelocity;
        }
        else
        {
            // Combine both velocities normally
            finalVelocity = velocity + knockbackVelocity;
        }

        rb.linearVelocity = new Vector3(finalVelocity.x, 0, finalVelocity.z);

        // Always decay knockback velocity using knockbackFriction (no hard cutoff)
        if (knockbackVelocity.magnitude > 0.01f)
        {
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, Time.deltaTime * knockbackFriction);
        }
        else
        {
            knockbackVelocity = Vector3.zero;
        }
    }

    void EnforceYLock()
    {
        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.y - lockedYPosition) > 0.01f)
        {
            pos.y = lockedYPosition;
            transform.position = pos;
        }
    }

    //----------------------------------------------------------------------
    //  ATTACK / KNOCKBACK
    //----------------------------------------------------------------------

    void HandleAttack()
    {
        if(parry.IsParryActive())
            return;
         
        if (attackPressed && Time.time >= lastAttackTime + attackCooldown)
        {
            attackPressed = false;
            isAttacking = true;
            PerformPunch();
            lastAttackTime = Time.time;
        }
        else if (quickAttackPressed && Time.time >= lastAttackTime + attackCooldown)
        {
            quickAttackPressed = false;
            isAttacking = true;
            PerformQuickPunch();
            lastAttackTime = Time.time;
        }
        else if (specialAttackRightPressed && Time.time >= lastAttackTime + attackCooldown)
        {
            specialAttackRightPressed = false;
            isAttacking = true;
            SpecialAttackRight();
            lastAttackTime = Time.time;
        }
        else if (specialAttackLeftPressed && Time.time >= lastAttackTime + attackCooldown)
        {
            specialAttackLeftPressed = false;
            isAttacking = true;
            SpecialAttackLeft();
            lastAttackTime = Time.time;
        }
        else if (attackPressed || specialAttackRightPressed || specialAttackLeftPressed || quickAttackPressed || parry.IsParryActive())
        {

            if (isAttacking){
                isRotationLocked = !isRotationLocked;
            }
            
            attackPressed = false;
            quickAttackPressed = false;
            specialAttackRightPressed = false;
            specialAttackLeftPressed = false;
        }
    }
    void HandleParry()
    {
        // Check cooldown and shared charges
        if (parryPressed && Time.time >= lastParryTime + parryCooldown && !isAttacking)
        {
            if (dashController != null && dashController.HasCharges())
            {
                PerformParry();
                lastParryTime = Time.time;
            }
        }
        parryPressed = false;
    }

    public void ApplyKnockback(Vector3 knockbackVel)
    {
        // Store knockback separately
        knockbackVelocity = knockbackVel;
        knockbackEndTime = Time.time + knockbackDuration;

        // Mark this as attack-caused velocity for bouncepads
        IsAttackVelocity = true;


        // Check if knockback force exceeds threshold to lock movement
        if (knockbackVel.magnitude >= movementLockThreshold)
        {
            TriggerRumble();
            isMovementLocked = true;
            velocity = Vector3.zero; // Cancel current movement velocity
        }

        // Apply immediate velocity change
        rb.linearVelocity = new Vector3((velocity + knockbackVelocity).x, 0, (velocity + knockbackVelocity).z);

    }

    public void TriggerRumble()
    {
        float maxVelocityThreshold = 30f; // Increase this if it feels too strong too early
        float currentSpeed = rb.linearVelocity.magnitude;

        // Calculate a raw 0-1 value
        float rawIntensity = Mathf.Clamp01(currentSpeed / maxVelocityThreshold);

        // POWER SCALING: Squaring the value (raw * raw) makes low speeds much 
        // weaker and high speeds feel like a sudden peak.
        float curvedIntensity = rawIntensity * rawIntensity;

        // Define motor speeds
        // We keep a small 'floor' (0.05f) so you still feel something on light hits
        float lowFreq = Mathf.Clamp(curvedIntensity, 0.05f, 1.0f);
        float highFreq = Mathf.Clamp(curvedIntensity, 0.05f, 1.0f);
        
        float duration = 0.2f;

        StartCoroutine(Rumble(lowFreq, highFreq, duration));
    }

    private System.Collections.IEnumerator Rumble(float lowFreq, float highFreq, float duration)
    {
        // Ensure we find the specific gamepad paired to this player's PlayerInput
        if (playerInput != null && playerInput.user.pairedDevices.Count > 0 && gamepad == null)
        {
            foreach (var device in playerInput.user.pairedDevices)
            {
                if (device is Gamepad g)
                {
                    gamepad = g;
                    break;
                }
            }
        }

        if (gamepad != null)
        {
            gamepad.SetMotorSpeeds(lowFreq, highFreq);
            yield return new WaitForSeconds(duration);
            gamepad.ResetHaptics();
        }
    }

    public void SetAttackVelocity(bool value)
    {
        IsAttackVelocity = value;
    }

    //----------------------------------------------------------------------
    //  COLLISION LOGIC
    //----------------------------------------------------------------------

    void OnCollisionEnter(Collision collision)
    {
        // Prevent infinite sliding on edges
        if (collision.gameObject.name.Contains("Border"))
        {
            velocity = Vector3.zero;
        }
    }

    //----------------------------------------------------------------------
    //  GIZMOS
    //----------------------------------------------------------------------

    void OnDrawGizmos()
    {
        Gizmos.color = (playerNumber == PlayerNumber.Player1) ? Color.blue : Color.red;
        //Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    public void Die()
    {
        playerLives--;
        percentMeter.UpdateHealthUI();

        if (resetHitCoroutine != null)
        {
            StopCoroutine(resetHitCoroutine);
        }

        gamepad?.ResetHaptics();

        // Disable player input immediately
        PlayerInput pi = GetComponent<PlayerInput>();
        if (pi != null)
        {
            pi.DeactivateInput();
        }

        // Stop all movement
        ResetPlayerMovement();

        percentMeter.ResetPercent();
        
        DashChargeParticleIndicator particleIndicator = GetComponent<DashChargeParticleIndicator>();
        if (particleIndicator != null)
        {
            particleIndicator.ResetIndicator();
        }

        // Disable visual components
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.enabled = false;
        }

        // Disable collision
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
        
        // HARD STOP ALL VFX (Children of Children safe)
        foreach (var vfx in allVFX)
        {
            if (vfx == null) continue;

            vfx.Stop();                         // stoppt Emission
            vfx.gameObject.SetActive(false);    // killt Sichtbarkeit sofort
        }

        // Ask the spawn manager to respawn this player
        if (playerLives > 0)
        {
            PlayerSpawnManager.Instance.RespawnPlayer(this);
        }
        else
        {
            isDead = true;
            PlayerSpawnManager.Instance.CheckIfAllDead();
        }
    }

    public void StopAttack()
    {
        isAttacking = false;
        isRotationLocked = false;

        DeactivateAttacks();
    }

    private void DeactivateAttacks()
    {
        attackPressed = false;
        isAttacking = false;
        quickAttackPressed = false;
        specialAttackRightPressed = false;
        specialAttackLeftPressed = false;
    }
    
    public void ReactivateVFX()
    {
        if (allVFX == null) return;

        foreach (var vfx in allVFX)
        {
            if (vfx == null) continue;

            vfx.gameObject.SetActive(true);
            vfx.Stop();                     
        }
    }

    public void ResetPlayerMovement()
    {
        rb.linearVelocity = Vector3.zero;
        knockbackVelocity = Vector3.zero;
        velocity = Vector3.zero;
    }

}