using UnityEngine;

public class PlayerDeath : MonoBehaviour
{
    private bool isDead = false;

    public GameObject deathScreenPrefab; // ��������� Canvas DeathScreen � ����������

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        if (deathScreenPrefab != null)
        {
            Instantiate(deathScreenPrefab);
        }
        // ����� �������� �������� ������, ��������� ������� � �.�.
        Destroy(gameObject);
    }
}