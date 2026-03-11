using UnityEngine;
using System.Collections;

public class Shake : MonoBehaviour{
    public static Shake instance; 
    public AnimationCurve curve;
    public float baseDuration = 0.1f;
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public IEnumerator ShakeCamera(float intensity)
    {
        Vector3 originalPos = transform.localPosition;
        float elapsed = 0.0f;

         float duration = baseDuration * Mathf.Clamp(intensity, 0.2f, 5f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float strength = curve.Evaluate(t) * intensity;

            float x = Random.Range(-1f, 1f) * strength;
            float z = Random.Range(-1f, 1f) * strength;

            transform.localPosition = new Vector3(x, originalPos.y, z);

            yield return null;
        }

        transform.localPosition = originalPos;
    }
}
