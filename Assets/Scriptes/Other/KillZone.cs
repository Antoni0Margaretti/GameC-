using UnityEngine;

public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // ��� ������
        if (other.CompareTag("Player"))
        {
            var death = other.GetComponent<PlayerDeath>();
            if (death != null)
                death.Die();
            else
            {
                // �������������, ���� ��� PlayerDeath
                var movement = other.GetComponent<PlayerController>();
                if (movement != null)
                    movement.Die();
            }
        }
        // ��� ������
        else if (other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponent<MeleeEnemyAI>();
            if (enemy != null)
                enemy.TakeDamage(); // ��� enemy.Die();
            // ���������� ��� ������ ����� ������
        }
    }
}