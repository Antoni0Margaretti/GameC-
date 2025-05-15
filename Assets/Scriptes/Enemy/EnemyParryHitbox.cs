using UnityEngine;

public class EnemyParryHitbox : MonoBehaviour
{
    // Ссылка на AI врага
    public MeleeEnemyAI enemyAI;

    // Свойство для проверки, может ли враг парировать снаряды в данный момент
    public bool CanParryProjectile
    {
        get
        {
            // Хитбокс активен только когда включён (enabled)
            return enemyAI != null && enabled && enemyAI.enabled;
        }
    }
}