using UnityEngine;

public class AutoDestroyVFX : MonoBehaviour
{
    public float lifetime = 1.0f; // ��������� ��� ����� ��������

    void Start()
    {
        Destroy(gameObject, lifetime);
    }
}