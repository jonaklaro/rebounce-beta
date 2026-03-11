using UnityEngine;

/// <summary>
/// Displays dash charges as orbiting particles around the player
/// </summary>
public class DashChargeParticleIndicator : MonoBehaviour
{
    [Header("Particle Settings")]
    [SerializeField] private GameObject particlePrefab; // Prefab mit einem Particle System oder einfachem Quad
    [SerializeField] private float orbitRadius = 1f;
    [SerializeField] private float orbitSpeed = 60f; // Grad pro Sekunde
    [SerializeField] private float heightOffset = 1.5f;
    [SerializeField] private Color chargeColor = Color.cyan;
    [SerializeField] private float particleSize = 0.2f;
    
    private GameObject[] particles;
    private Dash dashController;
    private float currentRotation = 0f;

    private void Awake()
    {
        dashController = GetComponent<Dash>();
    }

    private void Start()
    {
        InitializeParticles();
    }

    private void Update()
    {
        // Rotate particles around player
        currentRotation += orbitSpeed * Time.deltaTime;
        UpdateParticlePositions();
    }

    /// <summary>
    /// Initialize or reinitialize all particles
    /// </summary>
    private void InitializeParticles()
    {
        if (dashController == null)
        {
            Debug.LogError("[DashChargeParticleIndicator] Dash controller not found!");
            return;
        }

        particles = new GameObject[dashController.maxDashCharges];
        
        for (int i = 0; i < particles.Length; i++)
        {
            CreateParticle(i);
        }
    }

    /// <summary>
    /// Creates a single particle at the given index
    /// </summary>
    private void CreateParticle(int index)
    {
        if (particlePrefab == null)
        {
            Debug.LogError("[DashChargeParticleIndicator] Particle prefab not assigned!");
            return;
        }

        particles[index] = Instantiate(particlePrefab, transform);
        
        // Setup renderer/material
        Renderer renderer = particles[index].GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = chargeColor;
        }
        
        // Set scale
        particles[index].transform.localScale = Vector3.one * particleSize;
    }

    /// <summary>
    /// Completely reset the indicator - destroys all particles and recreates them
    /// Call this when player respawns
    /// </summary>
    public void ResetIndicator()
    {
        // Destroy all existing particles
        if (particles != null)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null)
                {
                    Destroy(particles[i]);
                }
            }
        }

        // Reinitialize
        InitializeParticles();
    }

    public void UpdateChargeDisplay(int currentCharges)
    {
        // Safety check - reinitialize if particles array is null or wrong size
        if (particles == null || particles.Length != dashController.maxDashCharges)
        {
            ResetIndicator();
            return;
        }

        // Show/hide particles based on charge count
        for (int i = 0; i < particles.Length; i++)
        {
            // If particle was destroyed, recreate it
            if (particles[i] == null)
            {
                CreateParticle(i);
            }
            
            particles[i].SetActive(i < currentCharges);
        }
    }

    private void UpdateParticlePositions()
    {
        if (particles == null) return;

        int activeCount = 0;
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] != null && particles[i].activeSelf)
            {
                activeCount++;
            }
        }
        
        if (activeCount == 0) return;
        
        float angleStep = 360f / activeCount;
        int activeIndex = 0;
        
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] != null && particles[i].activeSelf)
            {
                float angle = (currentRotation + (angleStep * activeIndex)) * Mathf.Deg2Rad;
                
                float x = Mathf.Cos(angle) * orbitRadius;
                float z = Mathf.Sin(angle) * orbitRadius;
                
                particles[i].transform.localPosition = new Vector3(x, heightOffset, z);
                
                // Make particle face camera
                if (Camera.main != null)
                {
                    particles[i].transform.LookAt(Camera.main.transform);
                }
                
                activeIndex++;
            }
        }
    }

    private void OnDestroy()
    {
        // Clean up particles when this component is destroyed
        if (particles != null)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null)
                {
                    Destroy(particles[i]);
                }
            }
        }
    }
}