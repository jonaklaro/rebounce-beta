using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class AttackBadgeAnimation : MonoBehaviour
{
    public float scaleUpDuration = 0.2f;   // How fast it pops up
    public float visibleDuration = 0.2f;   // How long it stays big
    public float scaleDownDuration = 0.15f; // How fast it shrinks away
    
    public Vector3 targetScale = new Vector3(.8f, .8f, .8f);

    [SerializeField] private GameObject attackImage;
    [SerializeField] private Image attackFG;
    [SerializeField] private GameObject textComponent;
    [SerializeField] private TextMeshProUGUI hitText;
    [SerializeField] private TextMeshProUGUI percentText;

    private static Color[] playerColors = PlayerColors.playerColors;


    void Start()
    {
        // Set scale to zero immediately to avoid a 1-frame flicker of the full size
        transform.localScale = Vector3.zero;
        StartCoroutine(AnimateBadge());

    }

    private IEnumerator AnimateBadge()
    {
        // SetupAttackImage();

        float elapsed = 0;

        // 1. SCALE UP
        while (elapsed < scaleUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / scaleUpDuration);
            transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);
            yield return null;
        }
        
        // Ensure it hits the exact target scale
        transform.localScale = targetScale;

        // 2. WAIT
        yield return new WaitForSeconds(visibleDuration);

        // 3. SCALE DOWN
        elapsed = 0;
        while (elapsed < scaleDownDuration)
        {
            elapsed += Time.deltaTime;
            // Reverse SmoothStep: from 1 down to 0
            float t = Mathf.SmoothStep(0f, 1f, elapsed / scaleDownDuration);
            transform.localScale = Vector3.Lerp(targetScale, Vector3.zero, t);
            yield return null;
        }

        // 4. CLEANUP
        // Using transform.root ensures you grab the top-most parent regardless of hierarchy depth
        Destroy(gameObject.transform.parent.gameObject);
    }

    public void SetupAttackImage(ComboBuffRewards reward, int playerIndex = 0)
    {

        // rotate attack image a little
        attackImage.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-30f, 30f));

        // get random primary color
        var playerColor = playerColors[playerIndex];
        attackFG.color = playerColor;


        // rotate text a little
        textComponent.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-30f, 30f));

        // Set text to "Bam!" or "KPOW!" or "Punch!"
        var textOptions = new string[] {"Bam!", "KPOW!", "Punch!", "Whack!", "Ouch!", "Zoinks!", "Oof!"};
        hitText.text = textOptions[Random.Range(0, textOptions.Length)];
        if (reward.percentToAdd > 0)
        {
            percentText.text = $"+{reward.percentToAdd}%";
        } else {
            if (reward.knockbackToAdd > 0){
                percentText.color = Color.yellow;
                percentText.text = "POWER!!!";
            } else {
                percentText.text = "";
            }
        }
    }

}