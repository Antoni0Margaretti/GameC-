using UnityEngine;

public class AutoDestroyVFX : MonoBehaviour
{
    public float lifetime = 1.0f; // выставьте под длину анимации

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}