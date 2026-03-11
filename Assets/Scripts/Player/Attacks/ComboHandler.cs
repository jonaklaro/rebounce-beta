using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using TMPro;
using FMODUnity;
using FMOD.Studio;

[System.Serializable]
public class ComboBuffRewards
{
    public int percentToAdd;
    public float knockbackToAdd; 
}

public class ComboHandler : MonoBehaviour
{
    [SerializeField] private int currentComboCount = 0;
    [SerializeField] private int minComboCount;
    [SerializeField] private int maxComboCount;
    private Coroutine ComboTimerCoroutine;
    private bool quickPunch = false;
    private bool chargedPunch = false;
    private bool rightSweep = false;
    private bool leftSweep = false;
    private bool parry = false;

    [SerializeField]List<ComboBuffRewards> comboBuffs;


    [Header("Visual Effects")]
    [SerializeField] private Transform badgeSpawnPoint;
    [SerializeField] private GameObject attackEffectPrefab;

    // private string comboEventPath = "event:/Combo";
    // private EventInstance comboEventInstance;

    private CapsuleController controller;

    private void Start()
    {
        // comboEventInstance = RuntimeManager.CreateInstance(comboEventPath);
        controller = GetComponent<CapsuleController>();
    }
    

    public bool IsQuickPunch
    {
        get { return quickPunch; }
        set { quickPunch = value; }
    }

    public bool IsChargedPunch
    {
        get { return chargedPunch; }
        set { chargedPunch = value; }
    }

    public bool IsRightSweep
    {
        get { return rightSweep; }
        set { rightSweep = value; }
    }
    public bool IsLeftSweep
    {
        get { return leftSweep; }
        set { leftSweep = value; }
    }

    public bool IsParry
    {
        get { return parry; }
        set { parry = value; }
    }

    public void IncreaseCombo(CapsuleController controller)
    {
        if (currentComboCount < maxComboCount)
        {
            currentComboCount++;

            badgeSpawnPoint = controller.transform;
            SpawnAttackEffect();

            // comboEventInstance.setParameterByName("Combo", currentComboCount - 1);
            // comboEventInstance.start();

            if (ComboTimerCoroutine != null)
            {
                StopCoroutine(ComboTimerCoroutine);
            }
            ComboTimerCoroutine = StartCoroutine(ComboTimer());
        }
    }


    public void SpawnAttackEffect()
    {
        if (attackEffectPrefab == null) return;

        Vector3 offsetPosition = badgeSpawnPoint.position + (Vector3.up * 1.5f);

        GameObject attack = Instantiate(attackEffectPrefab, badgeSpawnPoint.position, Quaternion.identity);

        var attackAnim = attack.GetComponentInChildren<AttackBadgeAnimation>();
        attackAnim.SetupAttackImage(comboBuffs[currentComboCount-1], controller.playerInputNumber);

        // scale badge down
        attack.transform.localScale = new Vector3(0.075f, 0.075f, 0.075f);
    }

    public void GetComboBuffs(out int percentToAdd, out float knockbackToAdd)
    {
        percentToAdd = 0;
        knockbackToAdd = 0f;

        
        int index = currentComboCount - 1;
        if (index >= 0 && index < comboBuffs.Count)
        {
            percentToAdd = comboBuffs[index].percentToAdd;
            knockbackToAdd = comboBuffs[index].knockbackToAdd;
        }
        if (currentComboCount == maxComboCount)
        {
            ResetCombo();
        }
    }

    public void ResetCombo()
    {
        currentComboCount = 0;
        if (ComboTimerCoroutine != null)
        {
            StopCoroutine(ComboTimerCoroutine);
        }
        quickPunch = false;
        chargedPunch = false;
        rightSweep = false;
        leftSweep = false;
        parry = false;
        
    }

    public void DeactivateCombo()
    {
        currentComboCount = 0;
        if (ComboTimerCoroutine != null)
        {
            StopCoroutine(ComboTimerCoroutine);
            ComboTimerCoroutine = null;
        }
    }

    public void StartComboTimer()
    {
        ComboTimerCoroutine = StartCoroutine(ComboTimer());
    }

    public IEnumerator ComboTimer()
    {
        yield return new WaitForSeconds(10);
        DeactivateCombo();
    }
    
}
