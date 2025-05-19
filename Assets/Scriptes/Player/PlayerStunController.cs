using UnityEngine;

public class PlayerStunController : MonoBehaviour
{
    private bool isStunned = false;
    private float stunEndTime = 0f;
    private Rigidbody2D rb;
    private CombatController combatController; // если есть

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        combatController = GetComponent<CombatController>();
    }

    void Update()
    {
        if (isStunned && Time.time >= stunEndTime)
        {
            isStunned = false;
            if (combatController != null)
                combatController.enabled = true;
        }
    }

    public void Stun(float duration, Vector2 knockbackDir, float knockbackForce)
    {
        isStunned = true;
        stunEndTime = Time.time + duration;
        if (combatController != null)
            combatController.enabled = false;

        if (rb != null && knockbackForce > 0f)
            rb.velocity = new Vector2(knockbackDir.x * knockbackForce, rb.velocity.y);
    }

    public bool IsStunned => isStunned;
}