using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MeleeEnemyAI : EnemyTeleportController
{
    private enum State
    {
        Pursuing, Charging, Dashing, Recovery, Stunned, MeleeComboAttacking,
        EvasionDashing, Retreating, Jumping, StepOver, Feinting
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
    public GameObject[] comboAttackHitboxes;

    [Header("Dash Attack Hitbox")]
    public GameObject dashAttackHitbox;

    [Header("Parry Projectile Hitbox")]
    public GameObject parryProjectileHitbox;

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

    [Header("AI Variability")]
    public float feintChance = 0.25f;
    public float dashBehindChance = 0.2f;
    public float unpredictableMoveChance = 0.15f;
    public float attackRhythmVariance = 0.2f;
    private float lastAttackTime = -10f;

    public ActionBasedPathfinder actionPathfinder;
    private List<EnemyAction> currentActions;
    private int currentActionIndex = 0;
    private float pathRecalcTimer = 0f;
    private float pathRecalcInterval = 1.0f;

    private bool isDead = false;
    private Coroutine parryProjectileCoroutine;
    public bool isInvulnerable = false;

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

    private bool facingRight = true;

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
        SetInvulnerable(true);
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

        // --- Вариативное и непредсказуемое поведение ---
        if (currentState == State.Pursuing)
        {
            // Слишком близко — шанс уклониться за спину или отступить
            if (distanceToPlayer < meleeAttackRange * 0.7f)
            {
                if (Random.value < dashBehindChance && Time.time - lastEvasionDashTime > evasionDashCooldown)
                {
                    StartCoroutine(DashBehindPlayerRoutine());
                    lastEvasionDashTime = Time.time;
                    return;
                }
                else if (Time.time - lastRetreatTime > retreatCooldown)
                {
                    StartCoroutine(RetreatDashRoutine());
                    lastRetreatTime = Time.time;
                    return;
                }
            }
            // В зоне атаки — шанс на ложную атаку или вариативный ритм
            else if (distanceToPlayer <= meleeAttackRange)
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
            // Иногда — неожиданный манёвр (например, прыжок вбок)
            else if (Random.value < unpredictableMoveChance)
            {
                StartCoroutine(UnpredictableMoveRoutine());
                return;
            }
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

        // --- Умная телепортация ---
        if (IsEmergencyTeleportSituation())
        {
            if (!groupTeleportActive && CanTeleport())
            {
                groupTeleportActive = true;
                TrySmartTeleport();
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
                TrySmartTeleport();
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
                TrySmartTeleport();
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
                TrySmartTeleport();
                StartCoroutine(ReleaseGroupTeleportFlag());
                failedPathAttempts = 0;
                return;
            }
        }
    }

    void FixedUpdate()
    {
        if (currentActions != null && currentActionIndex < currentActions.Count && currentState == State.Pursuing)
        {
            var action = currentActions[currentActionIndex];
            if (action.Type == EnemyActionType.Walk)
            {
                float moveDir = Mathf.Sign(action.Direction.x);
                if (currentState != State.Retreating)
                    Flip(moveDir);

                Vector2 targetPos = (Vector2)transform.position + action.Direction * action.Force * action.Duration;
                rb.linearVelocity = new Vector2(moveDir * moveSpeed, rb.linearVelocity.y);

                if (Vector2.Distance(transform.position, targetPos) < 0.1f)
                    currentActionIndex++;
            }
            else if (action.Type == EnemyActionType.AirControl)
            {
                Vector2 targetPos = transform.position + (Vector3)action.Direction * action.Force * action.Duration;
                rb.position = Vector2.MoveTowards(rb.position, targetPos, moveSpeed * Time.fixedDeltaTime);
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

    // Вариативные манёвры
    IEnumerator FeintAttackRoutine()
    {
        if (currentState != State.Pursuing)
            yield break;
        currentState = State.Feinting;
        SetInvulnerable(false);

        // Короткая ложная атака (анимация, звук, но без урона)
        yield return new WaitForSeconds(0.15f + Random.Range(-0.05f, 0.1f));
        // Быстрое уклонение после ложной атаки
        if (Random.value < 0.7f)
            StartCoroutine(DashBehindPlayerRoutine());
        else
            StartCoroutine(EvasionDashRoutine((transform.position - player.position).normalized));
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
        lastEvasionDashTime = Time.time;
    }

    IEnumerator UnpredictableMoveRoutine()
    {
        currentState = State.EvasionDashing;
        SetInvulnerable(true);
        float timer = 0f;
        Vector2 dir = Random.value < 0.5f ? Vector2.right : Vector2.left;
        Flip(dir.x);
        if (Random.value < 0.5f)
            rb.linearVelocity = dir * evasionDashSpeed;
        else
            rb.linearVelocity = new Vector2(dir.x * moveSpeed, jumpForce * 0.7f);
        while (timer < evasionDashDuration * 0.7f)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
        SetInvulnerable(false);
        currentState = State.Pursuing;
    }

    // --- Умная телепортация ---
    private void TrySmartTeleport()
    {
        Vector2? pos = FindSmartTeleportPosition();
        if (pos.HasValue)
        {
            StartCoroutine(TeleportToPositionRoutine(pos.Value));
        }
        else
        {
            TryTeleport(); // fallback
        }
    }

    private Vector2? FindSmartTeleportPosition()
    {
        float[] distances = { retreatDashDistance, meleeAttackRange, meleeAttackRange * 1.5f };
        foreach (float dist in distances)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 offset = Vector2.right * dist * side;
                Vector2 candidate = (Vector2)player.position + offset;
                RaycastHit2D groundHit = Physics2D.Raycast(candidate, Vector2.down, 2f, groundLayer);
                if (groundHit.collider != null)
                {
                    candidate.y = groundHit.point.y + 0.5f;
                    if (IsPositionSafe(candidate))
                        return candidate;
                }
            }
        }
        return null;
    }

    private IEnumerator TeleportToPositionRoutine(Vector2 pos)
    {
        isTeleporting = true;
        yield return new WaitForSeconds(teleportChargeTimeNear);
        transform.position = pos;
        lastTeleportTime = Time.time;
        isTeleporting = false;
    }

    private bool IsPositionSafe(Vector2 pos)
    {
        RaycastHit2D groundHit = Physics2D.Raycast(pos, Vector2.down, 2f, groundLayer);
        if (groundHit.collider == null)
            return false;
        Collider2D wall = Physics2D.OverlapCircle(pos, 0.5f, groundLayer);
        if (wall != null)
            return false;
        return true;
    }

    // Тип маршрута
    private enum RouteType { Normal, StepOver, Jump, JumpAndDash, Impossible }

    private RouteType AnalyzeRouteToPlayer()
    {
        Vector2 dir = (player.position - transform.position).normalized;
        float dist = Vector2.Distance(transform.position, player.position);

        if (IsStepOverPossible(dir))
            return RouteType.StepOver;
        if (IsJumpPossible(dir))
            return RouteType.Jump;
        if (IsJumpAndDashPossible(dir))
            return RouteType.JumpAndDash;
        if (IsPathClear(dir, dist))
            return RouteType.Normal;
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
            return true;
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

    private float GetTeleportBaseTime(float distance)
    {
        return 0.5f;
    }

    private float GetTeleportPenalty(float distance)
    {
        if (distance >= teleportCompareDistance)
            return 0f;
        float t = 1f - Mathf.Clamp01(distance / teleportCompareDistance);
        return Mathf.Lerp(teleportMinPenalty, teleportMaxPenalty, t);
    }

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

    private IEnumerator JumpRoutine()
    {
        currentState = State.Jumping;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        yield return new WaitForSeconds(0.2f);
        currentState = State.Pursuing;
    }

    private IEnumerator JumpAndDashRoutine()
    {
        currentState = State.Jumping;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        yield return new WaitForSeconds(0.15f);
        StartCoroutine(EvasionDashRoutine(Vector2.right * Mathf.Sign(player.position.x - transform.position.x)));
        yield return new WaitForSeconds(evasionDashDuration);
        currentState = State.Pursuing;
    }

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
        SetInvulnerable(false);

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
                // Вариативный шанс прервать атаку и уклониться
                if (Random.value < 0.1f && Time.time - lastEvasionDashTime > evasionDashCooldown)
                {
                    StartCoroutine(DashBehindPlayerRoutine());
                    lastEvasionDashTime = Time.time;
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

    IEnumerator EvasionDashRoutine(Vector2 direction)
    {
        currentState = State.EvasionDashing;
        SetInvulnerable(true);
        float timer = 0f;
        Flip(direction.x);
        rb.linearVelocity = direction.normalized * evasionDashSpeed;
        while (timer < evasionDashDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;
        SetInvulnerable(false);
        currentState = State.Pursuing;
        lastEvasionDashTime = Time.time;
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

    IEnumerator ChargeRoutine()
    {
        if (!CanDash((player.position - transform.position).normalized, dashSpeed * dashDuration))
        {
            currentState = State.Pursuing;
            SetInvulnerable(true);
            if (parryProjectileHitbox != null)
                parryProjectileHitbox.SetActive(false);
            yield break;
        }

        if (currentState != State.Pursuing)
            yield break;
        currentState = State.Charging;
        SetInvulnerable(false);

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
                SetInvulnerable(true);
                if (parryProjectileHitbox != null)
                    parryProjectileHitbox.SetActive(false);
                yield break;
            }
            var parryHitbox = player.GetComponentInChildren<ParryHitbox>();
            if (parryHitbox != null && parryHitbox.IsParrying)
            {
                currentState = State.Pursuing;
                SetInvulnerable(true);
                if (parryProjectileHitbox != null)
                    parryProjectileHitbox.SetActive(false);
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
        SetInvulnerable(true);

        if (dashAttackHitbox != null)
            dashAttackHitbox.SetActive(true);

        Vector2 dashDirection = ((Vector2)player.position - rb.position).normalized;
        Flip(dashDirection.x);
        float timer = 0f;
        rb.linearVelocity = dashDirection * dashSpeed;
        while (timer < dashDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        rb.linearVelocity = Vector2.zero;

        if (dashAttackHitbox != null)
            dashAttackHitbox.SetActive(false);

        StartCoroutine(RecoveryRoutine());
    }

    IEnumerator RecoveryRoutine()
    {
        currentState = State.Recovery;
        SetInvulnerable(false);
        yield return new WaitForSeconds(recoveryTime);
        currentState = State.Pursuing;
        SetInvulnerable(true);
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
        Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead) return;

        // Неуязвимость: парируем только если не в атаке/рывке/оглушении/уклонении/отступлении
        if (isInvulnerable && !IsDashingOrAttacking() && currentState != State.EvasionDashing && currentState != State.Retreating)
        {
            if (collision.CompareTag("PlayerAttack") || collision.CompareTag("DashAttack"))
            {
                TryParry(collision.transform.position);
                return;
            }
            var proj = collision.GetComponent<Projectile>();
            if (proj != null && proj.isReflected)
            {
                TryParry(collision.transform.position);
                return;
            }
        }
        // Во время рывка/уклонения/отступления — просто игнорируем урон, не парируем
        else if (isInvulnerable)
        {
            return;
        }
        // В остальных случаях — получаем урон
        else
        {
            if (collision.CompareTag("PlayerAttack") || collision.CompareTag("DashAttack"))
            {
                TakeDamage();
                return;
            }
            var proj = collision.GetComponent<Projectile>();
            if (proj != null && proj.isReflected)
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
                playerParryCount++;
                lastPlayerParryTime = Time.time;
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

    void SetInvulnerable(bool value)
    {
        isInvulnerable = value;
        gameObject.tag = value ? "Invulnerable" : "Enemy";
    }

    private bool IsDashingOrAttacking()
    {
        return currentState == State.Dashing || currentState == State.MeleeComboAttacking;
    }
}