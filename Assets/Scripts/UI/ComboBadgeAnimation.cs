using UnityEngine;
using System.Collections;

public class ComboBadgeAnimation : MonoBehaviour
{
    public float scaleUpDuration = 0.2f;   // How fast it pops up
    public float visibleDuration = 0.4f;   // How long it stays big
    public float scaleDownDuration = 0.15f; // How fast it shrinks away
    
    public Vector3 targetScale = new Vector3(.8f, .8f, .8f);

    void Start()
    {
        // Set scale to zero immediately to avoid a 1-frame flicker of the full size
        transform.localScale = Vector3.zero;
        StartCoroutine(AnimateBadge());
    }

    IEnumerator AnimateBadge()
    {
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
}