using UnityEngine;

public class PlayerDeath : MonoBehaviour
{
    private bool isDead = false;

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        // ����� ����� �������� �������� ������, �������, ���������� ������ � �.�.
        Destroy(gameObject);
    }
}