using UnityEngine;
using System.Collections;

public class MeleeEnemyAI : MonoBehaviour
{
    private enum State { Pursuing, Charging, Dashing, Recovery, Stunned, MeleeComboAttacking }
    private State currentState = State.Pursuing;

    [Header("Detection & Movement")]
    public float detectionRadius = 10f;
    public float dashTriggerHorizDistance = 2f;
    public float dashTriggerVertDistance = 1f;

    [Header("Movement Settings")]
    public float moveSpeed = 3f;

    [Header("Dash (Ryvok) Settings")]
    public float chargeTime = 0.5f;
    public float dashSpeed = 8f;
    public float dashDuration = 0.3f;
    public float recoveryTime = 0.5f;
    public float stunnedTime = 1f;

    [Header("References")]
    public Transform player;

    [Header("Invulnerability / Parry")]
    public bool isInvulnerable { get; private set; } = true;

    [Header("Combo Attack Hitboxes")]
    public Collider2D[] comboAttackHitboxes; // Индекс соответствует номеру удара (0 - первый удар и т.д.)

    [Header("Dash Attack Hitbox")]
    public Collider2D dashAttackHitbox;

    [Header("Parry Projectile Hitbox")]
    public Collider2D parryProjectileHitbox; // Для парирования снарядов

    [Header("Melee Combo Settings")]
    public float meleeAttackRange = 1.2f;
    public float meleeAttackDelay = 0.25f;
    public int meleeComboCount = 2;

    private Rigidbody2D rb;
    private bool isDead = false;
    private Coroutine parryProjectileCoroutine;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
        currentState = State.Pursuing;
        isInvulnerable = true;
        DisableAllHitboxes();
    }

    void Update()
    {
        if (player == null || isDead)
            return;

        if (currentState == State.Pursuing)
        {
            float horizontalDistance = Mathf.Abs(transform.position.x - player.position.x);
            float verticalDistance = Mathf.Abs(transform.position.y - player.position.y);

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer <= meleeAttackRange)
            {
                StartCoroutine(MeleeComboAttackRoutine());
            }
            else if (horizontalDistance <= dashTriggerHorizDistance && verticalDistance <= dashTriggerVertDistance)
            {
                StartCoroutine(ChargeRoutine());
            }
        }
    }

    void FixedUpdate()
    {
        if (currentState == State.Pursuing)
        {
            Vector2 targetPos = new Vector2(player.position.x, transform.position.y);
            Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPos);
        }
    }

    IEnumerator MeleeComboAttackRoutine()
    {
        if (currentState != State.Pursuing)
            yield break;
        currentState = State.MeleeComboAttacking;
        isInvulnerable = false;

        for (int i = 0; i < meleeComboCount; i++)
        {
            // Окно для парирования снарядов (например, 0.2 сек)
            if (parryProjectileCoroutine != null)
                StopCoroutine(parryProjectileCoroutine);
            parryProjectileCoroutine = StartCoroutine(ParryProjectileWindow(0.2f));

            yield return new WaitForSeconds(meleeAttackDelay);

            if (currentState == State.Stunned || isDead)
                yield break;

            // Включаем hitbox для текущего удара
            EnableComboHitbox(i);
            yield return new WaitForSeconds(0.15f);
            DisableComboHitbox(i);

            yield return new WaitForSeconds(0.1f);
        }

        isInvulnerable = true;
        currentState = State.Pursuing;
    }

    IEnumerator ParryProjectileWindow(float duration)
    {
        if (parryProjectileHitbox != null)
            parryProjectileHitbox.enabled = true;
        yield return new WaitForSeconds(duration);
        if (parryProjectileHitbox != null)
            parryProjectileHitbox.enabled = false;
    }

    public void TryParry(Vector2 attackerPosition)
    {
        if (currentState == State.MeleeComboAttacking)
        {
            StopAllCoroutines();
            currentState = State.Stunned;
            isInvulnerable = false;
            rb.linearVelocity = Vector2.zero;
            DisableAllHitboxes();
            StartCoroutine(StunnedRoutine());
        }
        else if (isInvulnerable)
        {
            BlockAttack(attackerPosition);
        }
    }

    private void BlockAttack(Vector2 threatPosition)
    {
        Vector3 dir = threatPosition - (Vector2)transform.position;
        if (dir.x != 0)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Sign(dir.x) * Mathf.Abs(scale.x);
            transform.localScale = scale;
        }
    }

    IEnumerator ChargeRoutine()
    {
        if (currentState != State.Pursuing)
            yield break;
        currentState = State.Charging;
        isInvulnerable = false;

        // Окно для парирования снарядов (например, 0.2 сек)
        if (parryProjectileCoroutine != null)
            StopCoroutine(parryProjectileCoroutine);
        parryProjectileCoroutine = StartCoroutine(ParryProjectileWindow(0.2f));

        // Окно отмены атаки
        float cancelWindow = 0.2f;
        float timer = 0f;
        while (timer < cancelWindow)
        {
            float horizontalDistance = Mathf.Abs(transform.position.x - player.position.x);
            float verticalDistance = Mathf.Abs(transform.position.y - player.position.y);
            if (horizontalDistance > dashTriggerHorizDistance || verticalDistance > dashTriggerVertDistance)
            {
                currentState = State.Pursuing;
                isInvulnerable = true;
                if (parryProjectileHitbox != null)
                    parryProjectileHitbox.enabled = false;
                yield break;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(chargeTime - cancelWindow);
        StartCoroutine(DashRoutine());
    }

    IEnumerator DashRoutine()
    {
        currentState = State.Dashing;
        isInvulnerable = true;

        if (dashAttackHitbox != null)
            dashAttackHitbox.enabled = true;

        Vector2 dashDirection = ((Vector2)player.position - rb.position).normalized;
        float timer = 0f;
        while (timer < dashDuration)
        {
            timer += Time.deltaTime;
            rb.linearVelocity = dashDirection * dashSpeed;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;

        if (dashAttackHitbox != null)
            dashAttackHitbox.enabled = false;

        StartCoroutine(RecoveryRoutine());
    }

    IEnumerator RecoveryRoutine()
    {
        currentState = State.Recovery;
        isInvulnerable = false;
        yield return new WaitForSeconds(recoveryTime);
        currentState = State.Pursuing;
        isInvulnerable = true;
    }

    IEnumerator StunnedRoutine()
    {
        yield return new WaitForSeconds(stunnedTime);
        currentState = State.Pursuing;
        isInvulnerable = true;
    }

    public void TakeDamage()
    {
        if (isDead) return;
        isDead = true;
        Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead) return;
        if (collision.CompareTag("PlayerAttack"))
        {
            TakeDamage();
        }
        var proj = collision.GetComponent<Projectile>();
        if (proj != null && proj.isReflected)
        {
            TakeDamage();
        }
    }

    private void OnAttackHitboxTriggerEnter2D(Collider2D collision)
    {
        if ((currentState == State.Dashing && dashAttackHitbox != null && collision.CompareTag("Player")) ||
            (currentState == State.MeleeComboAttacking && IsComboHitbox(collision)))
        {
            var playerController = collision.GetComponent<PlayerController>();
            if (playerController != null && playerController.isInvulnerable)
                return; // Игнорируем урон, если игрок неуязвим

            PlayerDeath playerDeath = collision.GetComponent<PlayerDeath>();
            if (playerDeath != null)
                playerDeath.Die();
        }
    }

    // Проверка, является ли коллайдер одним из комбо-хитбоксов
    private bool IsComboHitbox(Collider2D col)
    {
        if (comboAttackHitboxes == null) return false;
        foreach (var hitbox in comboAttackHitboxes)
            if (hitbox == col) return true;
        return false;
    }

    void OnEnable()
    {
        if (dashAttackHitbox != null)
        {
            AttackHitboxTrigger trigger = dashAttackHitbox.GetComponent<AttackHitboxTrigger>();
            if (trigger != null)
                trigger.OnTriggerEnterEvent += OnAttackHitboxTriggerEnter2D;
        }
        if (comboAttackHitboxes != null)
        {
            foreach (var hitbox in comboAttackHitboxes)
            {
                if (hitbox != null)
                {
                    AttackHitboxTrigger trigger = hitbox.GetComponent<AttackHitboxTrigger>();
                    if (trigger != null)
                        trigger.OnTriggerEnterEvent += OnAttackHitboxTriggerEnter2D;
                }
            }
        }
    }
    void OnDisable()
    {
        if (dashAttackHitbox != null)
        {
            AttackHitboxTrigger trigger = dashAttackHitbox.GetComponent<AttackHitboxTrigger>();
            if (trigger != null)
                trigger.OnTriggerEnterEvent -= OnAttackHitboxTriggerEnter2D;
        }
        if (comboAttackHitboxes != null)
        {
            foreach (var hitbox in comboAttackHitboxes)
            {
                if (hitbox != null)
                {
                    AttackHitboxTrigger trigger = hitbox.GetComponent<AttackHitboxTrigger>();
                    if (trigger != null)
                        trigger.OnTriggerEnterEvent -= OnAttackHitboxTriggerEnter2D;
                }
            }
        }
    }

    private void EnableComboHitbox(int index)
    {
        if (comboAttackHitboxes != null && index < comboAttackHitboxes.Length && comboAttackHitboxes[index] != null)
            comboAttackHitboxes[index].enabled = true;
    }

    private void DisableComboHitbox(int index)
    {
        if (comboAttackHitboxes != null && index < comboAttackHitboxes.Length && comboAttackHitboxes[index] != null)
            comboAttackHitboxes[index].enabled = false;
    }

    private void DisableAllHitboxes()
    {
        if (dashAttackHitbox != null)
            dashAttackHitbox.enabled = false;
        if (comboAttackHitboxes != null)
            foreach (var hitbox in comboAttackHitboxes)
                if (hitbox != null) hitbox.enabled = false;
        if (parryProjectileHitbox != null)
            parryProjectileHitbox.enabled = false;
    }
}