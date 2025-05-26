using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// Интеллект ближнего врага: умный, адаптивный, с вариативным боем, но без сложного pathfinding.
/// Враг:
/// - Всегда знает где игрок.
/// - Может телепортироваться, если застрял или слишком далеко.
/// - Вблизи действует агрессивно и непредсказуемо: комбо, ложные атаки, рывки, уклонения, отступления.
/// - Реагирует на парирование.
/// - Не использует ActionBasedPathfinder, но умеет прыгать через препятствия и "шагать" через низкие.
public class MeleeEnemyAI : EnemyTeleportController
{
    private enum State
    {
        Idle, Pursuing, MeleeComboAttacking, Charging, Dashing, EvasionDashing, Retreating, Feinting, Jumping, StepOver, Stunned, Recovery, Dead
    }
    private State currentState = State.Pursuing;

    [Header("Detection & Movement")]
    public float detectionRadius = 12f;
    public float moveSpeed = 3.2f;
    public float jumpForce = 7.5f;
    public float stepHeight = 0.35f;
    public float stepUpSpeed = 10f;
    public float obstacleCheckDistance = 0.5f;

    [Header("Teleport Settings")]
    public float teleportDistance = 15f;

    [Header("Dash Settings")]
    public float dashSpeed = 8f;
    public float dashDuration = 0.28f;
    public float dashCooldown = 1.2f;
    private float lastDashTime = -10f;

    [Header("Dash Attack Settings")]
    public float dashAttackWindup = 0.22f; // задержка перед рывком
    public float dashAttackActive = 0.18f; // длительность самой атаки
    public float dashAttackCooldown = 2.0f;
    private float lastDashAttackTime = -10f;

    [Header("Evasion Settings")]
    public float evasionDashSpeed = 13f;
    public float evasionDashDuration = 0.13f;
    public float evasionDashCooldown = 1.5f;
    private float lastEvasionTime = -10f;

    [Header("Retreat Settings")]
    public float retreatDashSpeed = 9f;
    public float retreatDashDuration = 0.15f;
    public float retreatCooldown = 1.2f;
    private float lastRetreatTime = -10f;

    [Header("Melee Combo Settings")]
    public float meleeAttackRange = 1.25f;
    public float meleeAttackDelay = 0.22f;
    public int meleeComboCount = 2;
    public GameObject[] comboAttackHitboxes;

    [Header("Dash Attack Hitbox")]
    public GameObject dashAttackHitbox;

    [Header("Parry Projectile Hitbox")]
    public GameObject parryProjectileHitbox;

    [Header("AI Variability")]
    public float feintChance = 0.22f;
    public float dashBehindChance = 0.18f;
    public float unpredictableMoveChance = 0.12f;
    public float attackRhythmVariance = 0.18f;
    private float lastAttackTime = -10f;

    [Header("Parry (Stun Player) Settings")]
    public float parryStunDuration = 0.7f;
    public float parryKnockbackForce = 8f;
    public float stunnedTime = 1.1f;

    public GameObject exclamationPrefab;
    private GameObject exclamationInstance;

    private bool isDead = false;
    private bool isInvulnerable = true;
    private bool facingRight = true;
    private Coroutine parryProjectileCoroutine;
    private static bool groupTeleportActive = false;

    private LayerMask groundLayer;
    private Rigidbody2D rb;
    private Transform player;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) Debug.LogError("Rigidbody2D не найден!");
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) Debug.LogError("Player не найден!");
        groundLayer = LayerMask.GetMask("Ground");
        SetInvulnerable(true);
        DisableAllHitboxes();
    }

    void Update()
    {
        if (isDead || player == null || isTeleporting || currentState == State.Dead) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        var playerCombat = player.GetComponent<CombatController>();

        // --- Реакция на подготовку атаки игрока ---
        if (playerCombat != null && playerCombat.IsAttackWindup && currentState == State.Pursuing)
        {
            ReactToPlayerAttackWindup(distanceToPlayer);
            return;
        }

        // --- Телепорт, если далеко или застрял ---
        if (ShouldTeleport(distanceToPlayer))
        {
            TryTeleportSmart();
            return;
        }

        // --- Если игрок далеко, подходим на дистанцию dash-атаки ---
        if (distanceToPlayer > meleeAttackRange * 1.1f)
        {
            if (distanceToPlayer < detectionRadius)
            {
                if (CanDashAttack(distanceToPlayer))
                {
                    StartCoroutine(DashAttackRoutine());
                    lastDashAttackTime = Time.time;
                    return;
                }
                MoveTowardsPlayer();
            }
            return;
        }

        // --- Если игрок в радиусе dash-атаки, делаем dash-атаку ---
        if (CanDashAttack(distanceToPlayer))
        {
            StartCoroutine(DashAttackRoutine());
            lastDashAttackTime = Time.time;
            return;
        }

        // --- Если игрок в радиусе обычной атаки, делаем комбо ---
        if (distanceToPlayer <= meleeAttackRange)
        {
            if (Random.value < feintChance)
            {
                StartCoroutine(FeintAttackRoutine());
                return;
            }
            if (Time.time - lastAttackTime > meleeAttackDelay + Random.Range(-attackRhythmVariance, attackRhythmVariance))
            {
                StartCoroutine(MeleeComboAttackRoutine());
                lastAttackTime = Time.time;
                return;
            }
        }

        // --- Если игрок слишком близко, делаем уклоняющийся или отступающий рывок ---
        if (distanceToPlayer < meleeAttackRange * 0.7f)
        {
            if (CanDashBehind())
            {
                StartCoroutine(DashBehindPlayerRoutine());
                lastEvasionTime = Time.time;
                return;
            }
            else if (CanRetreat())
            {
                StartCoroutine(RetreatDashRoutine());
                lastRetreatTime = Time.time;
                return;
            }
        }

        // --- Немного хаоса: неожиданные манёвры ---
        if (Random.value < unpredictableMoveChance)
        {
            StartCoroutine(UnpredictableMoveRoutine());
            return;
        }
    }

    private bool CanDashAttack(float distanceToPlayer)
    {
        return currentState == State.Pursuing
            && distanceToPlayer < meleeAttackRange * 1.1f
            && Time.time - lastDashAttackTime > dashAttackCooldown;
    }

    private bool CanDashBehind()
    {
        return Random.value < 0.6f && Time.time - lastEvasionTime > evasionDashCooldown;
    }

    private bool CanRetreat()
    {
        return Time.time - lastRetreatTime > retreatCooldown;
    }

    private void ReactToPlayerAttackWindup(float distanceToPlayer)
    {
        // Приоритет: парирование > уклонение > отступление
        float r = Random.value;
        if (distanceToPlayer <= meleeAttackRange && r < 0.5f)
        {
            // Парировать
            StartCoroutine(ParryProjectileWindow(0.25f));
        }
        else if (distanceToPlayer < meleeAttackRange * 0.7f && r < 0.8f)
        {
            // Уклоняющийся рывок
            if (CanDashBehind())
            {
                StartCoroutine(DashBehindPlayerRoutine());
                lastEvasionTime = Time.time;
            }
            else if (CanRetreat())
            {
                StartCoroutine(RetreatDashRoutine());
                lastRetreatTime = Time.time;
            }
        }
        else
        {
            // Просто отступить
            if (CanRetreat())
            {
                StartCoroutine(RetreatDashRoutine());
                lastRetreatTime = Time.time;
            }
        }
    }

    void FixedUpdate()
    {
        // --- Блокировка движения вне состояния Pursuing ---
        if (currentState != State.Pursuing)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }
        // Движение к игроку реализовано в MoveTowardsPlayer (Update)
    }

    private void MoveTowardsPlayer()
    {
        if (currentState != State.Pursuing) return;
        float dir = Mathf.Sign(player.position.x - transform.position.x);
        Flip(dir);
        rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);
    }

    private bool ShouldTeleport(float distanceToPlayer)
    {
        if (isTeleporting || groupTeleportActive) return false;
        if (distanceToPlayer > teleportDistance && Time.time - lastTeleportTime > teleportCooldown)
            return true;
        if (!IsGrounded() && !IsGroundBelow())
            return true;
        return false;
    }

    private void TryTeleportSmart()
    {
        groupTeleportActive = true;
        lastTeleportTime = Time.time;
        StartCoroutine(TeleportToPlayerRoutine(teleportChargeTimeFar));
        StartCoroutine(ReleaseGroupTeleportFlag());
    }

    protected override IEnumerator TeleportToPlayerRoutine(float chargeTime)
    {
        isTeleporting = true;
        yield return new WaitForSeconds(chargeTime);
        Vector3 offset = Vector3.right * Random.Range(-1.5f, 1.5f);
        transform.position = player.position + offset;
        lastTeleportTime = Time.time;
        isTeleporting = false;
    }

    private IEnumerator ReleaseGroupTeleportFlag()
    {
        yield return new WaitForSeconds(1.5f);
        groupTeleportActive = false;
    }

    // --- Бой и манёвры ---
    IEnumerator MeleeComboAttackRoutine()
    {
        if (currentState != State.Pursuing) yield break;
        currentState = State.MeleeComboAttacking;
        SetInvulnerable(false);

        ShowExclamation(); // Показать сигнал

        // Всегда поворачиваемся к игроку перед атакой
        Flip(Mathf.Sign(player.position.x - transform.position.x));

        // ... замах ...
        yield return new WaitForSeconds(0.25f);

        HideExclamation(); // Скрыть сигнал

        for (int i = 0; i < meleeComboCount; i++)
        {
            float cancelWindow = 0.2f + Random.Range(-0.1f, 0.1f);
            float timer = 0f;
            while (timer < cancelWindow)
            {
                float distanceToPlayer = Vector2.Distance(transform.position, player.position);
                if (distanceToPlayer > meleeAttackRange)
                {
                    currentState = State.Pursuing;
                    SetInvulnerable(true);
                    DisableAllHitboxes();
                    yield break;
                }
                var parryHitbox = player.GetComponentInChildren<ParryHitbox>();
                if (parryHitbox != null && parryHitbox.IsParrying)
                {
                    currentState = State.Pursuing;
                    SetInvulnerable(true);
                    DisableAllHitboxes();
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            if (parryProjectileCoroutine != null)
                StopCoroutine(parryProjectileCoroutine);
            parryProjectileCoroutine = StartCoroutine(ParryProjectileWindow(0.2f));

            yield return new WaitForSeconds(meleeAttackDelay + Random.Range(-0.1f, 0.1f));

            if (currentState == State.Stunned || isDead)
                yield break;

            EnableComboHitbox(i);
            yield return new WaitForSeconds(0.15f + Random.Range(-0.05f, 0.05f));
            DisableComboHitbox(i);

            yield return new WaitForSeconds(0.1f + Random.Range(-0.05f, 0.05f));
        }

        SetInvulnerable(true);
        currentState = State.Pursuing;
    }

    IEnumerator DashAttackRoutine()
    {
        if (currentState != State.Pursuing) yield break;
        currentState = State.Dashing;
        SetInvulnerable(true);

        // Сигнал игроку
        ShowExclamation();
        // Замах
        yield return new WaitForSeconds(dashAttackWindup);
        HideExclamation();

        // Рывок к игроку
        float dir = Mathf.Sign(player.position.x - transform.position.x);
        Flip(dir);
        if (dashAttackHitbox != null)
            dashAttackHitbox.SetActive(true);

        rb.linearVelocity = new Vector2(dir * dashSpeed, 0f);

        yield return new WaitForSeconds(dashAttackActive);

        rb.linearVelocity = Vector2.zero;
        if (dashAttackHitbox != null)
            dashAttackHitbox.SetActive(false);

        SetInvulnerable(false);
        currentState = State.Pursuing;
    }

    IEnumerator FeintAttackRoutine()
    {
        if (currentState != State.Pursuing) yield break;
        currentState = State.Feinting;
        SetInvulnerable(false);

        // Всегда поворачиваемся к игроку перед ложной атакой
        Flip(Mathf.Sign(player.position.x - transform.position.x));

        yield return new WaitForSeconds(0.13f + Random.Range(-0.04f, 0.08f));
        if (Random.value < 0.7f)
            StartCoroutine(DashBehindPlayerRoutine());
        else
            StartCoroutine(UnpredictableMoveRoutine());
        SetInvulnerable(true);
        currentState = State.Pursuing;
    }

    IEnumerator DashBehindPlayerRoutine()
    {
        currentState = State.EvasionDashing;
        SetInvulnerable(true);
        float timer = 0f;
        Vector2 behindDir = (player.position.x > transform.position.x) ? Vector2.left : Vector2.right;
        Flip(behindDir.x);
        Vector2 dashTarget = (Vector2)player.position + behindDir * 1.2f;
        Vector2 dashDir = (dashTarget - (Vector2)transform.position).normalized;
        rb.linearVelocity = dashDir * evasionDashSpeed;
        while (timer < evasionDashDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
        SetInvulnerable(false);
        currentState = State.Pursuing;
    }

    IEnumerator UnpredictableMoveRoutine()
    {
        currentState = State.EvasionDashing;
        SetInvulnerable(true);
        float timer = 0f;
        Vector2 dir = Random.value < 0.5f ? Vector2.right : Vector2.left;
        Flip(dir.x);
        rb.linearVelocity = dir * evasionDashSpeed;
        while (timer < evasionDashDuration * 0.7f)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
        SetInvulnerable(false);
        currentState = State.Pursuing;
    }

    IEnumerator RetreatDashRoutine()
    {
        currentState = State.Retreating;
        SetInvulnerable(false);
        float direction = Mathf.Sign(transform.position.x - player.position.x);
        Flip(direction);
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
        SetInvulnerable(true);
    }

    IEnumerator ParryProjectileWindow(float duration)
    {
        if (parryProjectileHitbox != null)
            parryProjectileHitbox.SetActive(true);
        yield return new WaitForSeconds(duration);
        if (parryProjectileHitbox != null)
            parryProjectileHitbox.SetActive(false);
    }

    IEnumerator StepOverRoutine()
    {
        currentState = State.StepOver;
        float timer = 0f;
        while (timer < stepHeight / stepUpSpeed)
        {
            transform.position += Vector3.up * stepUpSpeed * Time.deltaTime;
            timer += Time.deltaTime;
            yield return null;
        }
        currentState = State.Pursuing;
    }

    IEnumerator JumpRoutine()
    {
        currentState = State.Jumping;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        yield return new WaitForSeconds(0.2f);
        currentState = State.Pursuing;
    }

    // --- Вспомогательные методы движения ---
    private bool IsObstacleAhead()
    {
        Vector2 dir = Vector2.right * Mathf.Sign(player.position.x - transform.position.x);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, obstacleCheckDistance, groundLayer);
        return hit.collider != null;
    }

    private bool CanStepOver()
    {
        Vector2 dir = Vector2.right * Mathf.Sign(player.position.x - transform.position.x);
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.05f;
        RaycastHit2D hitLow = Physics2D.Raycast(origin, dir, 0.2f, groundLayer);
        if (hitLow.collider == null) return false;
        Vector2 stepOrigin = origin + Vector2.up * stepHeight;
        RaycastHit2D hitHigh = Physics2D.Raycast(stepOrigin, dir, 0.2f, groundLayer);
        return hitHigh.collider == null;
    }

    private bool CanJump()
    {
        Vector2 dir = Vector2.right * Mathf.Sign(player.position.x - transform.position.x);
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, 1.0f, groundLayer);
        return hit.collider == null;
    }

    private bool IsGroundBelow()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 2.0f, groundLayer);
        return hit.collider != null;
    }

    // --- Парирование и урон ---
    public void TryParry(Vector2 attackerPosition)
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            var stun = playerObj.GetComponent<PlayerStunController>();
            if (stun != null)
            {
                Vector2 knockDir = (playerObj.transform.position - transform.position).normalized;
                stun.Stun(stunnedTime, knockDir, parryKnockbackForce);
            }
        }
        BlockAttack(attackerPosition);

        if (currentState == State.MeleeComboAttacking ||
            currentState == State.Dashing)
        {
            StopAllCoroutines();
            currentState = State.Stunned;
            SetInvulnerable(false);
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

    private void Flip(float moveDir)
    {
        if ((moveDir > 0 && !facingRight) || (moveDir < 0 && facingRight))
        {
            facingRight = !facingRight;
            Vector3 s = transform.localScale;
            s.x *= -1;
            transform.localScale = s;
        }
    }

    IEnumerator StunnedRoutine()
    {
        yield return new WaitForSeconds(stunnedTime);
        currentState = State.Pursuing;
        SetInvulnerable(true);
    }

    public void TakeDamage()
    {
        if (isDead) return;
        isDead = true;
        currentState = State.Dead;
        Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead) return;

        // Парирование только если реально атакован
        if (collision.CompareTag("PlayerAttack") || collision.CompareTag("DashAttack"))
        {
            if (isInvulnerable && !IsDashingOrAttacking() && currentState != State.EvasionDashing && currentState != State.Retreating)
            {
                TryParry(collision.transform.position);
                return;
            }
            else if (!isInvulnerable)
            {
                TakeDamage();
                return;
            }
        }
        var proj = collision.GetComponent<Projectile>();
        if (proj != null && proj.isReflected)
        {
            if (isInvulnerable && !IsDashingOrAttacking() && currentState != State.EvasionDashing && currentState != State.Retreating)
            {
                TryParry(collision.transform.position);
                return;
            }
            else if (!isInvulnerable)
            {
                TakeDamage();
                return;
            }
        }
    }

    private void OnAttackHitboxTriggerEnter2D(Collider2D collision)
    {
        if ((currentState == State.Dashing && dashAttackHitbox != null && collision.CompareTag("Player")) ||
            (currentState == State.MeleeComboAttacking && IsComboHitbox(collision)))
        {
            var parry = collision.GetComponent<ParryHitbox>();
            if (parry != null && parry.IsParrying)
            {
                StopAllCoroutines();
                currentState = State.Stunned;
                SetInvulnerable(false);
                rb.linearVelocity = new Vector2(-Mathf.Sign(transform.position.x - player.position.x) * 5f, 3f);
                DisableAllHitboxes();
                StartCoroutine(StunnedRoutine());
                return;
            }

            var playerDeath = collision.GetComponent<PlayerDeath>();
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
            comboAttackHitboxes[index].SetActive(true);
    }

    private void DisableComboHitbox(int index)
    {
        if (comboAttackHitboxes != null && index < comboAttackHitboxes.Length && comboAttackHitboxes[index] != null)
            comboAttackHitboxes[index].SetActive(false);
    }

    private void DisableAllHitboxes()
    {
        if (dashAttackHitbox != null)
            dashAttackHitbox.SetActive(false);
        if (comboAttackHitboxes != null)
            foreach (var hitbox in comboAttackHitboxes)
                if (hitbox != null) hitbox.SetActive(false);
        if (parryProjectileHitbox != null)
            parryProjectileHitbox.SetActive(false);
    }

    void SetInvulnerable(bool value)
    {
        isInvulnerable = value;
    }

    private bool IsDashingOrAttacking()
    {
        return currentState == State.Dashing || currentState == State.MeleeComboAttacking;
    }

    private void ShowExclamation()
    {
        if (exclamationPrefab != null && exclamationInstance == null)
        {
            exclamationInstance = Instantiate(exclamationPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity, transform);
        }
    }

    private void HideExclamation()
    {
        if (exclamationInstance != null)
        {
            Destroy(exclamationInstance);
            exclamationInstance = null;
        }
    }
}