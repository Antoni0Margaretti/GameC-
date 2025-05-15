using UnityEngine;

public class PlayerDeath : MonoBehaviour
{
    private bool isDead = false;

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        // Здесь можно добавить анимацию смерти, эффекты, перезапуск уровня и т.д.
        Destroy(gameObject);
    }
}