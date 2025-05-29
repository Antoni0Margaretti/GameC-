using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Для игрока
        if (other.CompareTag("Player"))
        {
            var death = other.GetComponent<PlayerDeath>();
            if (death != null)
                death.Die();
            else
            {
                // Альтернативно, если нет PlayerDeath
                var movement = other.GetComponent<PlayerController>();
                if (movement != null)
                    movement.Die();
            }
        }
        // Для врагов
        else if (other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponent<MeleeEnemyAI>();
            if (enemy != null)
                enemy.TakeDamage(); // или enemy.Die();
            // Аналогично для других типов врагов
        }
    }
}