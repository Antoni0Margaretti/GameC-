using UnityEngine;
using System.Collections;

public class MeleeEnemyAI : EnemyTeleportController
{
    private enum State { Pursuing, Charging, Dashing, Recovery, Stunned, MeleeComboAttacking, EvasionDashing, Retreating }
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

    [Header("Combo Attack Hitboxes")]
    public Collider2D[] comboAttackHitboxes;

    [Header("Dash Attack Hitbox")]
    public Collider2D dashAttackHitbox;

    [Header("Parry Projectile Hitbox")]
    public Collider2D parryProjectileHitbox;

    [Header("Melee Combo Settings")]
    public float meleeAttackRange = 1.2f;
    public float meleeAttackDelay = 0.25f;
    public int meleeComboCount = 2;

    [Header("Evasion Dash Settings")]
    public float evasionDashSpeed = 14f;
    public float evasionDashDuration = 0.12f;
    public float evasionDashCooldown = 1.5f;
    private float lastEvasionDashTime = -10f;

    [Header("Retreat Dash Settings")]
    public float retreatDashDistance = 1.5f;
    public float retreatDashSpeed = 10f;
    public float retreatDashDuration = 0.15f;
    public float retreatMinDistance = 0.7f;
    public float retreatCooldown = 1.2f;
    private float lastRetreatTime = -10f;

    [Header("Parry (Stun Player) Settings")]
    public float parryStunDuration = 0.7f;
    public float parryKnockbackForce = 8f;

    private bool isDead = false;
    private Coroutine parryProjectileCoroutine;
    public bool isInvulnerable = false;

    // Память о неудачных попытках добраться до игрока
    private int failedPathAttempts = 0;
    private int maxFailedAttemptsBeforeTeleport = 3;

    public bool IsDead => isDead;
    public string CurrentState => currentState.ToString();

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
        if (player == null || isDead || isTeleporting)
            return;

        if (currentState == State.Pursuing)
        {
            // Телепортация (приоритет)
            if (CanTeleport())
            {
                TryTeleport();
                return;
            }

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);

            // Разрыв дистанции
            if (distanceToPlayer < retreatMinDistance && Time.time - lastRetreatTime > retreatCooldown)
            {
                if (CanRetreat())
                {
                    StartCoroutine(RetreatDashRoutine());
                    return;
                }
                else if (Time.time - lastEvasionDashTime > evasionDashCooldown &&
                         CanDash((transform.position - player.position).normalized, evasionDashSpeed * evasionDashDuration))
                {
                    StartCoroutine(EvasionDashRoutine((transform.position - player.position).normalized));
                    return;
                }
            }

            float horizontalDistance = Mathf.Abs(transform.position.x - player.position.x);
            float verticalDistance = Mathf.Abs(transform.position.y - player.position.y);

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
        if (currentState == State.Pursuing && !isTeleporting)
        {
            Vector2 targetPos = new Vector2(player.position.x, transform.position.y);
            Vector2 newPos = Vector2.MoveTowards(rb.position, targetPos, moveSpeed * Time.fixedDeltaTime);

            // Память о неудачных попытках
            if (rb.position == newPos)
            {
                failedPathAttempts++;
                if (failedPathAttempts >= maxFailedAttemptsBeforeTeleport)
                {
                    TryTeleport();
                    failedPathAttempts = 0;
                    return;
                }
            }
            else
            {
                failedPathAttempts = 0;
            }

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
            // Окно для отмены атаки (например, 0.2 сек)
            float cancelWindow = 0.2f;
            float timer = 0f;
            while (timer < cancelWindow)
            {
                float distanceToPlayer = Vector2.Distance(transform.position, player.position);
                if (distanceToPlayer > meleeAttackRange)
                {
                    currentState = State.Pursuing;
                    isInvulnerable = true;
                    DisableAllHitboxes();
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            // Окно для парирования снарядов (например, 0.2 сек)
            if (parryProjectileCoroutine != null)
                StopCoroutine(parryProjectileCoroutine);
            parryProjectileCoroutine = StartCoroutine(ParryProjectileWindow(0.2f));

            yield return new WaitForSeconds(meleeAttackDelay);

            if (currentState == State.Stunned || isDead)
                yield break;

            EnableComboHitbox(i);
            yield return new WaitForSeconds(0.15f);
            DisableComboHitbox(i);

            yield return new WaitForSeconds(0.1f);
        }

        isInvulnerable = true;
        currentState = State.Pursuing;
    }

    IEnumerator EvasionDashRoutine(Vector2 direction)
    {
        currentState = State.EvasionDashing;
        isInvulnerable = true;
        float timer = 0f;
        rb.linearVelocity = direction.normalized * evasionDashSpeed;
        while (timer < evasionDashDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
        isInvulnerable = false;
        currentState = State.Pursuing;
        lastEvasionDashTime = Time.time;
    }

    IEnumerator RetreatDashRoutine()
    {
        currentState = State.Retreating;
        isInvulnerable = false; // Враг уязвим!
        float direction = Mathf.Sign(transform.position.x - player.position.x);
        float timer = 0f;
        rb.linearVelocity = new Vector2(direction * retreatDashSpeed, 0f);
        while (timer < retreatDashDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
        lastRetreatTime = Time.time;
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
        // attackerPosition — позиция игрока, который атакует
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            var stun = playerObj.GetComponent<PlayerStunController>();
            if (stun != null)
            {
                Vector2 knockDir = (playerObj.transform.position - transform.position).normalized;
                stun.Stun(stunnedTime, knockDir, 8f); // 8f — сила отталкивания, можно вынести в переменную
            }
        }
        BlockAttack(attackerPosition);

        if (currentState == State.MeleeComboAttacking ||
            currentState == State.Dashing ||
            currentState == State.Charging)
        {
            StopAllCoroutines();
            currentState = State.Stunned;
            isInvulnerable = false;
            rb.linearVelocity = Vector2.zero;
            DisableAllHitboxes();
            StartCoroutine(StunnedRoutine());
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
        if (!CanDash((player.position - transform.position).normalized, dashSpeed * dashDuration))
        {
            currentState = State.Pursuing;
            isInvulnerable = true;
            if (parryProjectileHitbox != null)
                parryProjectileHitbox.enabled = false;
            yield break;
        }

        if (currentState != State.Pursuing)
            yield break;
        currentState = State.Charging;
        isInvulnerable = false;

        if (parryProjectileCoroutine != null)
            StopCoroutine(parryProjectileCoroutine);
        parryProjectileCoroutine = StartCoroutine(ParryProjectileWindow(0.2f));

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
        rb.linearVelocity = dashDirection * dashSpeed;
        while (timer < dashDuration)
        {
            timer += Time.deltaTime;
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
                return;

            PlayerDeath playerDeath = collision.GetComponent<PlayerDeath>();
            if (playerDeath != null)
                playerDeath.Die();
        }
    }

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

    private bool IsGroundAhead(float checkDistance = 0.3f)
    {
        Vector2 origin = (Vector2)transform.position + Vector2.right * Mathf.Sign(player.position.x - transform.position.x) * checkDistance;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 1.0f, LayerMask.GetMask("Ground"));
        return hit.collider != null;
    }

    private bool CanDash(Vector2 direction, float dashDistance)
    {
        Vector2 origin = (Vector2)transform.position;
        Vector2 dashEnd = origin + direction.normalized * dashDistance;
        RaycastHit2D wallHit = Physics2D.Raycast(origin, direction, dashDistance, LayerMask.GetMask("Ground"));
        RaycastHit2D groundHit = Physics2D.Raycast(dashEnd, Vector2.down, 1.0f, LayerMask.GetMask("Ground"));
        return wallHit.collider == null && groundHit.collider != null;
    }

    private bool CanRetreat()
    {
        float direction = Mathf.Sign(transform.position.x - player.position.x);
        return CanDash(Vector2.right * direction * -1f, retreatDashDistance);
    }
}
