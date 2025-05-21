using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MeleeEnemyAI : EnemyTeleportController
{
    private enum State
    {
        Pursuing, Charging, Dashing, Recovery, Stunned, MeleeComboAttacking,
        EvasionDashing, Retreating, Jumping, StepOver
    }
    private State currentState = State.Pursuing;

    [Header("Detection & Movement")]
    public float detectionRadius = 10f;
    public float dashTriggerHorizDistance = 2f;
    public float dashTriggerVertDistance = 1f;

    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    public float jumpForce = 7f;
    public float stepCheckDistance = 0.2f;
    public float stepHeight = 0.3f;
    public float stepUpSpeed = 10f;

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

    [Header("Teleport Compare Settings")]
    public float teleportCompareDistance = 8f;
    public float teleportMinPenalty = 2f;
    public float teleportMaxPenalty = 5f;

    public ActionBasedPathfinder actionPathfinder;
    private List<EnemyAction> currentActions;
    private int currentActionIndex = 0;
    private float pathRecalcTimer = 0f;
    private float pathRecalcInterval = 0.5f;

    private bool isDead = false;
    private Coroutine parryProjectileCoroutine;
    public bool isInvulnerable = false;

    public SimpleGridPathfinding pathfinding;

    private int failedPathAttempts = 0;
    private int maxFailedAttemptsBeforeTeleport = 3;

    private int playerParryCount = 0;
    private int playerDodgeCount = 0;
    private float lastPlayerParryTime = -10f;
    private float lastPlayerDodgeTime = -10f;

    private static bool groupTeleportActive = false;
    public bool IsDead => isDead;
    public string CurrentState => currentState.ToString();

    private LayerMask groundLayer;

    void Start()
    {
        actionPathfinder = GetComponent<ActionBasedPathfinder>();
        if (actionPathfinder == null)
            Debug.LogError("ActionBasedPathfinder не найден на объекте врага!");

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            Debug.LogError("Rigidbody2D не найден на объекте врага!");

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
            else
                Debug.LogError("Player не найден на сцене!");
        }
        currentState = State.Pursuing;
        isInvulnerable = true;
        DisableAllHitboxes();
        groundLayer = LayerMask.GetMask("Ground");
    }

    void Update()
    {
        if (player == null || isDead || isTeleporting)
            return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        RouteType route = AnalyzeRouteToPlayer();

        // --- Action-based pathfinding: динамический пересчёт маршрута ---
        pathRecalcTimer += Time.deltaTime;
        bool needNewPath = false;

        if (currentActions == null || currentActionIndex >= (currentActions?.Count ?? 0) ||
            (currentActions != null && Vector2.Distance(player.position, GetCurrentTargetPoint()) > 1.5f) ||
            pathRecalcTimer > pathRecalcInterval)
        {
            needNewPath = true;
        }

        if (needNewPath && currentState == State.Pursuing)
        {
            pathRecalcTimer = 0f;
            var initialState = new EnemyState
            {
                Position = transform.position,
                Velocity = rb.linearVelocity,
                IsGrounded = IsGrounded(),
                DashUsed = false,
                JumpUsed = false
            };
            currentActions = actionPathfinder.FindActionPath(transform.position, player.position, initialState);
            currentActionIndex = 0;
        }

        // Выполнение действий из action-based pathfinding
        if (currentActions != null && currentActionIndex < currentActions.Count && currentState == State.Pursuing)
        {
            ExecuteAction(currentActions[currentActionIndex]);
        }

        // --- Оставляем вашу логику телепорта и адаптации ---
        if (IsEmergencyTeleportSituation())
        {       
            if (!groupTeleportActive && CanTeleport())
            {
                groupTeleportActive = true;
                TryTeleport();
                StartCoroutine(ReleaseGroupTeleportFlag());
                failedPathAttempts = 0;
                return;
            }
        }

        if (route == RouteType.Impossible)
        {
            if (!groupTeleportActive && CanTeleport())
            {
                groupTeleportActive = true;
                TryTeleport();
                StartCoroutine(ReleaseGroupTeleportFlag());
                failedPathAttempts = 0;
                return;
            }
        }

        if (failedPathAttempts >= maxFailedAttemptsBeforeTeleport && !groupTeleportActive)
        {
            if (CanTeleport())
            {
                groupTeleportActive = true;
                TryTeleport();
                StartCoroutine(ReleaseGroupTeleportFlag());
                failedPathAttempts = 0;
                return;
            }
        }

        if (route != RouteType.Impossible)
        {
            float walkTime = distanceToPlayer / moveSpeed;
            float teleportTime = GetTeleportBaseTime(distanceToPlayer);
            float penalty = GetTeleportPenalty(distanceToPlayer);
            teleportTime += penalty;

            if (teleportTime < walkTime && !groupTeleportActive && CanTeleport())
            {
                groupTeleportActive = true;
                TryTeleport();
                StartCoroutine(ReleaseGroupTeleportFlag());
                failedPathAttempts = 0;
                return;
            }
        }

        // --- Адаптивное поведение ---
        float parryAggro = (Time.time - lastPlayerParryTime < 3f && playerParryCount > 2) ? 1f : 0f;
        float dodgeAggro = (Time.time - lastPlayerDodgeTime < 3f && playerDodgeCount > 2) ? 1f : 0f;

        // --- Атака и особые действия ---
        if (currentState == State.Pursuing)
        {
            float horizontalDistance = Mathf.Abs(transform.position.x - player.position.x);
            float verticalDistance = Mathf.Abs(transform.position.y - player.position.y);

            if (distanceToPlayer <= meleeAttackRange)
            {
                StartCoroutine(MeleeComboAttackRoutine());
            }
            else if (horizontalDistance <= dashTriggerHorizDistance && verticalDistance <= dashTriggerVertDistance)
            {
                if (CanDash((player.position - transform.position).normalized, dashSpeed * dashDuration) &&
                    !IsCliffInDirection((player.position - transform.position).normalized, dashSpeed * dashDuration))
                {
                    StartCoroutine(ChargeRoutine());
                }
            }
        }
    }

    void FixedUpdate()
    {
        // Action-based pathfinding: выполнение движения (Walk, AirControl)
        if (currentActions != null && currentActionIndex < currentActions.Count && currentState == State.Pursuing)
        {
            var action = currentActions[currentActionIndex];
            if (action.Type == EnemyActionType.Walk)
            {
                Vector2 move = action.Direction * moveSpeed;
                rb.linearVelocity = new Vector2(move.x, rb.linearVelocity.y);
                if (Vector2.Distance(transform.position, transform.position + (Vector3)action.Direction * 0.2f) < 0.2f)
                    currentActionIndex++;
            }
            else if (action.Type == EnemyActionType.AirControl)
            {
                Vector2 move = action.Direction * moveSpeed;
                rb.linearVelocity = new Vector2(move.x, rb.linearVelocity.y);
                currentActionIndex++;
            }
        }
    }

    // --- Action-based pathfinding: выполнение действий ---
    void ExecuteAction(EnemyAction action)
    {
        switch (action.Type)
        {
            case EnemyActionType.Walk:
                // Движение реализовано в FixedUpdate
                break;
            case EnemyActionType.Jump:
                if (IsGrounded())
                {
                    StartCoroutine(JumpRoutine());
                    currentActionIndex++;
                }
                break;
            case EnemyActionType.EvasionDash:
                if (!isInvulnerable)
                {
                    StartCoroutine(EvasionDashRoutine(action.Direction));
                    currentActionIndex++;
                }
                break;
            case EnemyActionType.StepOver:
                StartCoroutine(StepOverRoutine());
                currentActionIndex++;
                break;
            case EnemyActionType.AirControl:
                // Управление в воздухе реализовано в FixedUpdate
                break;
        }
    }

    Vector2 GetCurrentTargetPoint()
    {
        if (currentActions != null && currentActionIndex < currentActions.Count)
        {
            var action = currentActions[currentActionIndex];
            return transform.position + (Vector3)action.Direction * 0.5f;
        }
        return transform.position;
    }

    // --- Новые механики ---

    // Тип маршрута
    private enum RouteType { Normal, StepOver, Jump, JumpAndDash, Impossible }

    // Анализ маршрута к игроку
    private RouteType AnalyzeRouteToPlayer()
    {
        Vector2 dir = (player.position - transform.position).normalized;
        float dist = Vector2.Distance(transform.position, player.position);

        // Step-over: небольшое препятствие
        if (IsStepOverPossible(dir))
            return RouteType.StepOver;

        // Прыжок: препятствие средней высоты или пропасть
        if (IsJumpPossible(dir))
            return RouteType.Jump;

        // Прыжок + рывок: широкая пропасть
        if (IsJumpAndDashPossible(dir))
            return RouteType.JumpAndDash;

        // Если путь свободен
        if (IsPathClear(dir, dist))
            return RouteType.Normal;

        // Если ничего не подходит — невозможно
        return RouteType.Impossible;
    }

    private bool IsStepOverPossible(Vector2 dir)
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.05f;
        RaycastHit2D hitLow = Physics2D.Raycast(origin, dir, stepCheckDistance, groundLayer);
        if (hitLow.collider == null) return false;
        Vector2 stepOrigin = origin + Vector2.up * stepHeight;
        RaycastHit2D hitHigh = Physics2D.Raycast(stepOrigin, dir, stepCheckDistance, groundLayer);
        return hitHigh.collider == null;
    }

    private bool IsJumpPossible(Vector2 dir)
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, 1.0f, groundLayer);
        if (hit.collider == null)
        {
            // Пропасть — можно прыгнуть
            return true;
        }
        // Высокая стена — нельзя перепрыгнуть
        return false;
    }

    private bool IsJumpAndDashPossible(Vector2 dir)
    {
        Vector2 origin = (Vector2)transform.position + Vector2.up * 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(origin, dir, 2.0f, groundLayer);
        return hit.collider == null;
    }

    private bool IsPathClear(Vector2 dir, float dist)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, dist, groundLayer);
        return hit.collider == null;
    }

    private bool IsCliffInDirection(Vector2 dir, float distance)
    {
        Vector2 dashEnd = (Vector2)transform.position + dir.normalized * distance;
        RaycastHit2D groundHit = Physics2D.Raycast(dashEnd, Vector2.down, 1.0f, groundLayer);
        return groundHit.collider == null;
    }

    private bool IsObstacleBehind()
    {
        Vector2 dir = Vector2.right * -Mathf.Sign(player.position.x - transform.position.x);
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, retreatDashDistance, groundLayer);
        return hit.collider != null;
    }

    // Проверка нештатных ситуаций (например, враг падает в пропасть)
    private bool IsEmergencyTeleportSituation()
    {
        if (!IsGrounded())
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 5f, groundLayer);
            if (hit.collider == null)
                return true;
        }
        return false;
    }

    // Получить базовое время телепорта (можно доработать под разные типы телепорта)
    private float GetTeleportBaseTime(float distance)
    {
        // Можно использовать teleportChargeTimeFar/teleportChargeTimeNear из EnemyTeleportController
        // Здесь для простоты — 0.5f
        return 0.5f;
    }

    // Штраф за телепорт на близкой дистанции
    private float GetTeleportPenalty(float distance)
    {
        if (distance >= teleportCompareDistance)
            return 0f;
        float t = 1f - Mathf.Clamp01(distance / teleportCompareDistance);
        return Mathf.Lerp(teleportMinPenalty, teleportMaxPenalty, t);
    }

    // Step-over
    private IEnumerator StepOverRoutine()
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

    // Прыжок
    private IEnumerator JumpRoutine()
    {
        currentState = State.Jumping;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        yield return new WaitForSeconds(0.2f);
        currentState = State.Pursuing;
    }

    // Прыжок + рывок
    private IEnumerator JumpAndDashRoutine()
    {
        currentState = State.Jumping;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        yield return new WaitForSeconds(0.15f);
        StartCoroutine(EvasionDashRoutine(Vector2.right * Mathf.Sign(player.position.x - transform.position.x)));
        yield return new WaitForSeconds(evasionDashDuration);
        currentState = State.Pursuing;
    }

    // --- Остальные методы (атаки, рывки, телепорт, память, адаптация) ---
    // Групповая логика: сбросить флаг телепорта через короткое время
    private IEnumerator ReleaseGroupTeleportFlag()
    {
        yield return new WaitForSeconds(1.5f);
        groupTeleportActive = false;
    }

    IEnumerator MeleeComboAttackRoutine()
    {
        if (currentState != State.Pursuing)
            yield break;
        currentState = State.MeleeComboAttacking;
        isInvulnerable = false;

        for (int i = 0; i < meleeComboCount; i++)
        {
            float cancelWindow = 0.2f; // окно отмены
            float timer = 0f;
            bool canCancel = true;
            // Окно отмены
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
                var parryHitbox = player.GetComponentInChildren<ParryHitbox>();
                if (parryHitbox != null && parryHitbox.IsParrying)
                {
                    currentState = State.Pursuing;
                    isInvulnerable = true;
                    DisableAllHitboxes();
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }
            // После окна отмены атака всегда завершается

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
        isInvulnerable = false;
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

        float cancelWindow = 0.2f; // окно отмены
        float timer = 0f;
        // Окно отмены
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
            var parryHitbox = player.GetComponentInChildren<ParryHitbox>();
            if (parryHitbox != null && parryHitbox.IsParrying)
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
        // После окна отмены атака всегда совершается

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

        bool isParry = false;

        if (collision.CompareTag("PlayerAttack"))
        {
            if (isInvulnerable)
            {
                TryParry(collision.transform.position);
                isParry = true;
            }
            else
            {
                TakeDamage();
            }
        }
        else if (collision.CompareTag("DashAttack"))
        {
            if (isInvulnerable)
            {
                BlockAttack(collision.transform.position);
                isParry = true;
            }
            else
            {
                TakeDamage();
            }
        }
        else
        {
            var proj = collision.GetComponent<Projectile>();
            if (proj != null && proj.isReflected)
            {
                if (isInvulnerable)
                {
                    BlockAttack(collision.transform.position);
                    isParry = true;
                }
                else
                {
                    TakeDamage();
                }
            }
        }
        // Здесь можно добавить вызов анимации парирования, если isParry == true
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
                isInvulnerable = false;
                rb.linearVelocity = Vector2.zero;
                DisableAllHitboxes();
                StartCoroutine(StunnedRoutine());
                playerParryCount++;
                lastPlayerParryTime = Time.time;
                return;
            }

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