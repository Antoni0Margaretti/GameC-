using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Интеллект ближнего врага: умный, адаптивный, с вариативным боем.
/// </summary>
public class MeleeEnemyAI : EnemyTeleportController
{
    // --- Состояния ---
    private enum State
    {
        Idle, Pursuing, MeleeComboAttacking, Charging, Dashing, EvasionDashing, Retreating, Feinting, Jumping, StepOver, Stunned, Recovery, Dead
    }
    [SerializeField] private State currentState = State.Pursuing;

    // --- Параметры обнаружения и движения ---
    [Header("Detection & Movement")]
    [SerializeField] public float detectionRadius = 12f;
    [SerializeField] public float moveSpeed = 3.2f;
    [SerializeField] public float jumpForce = 7.5f;
    [SerializeField] public float stepHeight = 0.35f;
    [SerializeField] public float stepUpSpeed = 10f;
    [SerializeField] public float obstacleCheckDistance = 0.5f;

    private bool playerDetected = false;

    // --- Телепорт ---
    [Header("Teleport Settings")]
    [SerializeField] public float teleportDistance = 15f;
    [SerializeField] public float aggressiveTeleportDistance = 20f; // Новая фишка: агрессивный телепорт

    // --- Dash-атака ---
    [Header("Dash Attack Settings")]
    [SerializeField] public float dashSpeed = 8f;
    [SerializeField] public float dashAttackWindup = 0.22f;
    [SerializeField] public float dashAttackActive = 0.18f;
    [SerializeField] public float dashAttackCooldown = 2.0f;
    [SerializeField] public GameObject dashAttackHitbox;
    private float lastDashAttackTime = -10f;

    // --- Обычные рывки и уклонения ---
    [Header("Evasion Settings")]
    [SerializeField] public float evasionDashSpeed = 13f;
    [SerializeField] public float evasionDashDuration = 0.13f;
    [SerializeField] public float evasionDashCooldown = 1.5f;
    private float lastEvasionTime = -10f;

    [Header("Retreat Settings")]
    [SerializeField] public float retreatDashSpeed = 9f;
    [SerializeField] public float retreatDashDuration = 0.15f;
    [SerializeField] public float retreatCooldown = 1.2f;
    private float lastRetreatTime = -10f;

    // --- Ближний бой ---
    [Header("Melee Combo Settings")]
    [SerializeField] public float meleeAttackRange = 1.25f;
    [SerializeField] public float meleeAttackDelay = 0.22f;
    [SerializeField] public int meleeComboCount = 2;
    [SerializeField] public GameObject[] comboAttackHitboxes;

    // --- Парирование ---
    [Header("Parry Projectile Hitbox")]
    [SerializeField] public GameObject parryProjectileHitbox;

    [Header("Parry (Stun Player) Settings")]
    [SerializeField] public float parryStunDuration = 0.7f;
    [SerializeField] public float parryKnockbackForce = 8f;
    [SerializeField] public float stunnedTime = 1.1f;

    // --- AI Вариативность ---
    [Header("AI Variability")]
    [SerializeField] public float feintChance = 0.22f;
    [SerializeField] public float dashBehindChance = 0.18f;
    [SerializeField] public float unpredictableMoveChance = 0.12f;
    [SerializeField] public float attackRhythmVariance = 0.18f;
    [SerializeField] public float dashBehindAndAttackChance = 0.08f;
    private float lastAttackTime = -10f;

    // --- Сигналы игроку ---
    [Header("Visual Signals")]
    [SerializeField] public GameObject exclamationPrefab;
    private GameObject exclamationInstance;

    // --- Визуальные эффекты ---
    [Header("VFX")]
    public GameObject[] comboAttackEffectPrefabs; // по одному на каждый удар комбо
    public Vector3[] comboAttackEffectOffsets;    // смещение для каждого удара
    public Vector3[] comboAttackEffectScales;     // масштаб для каждого удара

    public GameObject[] dashAttackEffectPrefabs;  // 2 эффекта для атакующего рывка
    public Vector3[] dashAttackEffectOffsets;
    public Vector3[] dashAttackEffectScales;

    public GameObject parrySuccessEffectPrefab;
    public Vector3 parrySuccessEffectOffset;
    public Vector3 parrySuccessEffectScale;

    public GameObject evasionDashEffectPrefab;
    public Vector3 evasionDashEffectOffset;
    public Vector3 evasionDashEffectScale;

    public GameObject retreatDashEffectPrefab;
    public Vector3 retreatDashEffectOffset;
    public Vector3 retreatDashEffectScale;

    // --- Внутренние переменные ---
    private bool isDead = false;
    private bool isInvulnerable = true;
    private bool facingRight = true;
    private Coroutine parryProjectileCoroutine;
    private static bool groupTeleportActive = false;
    private LayerMask groundLayer;
    private Rigidbody2D rb;
    private Transform player;

    // --- Адаптация к игроку ---
    private int playerParryCount = 0;
    private float feintAttackBonus = 0f;
    private float parryCheckWindow = 5f;
    private float lastParryCheckTime = 0f;

    // --- События (для расширения) ---
    public System.Action OnAttackStarted;
    public System.Action OnAttackEnded;

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
        if (isDead || isTeleporting || currentState == State.Dead) return;

        // Проверяем, есть ли игрок в detectionRadius
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null) return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // Детектирование игрока
        if (!playerDetected)
        {
            if (distanceToPlayer <= detectionRadius)
            {
                playerDetected = true;
                currentState = State.Pursuing;
            }
            else
            {
                currentState = State.Idle;
                rb.linearVelocity = Vector2.zero;
                return;
            }
        }
        else
        {
            if (distanceToPlayer > detectionRadius * 1.1f) // небольшой гистерезис, чтобы не мигал
            {
                playerDetected = false;
                currentState = State.Idle;
                rb.linearVelocity = Vector2.zero;
                return;
            }
        }

        // Дальнейшая логика AI (оставьте как есть)
        var playerCombat = player.GetComponent<CombatController>();

        // --- Адаптация к частым парированиям игрока ---
        if (Time.time - lastParryCheckTime > parryCheckWindow)
        {
            feintAttackBonus = playerParryCount > 2 ? 0.25f : 0f;
            playerParryCount = 0;
            lastParryCheckTime = Time.time;
        }

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

        // --- Агрессивный телепорт, если игрок слишком далеко (новая фишка) ---
        if (distanceToPlayer > aggressiveTeleportDistance && CanTeleport())
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

        // --- Если игрок в радиусе обычной атаки, делаем комбо или фейк ---
        if (distanceToPlayer <= meleeAttackRange)
        {
            if (Random.value < feintChance + feintAttackBonus)
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

        // --- Dash за спину и dash-атака (фишка) ---
        if (Random.value < dashBehindAndAttackChance && currentState == State.Pursuing && distanceToPlayer < meleeAttackRange * 1.2f)
        {
            StartCoroutine(DashBehindAndAttackRoutine());
            return;
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
        return Random.value < dashBehindChance && Time.time - lastEvasionTime > evasionDashCooldown;
    }

    private bool CanRetreat()
    {
        return Time.time - lastRetreatTime > retreatCooldown;
    }

    private void ReactToPlayerAttackWindup(float distanceToPlayer)
    {
        float r = Random.value;
        if (distanceToPlayer <= meleeAttackRange && r < 0.5f)
        {
            StartCoroutine(ParryProjectileWindow(0.25f));
        }
        else if (distanceToPlayer < meleeAttackRange * 0.7f && r < 0.8f)
        {
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
            if (CanRetreat())
            {
                StartCoroutine(RetreatDashRoutine());
                lastRetreatTime = Time.time;
            }
        }
    }

    void FixedUpdate()
    {
        if (currentState != State.Pursuing)
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            return;
        }
    }

    private void MoveTowardsPlayer()
    {
        if (currentState != State.Pursuing) return;

        float dir = Mathf.Sign(player.position.x - transform.position.x);
        Flip(dir);

        // Проверка препятствий
        if (IsObstacleAhead())
        {
            // Сначала пробуем перешагнуть
            if (CanStepOver())
            {
                StartCoroutine(StepOverRoutine());
                return;
            }
            // Если нельзя перешагнуть — пробуем прыгнуть
            else if (CanJump())
            {
                StartCoroutine(JumpRoutine());
                return;
            }
            // Если нельзя ни то, ни другое — останавливаемся
            else
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
                return;
            }
        }

        // Если путь свободен — двигаемся к игроку
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

        ShowExclamation();
        OnAttackStarted?.Invoke();

        Flip(Mathf.Sign(player.position.x - transform.position.x));
        yield return new WaitForSeconds(0.25f);

        HideExclamation();

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
                    OnAttackEnded?.Invoke();
                    yield break;
                }
                var parryHitbox = player.GetComponentInChildren<ParryHitbox>();
                if (parryHitbox != null && parryHitbox.IsParrying)
                {
                    currentState = State.Pursuing;
                    SetInvulnerable(true);
                    DisableAllHitboxes();
                    OnAttackEnded?.Invoke();
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
            {
                OnAttackEnded?.Invoke();
                yield break;
            }

            EnableComboHitbox(i);

            // Воспроизвести эффект удара для текущего удара
            if (comboAttackEffectPrefabs != null && i < comboAttackEffectPrefabs.Length && comboAttackEffectPrefabs[i] != null)
            {
                Vector3 offset = Vector3.zero;
                if (comboAttackEffectOffsets != null && i < comboAttackEffectOffsets.Length)
                    offset = comboAttackEffectOffsets[i];
                if (!facingRight)
                    offset.x = -offset.x;
                Vector3 pos = transform.position + offset;
                var fx = Instantiate(comboAttackEffectPrefabs[i], pos, Quaternion.identity, transform);
                if (comboAttackEffectScales != null && i < comboAttackEffectScales.Length)
                    fx.transform.localScale = comboAttackEffectScales[i];
            }

            yield return new WaitForSeconds(0.15f + Random.Range(-0.05f, 0.05f));
            DisableComboHitbox(i);

            yield return new WaitForSeconds(0.1f + Random.Range(-0.05f, 0.05f));
        }

        SetInvulnerable(true);
        currentState = State.Pursuing;
        OnAttackEnded?.Invoke();
    }

    IEnumerator DashAttackRoutine()
    {
        if (currentState != State.Pursuing) yield break;
        currentState = State.Dashing;
        SetInvulnerable(true);

        // Активировать все эффекты атакующего рывка
        if (dashAttackEffectPrefabs != null)
        {
            for (int i = 0; i < dashAttackEffectPrefabs.Length; i++)
            {
                if (dashAttackEffectPrefabs[i] != null)
                {
                    Vector3 offset = Vector3.zero;
                    if (dashAttackEffectOffsets != null && i < dashAttackEffectOffsets.Length)
                        offset = dashAttackEffectOffsets[i];

                    // Инвертируем X-ось смещения, если персонаж смотрит влево
                    if (!facingRight)
                        offset.x = -offset.x;

                    Vector3 pos = transform.position + offset;
                    var fx = Instantiate(dashAttackEffectPrefabs[i], pos, Quaternion.identity, transform);
                    if (dashAttackEffectScales != null && i < dashAttackEffectScales.Length)
                        fx.transform.localScale = dashAttackEffectScales[i];
                }
            }
        }

        ShowExclamation();
        OnAttackStarted?.Invoke();
        yield return new WaitForSeconds(dashAttackWindup);
        HideExclamation();

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
        OnAttackEnded?.Invoke();
    }

    IEnumerator DashBehindAndAttackRoutine()
    {
        if (currentState != State.Pursuing) yield break;
        currentState = State.EvasionDashing;
        SetInvulnerable(true);

        Vector2 behindDir = (player.position.x > transform.position.x) ? Vector2.left : Vector2.right;
        Flip(behindDir.x);
        Vector2 dashTarget = (Vector2)player.position + behindDir * 1.2f;
        Vector2 dashDir = (dashTarget - (Vector2)transform.position).normalized;
        rb.linearVelocity = dashDir * evasionDashSpeed;

        if (evasionDashEffectPrefab != null)
        {
            Vector3 offset = evasionDashEffectOffset;
            if (!facingRight)
                offset.x = -offset.x;
            var fx = Instantiate(evasionDashEffectPrefab, transform.position + offset, Quaternion.identity, transform);
            fx.transform.localScale = evasionDashEffectScale;
        }

        yield return new WaitForSeconds(evasionDashDuration);

        rb.linearVelocity = Vector2.zero;
        SetInvulnerable(false);

        yield return DashAttackRoutine();
    }

    IEnumerator FeintAttackRoutine()
    {
        if (currentState != State.Pursuing) yield break;
        currentState = State.Feinting;
        SetInvulnerable(false);

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

        if (evasionDashEffectPrefab != null)
        {
            Vector3 offset = evasionDashEffectOffset;
            if (!facingRight)
                offset.x = -offset.x;
            var fx = Instantiate(evasionDashEffectPrefab, transform.position + offset, Quaternion.identity, transform);
            fx.transform.localScale = evasionDashEffectScale;
        }

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

        if (evasionDashEffectPrefab != null)
        {
            Vector3 offset = evasionDashEffectOffset;
            if (!facingRight)
                offset.x = -offset.x;
            var fx = Instantiate(evasionDashEffectPrefab, transform.position + offset, Quaternion.identity, transform);
            fx.transform.localScale = evasionDashEffectScale;
        }

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

        if (retreatDashEffectPrefab != null)
        {
            Vector3 offset = retreatDashEffectOffset;
            if (!facingRight)
                offset.x = -offset.x;
            var fx = Instantiate(retreatDashEffectPrefab, transform.position + offset, Quaternion.identity, transform);
            fx.transform.localScale = retreatDashEffectScale;
        }

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

        if (parrySuccessEffectPrefab != null)
        {
            Vector3 offset = parrySuccessEffectOffset;
            if (!facingRight)
                offset.x = -offset.x;
            var fx = Instantiate(parrySuccessEffectPrefab, transform.position + offset, Quaternion.identity, transform);
            fx.transform.localScale = parrySuccessEffectScale;
        }

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
                playerParryCount++;
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