using UnityEngine;

public class PlayerDeath : MonoBehaviour
{
    private bool isDead = false;

    public GameObject deathScreenPrefab; // ѕрив€жите Canvas DeathScreen в инспекторе

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        if (deathScreenPrefab != null)
        {
            Instantiate(deathScreenPrefab);
        }
        // ћожно добавить анимацию смерти, остановку времени и т.д.
        Destroy(gameObject);
    }
}