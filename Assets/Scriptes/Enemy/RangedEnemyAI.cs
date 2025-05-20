using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RangedEnemyAI : EnemyTeleportController
{
    private enum State
    {
        Pursuing, Aiming, Shooting, Reloading, Retreating,
        FakeTeleporting, MultiTeleporting, TeleportBehindPlayer, Dodging
    }

    private State currentState = State.Pursuing;

    [Header("Detection & Visual Contact")]
    public float detectionRadius = 12f;
    public float minAttackDistance = 5f;
    public float maxAttackDistance = 10f;
    public Transform designatedPoint;
    public LayerMask obstacleMask;

    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float retreatSpeed = 1.5f;
    public float retreatDistance = 3.5f;
    public float retreatCooldown = 3f;
    private float lastRetreatTime = -10f;

    [Header("Attack & Aiming Settings")]
    public float aimTime = 1f;
    public float fireRate = 0.5f;
    public int magazineSize = 5;
    public float reloadTime = 2f;
    public float lostSightReloadDelay = 1.5f;

    [Header("Projectiles")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 10f;

    [Header("Melee Attack Settings")]
    public float meleeAttackRange = 1.2f;
    public float meleeAttackCooldown = 2f;
    public float meleeAttackKnockback = 8f;
    public float meleeAttackStunDuration = 0.7f;
    public Collider2D meleeAttackHitbox;

    private float lastMeleeAttackTime = -10f;
    private Coroutine meleeAttackCoroutine;

    private int currentAmmo;
    private float lastShotTime;
    private float lastSightTime;
    private float lastDodgeTime = -10f;
    private float dodgeCooldown = 1.5f;

    // ������������
    private int failedShots = 0;
    private int maxFailedShotsBeforeRelocate = 3;

    void Start()
    {
        currentAmmo = magazineSize;
        currentState = State.Pursuing;
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }
    }

    void Update()
    {
        // 1. Уклонение от отражённого снаряда
        if (Time.time - lastDodgeTime > dodgeCooldown && DetectReflectedProjectile())
        {
            if (CanTeleport())
            {
                // 50% шанс сделать серию телепортов, 50% — фейк-манёвр
                if (Random.value < 0.5f)
                    StartCoroutine(MultiTeleportRoutine());
                else
                    StartCoroutine(FakeTeleportRoutine());
            }
            else
            {
                StartCoroutine(DodgeRoutine());
            }
            lastDodgeTime = Time.time;
            return;
        }

        // 2. Если игрок слишком близко — телепорт за спину
        if (Vector2.Distance(transform.position, player.position) < meleeAttackRange * 1.2f && CanTeleport())
        {
            StartCoroutine(TeleportBehindPlayerRoutine());
            return;
        }

        if (player == null || isTeleporting)
            return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (Vector2.Distance(transform.position, player.position) <= meleeAttackRange
            && Time.time - lastMeleeAttackTime > meleeAttackCooldown
            && currentState != State.Aiming
            && currentState != State.Shooting
            && currentState != State.Reloading
            && !isTeleporting)
        {
            if (meleeAttackCoroutine == null)
                meleeAttackCoroutine = StartCoroutine(MeleeAttackRoutine());
            return;
        }

        // ������� �� ��������� �������
        if (Time.time - lastDodgeTime > dodgeCooldown && DetectReflectedProjectile())
        {
            if (CanTeleport())
            {
                TryTeleportToSmartPosition();
            }
            else if (CanSafelyRetreat())
            {
                StartCoroutine(RetreatRoutine());
            }
            lastDodgeTime = Time.time;
            return;
        }

        // ������������ ��� ������ �������� �������
        if (CanTeleport() && ShouldTeleportToBetterPosition())
        {
            TryTeleportToSmartPosition();
            return;
        }

        // �����������, ���� ����� ������� ������
        if (distanceToPlayer < retreatDistance && Time.time - lastRetreatTime > retreatCooldown)
        {
            if (CanSafelyRetreat())
            {
                StartCoroutine(RetreatRoutine());
                lastRetreatTime = Time.time;
                return;
            }
        }

        // ����� ��������� �� ���������
        if (distanceToPlayer > detectionRadius)
        {
            currentState = State.Pursuing;
            return;
        }

        bool hasVisualContact = CheckVisualContact();
        if (hasVisualContact)
            lastSightTime = Time.time;

        switch (currentState)
        {
            case State.Pursuing:
                MoveToBestAttackPosition();
                if (hasVisualContact && IsInAttackRange())
                    StartCoroutine(BeginAiming());
                if (!hasVisualContact && Time.time - lastSightTime > lostSightReloadDelay && currentAmmo < magazineSize)
                {
                    currentState = State.Reloading;
                    StartCoroutine(Reload());
                }
                break;

            case State.Aiming:
                break;

            case State.Shooting:
                if (hasVisualContact && IsInAttackRange())
                {
                    if (Time.time - lastShotTime >= fireRate && currentAmmo > 0)
                    {
                        Shoot();
                        lastShotTime = Time.time;
                        currentAmmo--;
                        if (currentAmmo <= 0)
                        {
                            currentState = State.Reloading;
                            StartCoroutine(Reload());
                        }
                    }
                }
                else if (Time.time - lastSightTime > lostSightReloadDelay)
                {
                    currentState = State.Reloading;
                    StartCoroutine(Reload());
                }
                break;

            case State.Reloading:
                break;

            case State.Retreating:
                // ����������� ����������� � �������� RetreatRoutine
                break;
        }
    }

    // --- ���������� ���������������� � �������� ---

    bool IsInAttackRange()
    {
        float dist = Vector2.Distance(transform.position, player.position);
        return dist >= minAttackDistance && dist <= maxAttackDistance && CheckVisualContact();
    }

    void MoveToBestAttackPosition()
    {
        // ���� ��� � ������� ������� � �� ���������
        if (IsInAttackRange())
            return;

        // ��������� � ������� �� ����������� ��������� � ������ ����������
        Vector2 direction = (transform.position.x < player.position.x) ? Vector2.left : Vector2.right;
        float targetX = Mathf.Clamp(player.position.x + direction.x * Random.Range(minAttackDistance, maxAttackDistance),
                                   player.position.x - maxAttackDistance, player.position.x + maxAttackDistance);
        Vector2 targetPos = new Vector2(targetX, transform.position.y);

        // �������� �� ����������� � �����
        if (IsPositionSafe(targetPos))
        {
            Vector2 newPosition = Vector2.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
            transform.position = newPosition;
        }
    }

    bool ShouldTeleportToBetterPosition()
    {
        // ���� �� � ���� ����� ��� ����� ������� ������ � ���������������
        float dist = Vector2.Distance(transform.position, player.position);
        return !IsInAttackRange() || dist < retreatDistance * 0.8f;
    }

    void TryTeleportToSmartPosition()
    {
        Vector2? pos = FindSmartTeleportPosition();
        if (pos.HasValue)
        {
            StartCoroutine(TeleportToPositionRoutine(pos.Value));
        }
    }

    Vector2? FindSmartTeleportPosition()
    {
        // ���� ������� �� ���������� ������ ������ � ������� �����
        List<Vector2> candidates = new List<Vector2>();
        int samples = 12;
        for (int i = 0; i < samples; i++)
        {
            float angle = i * Mathf.PI * 2f / samples;
            Vector2 offset = new Vector2(Mathf.Cos(angle), 0) * Random.Range(minAttackDistance, maxAttackDistance);
            Vector2 candidate = (Vector2)player.position + offset;
            if (IsPositionSafe(candidate) && HasLineOfSight(candidate, player.position))
                candidates.Add(candidate);
        }
        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];
        return null;
    }

    IEnumerator TeleportToPositionRoutine(Vector2 pos)
    {
        isTeleporting = true;
        yield return new WaitForSeconds(teleportChargeTimeNear);
        transform.position = pos;
        lastTeleportTime = Time.time;
        isTeleporting = false;
    }

    bool IsPositionSafe(Vector2 pos)
    {
        // �������� �����
        RaycastHit2D groundHit = Physics2D.Raycast(pos, Vector2.down, 2f, groundMask);
        if (groundHit.collider == null)
            return false;
        // �������� �����������
        Collider2D wall = Physics2D.OverlapCircle(pos, 0.5f, groundMask | obstacleMask);
        if (wall != null)
            return false;
        // ����� �������� �������� �� ������ ������ � �������
        return true;
    }

    bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        Vector2 dir = (to - from).normalized;
        float dist = Vector2.Distance(from, to);
        RaycastHit2D hit = Physics2D.Raycast(from, dir, dist, obstacleMask);
        return hit.collider == null;
    }

    // --- ������� �� ��������� ������� ---

    bool DetectReflectedProjectile()
    {
        Projectile[] projectiles = FindObjectsOfType<Projectile>();
        foreach (var proj in projectiles)
        {
            if (proj.isReflected && !proj.isEnemyProjectile)
            {
                Vector2 toEnemy = (Vector2)transform.position - (Vector2)proj.transform.position;
                float angle = Vector2.Angle(proj.transform.right, toEnemy);
                float dist = toEnemy.magnitude;
                if (dist < 3f && angle < 45f)
                    return true;
            }
        }
        return false;
    }

    // --- ����������� ---

    bool CanSafelyRetreat()
    {
        float direction = Mathf.Sign(transform.position.x - player.position.x);
        Vector2 checkOrigin = (Vector2)transform.position;
        float step = 0.3f;
        int steps = Mathf.CeilToInt(retreatDistance / step);

        for (int i = 1; i <= steps; i++)
        {
            Vector2 pos = checkOrigin + Vector2.right * direction * (i * step);
            RaycastHit2D groundHit = Physics2D.Raycast(pos, Vector2.down, 1.2f, groundMask);
            if (groundHit.collider == null)
                return false;
            Collider2D wall = Physics2D.OverlapCircle(pos, 0.3f, groundMask | obstacleMask);
            if (wall != null)
                return false;
        }
        return true;
    }

    IEnumerator RetreatRoutine()
    {
        currentState = State.Retreating;
        float direction = Mathf.Sign(transform.position.x - player.position.x);
        float timer = 0f;
        float duration = retreatDistance / retreatSpeed;
        while (timer < duration)
        {
            timer += Time.deltaTime;
            transform.position += new Vector3(direction * retreatSpeed * Time.deltaTime, 0, 0);
            yield return null;
        }
        currentState = State.Pursuing;
    }

    // --- ��������������� ������ ---

    bool CheckVisualContact()
    {
        if (designatedPoint == null)
            designatedPoint = transform;
        Vector2 direction = (player.position - designatedPoint.position).normalized;
        float distance = Vector2.Distance(designatedPoint.position, player.position);
        RaycastHit2D hit = Physics2D.Raycast(designatedPoint.position, direction, distance, obstacleMask);
        return (hit.collider == null);
    }

    IEnumerator BeginAiming()
    {
        if (currentState == State.Aiming || currentState == State.Shooting)
            yield break;

        currentState = State.Aiming;
        yield return new WaitForSeconds(aimTime);
        if (CheckVisualContact() && IsInAttackRange())
            currentState = State.Shooting;
        else
            currentState = State.Pursuing;
    }

    void Shoot()
    {
        if (projectilePrefab != null && firePoint != null)
        {
            GameObject proj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            Projectile projectileScript = proj.GetComponent<Projectile>();
            if (projectileScript != null)
            {
                Vector2 shootDirection = (player.position - firePoint.position).normalized;
                projectileScript.Init(shootDirection, projectileSpeed);
            }
        }
    }

    IEnumerator Reload()
    {
        yield return new WaitForSeconds(reloadTime);
        currentAmmo = magazineSize;
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        currentState = (distanceToPlayer < retreatDistance) ? State.Retreating : State.Pursuing;
    }

    IEnumerator MeleeAttackRoutine()
    {
        currentState = State.Retreating; // �������� ��������� �������� � ��������
        lastMeleeAttackTime = Time.time;

        // �������� ������� �����
        if (meleeAttackHitbox != null)
            meleeAttackHitbox.enabled = true;

        // ������� �������� ��� �������� �����
        yield return new WaitForSeconds(0.15f);

        if (meleeAttackHitbox != null)
            meleeAttackHitbox.enabled = false;

        // ������� ����� ����� �����
        yield return new WaitForSeconds(0.2f);

        currentState = State.Pursuing;
        meleeAttackCoroutine = null;
    }
    void OnEnable()
    {
        if (meleeAttackHitbox != null)
        {
            var trigger = meleeAttackHitbox.GetComponent<AttackHitboxTrigger>();
            if (trigger != null)
                trigger.OnTriggerEnterEvent += OnMeleeAttackHitboxTriggerEnter2D;
        }
    }

    void OnDisable()
    {
        if (meleeAttackHitbox != null)
        {
            var trigger = meleeAttackHitbox.GetComponent<AttackHitboxTrigger>();
            if (trigger != null)
                trigger.OnTriggerEnterEvent -= OnMeleeAttackHitboxTriggerEnter2D;
        }
    }

    void OnMeleeAttackHitboxTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            // Наносим отталкивание
            Rigidbody2D playerRb = collision.GetComponent<Rigidbody2D>();
            Vector2 knockDir = (collision.transform.position - transform.position).normalized;
            if (playerRb != null)
            {
                playerRb.linearVelocity = new Vector2(knockDir.x * meleeAttackKnockback, playerRb.linearVelocity.y);
            }

            // Оглушение (исправлено: передаём все параметры)
            var stun = collision.GetComponent<PlayerStunController>();
            if (stun != null)
                stun.Stun(meleeAttackStunDuration, knockDir, meleeAttackKnockback);
        }
    }

    IEnumerator FakeTeleportRoutine()
    {
        currentState = State.FakeTeleporting;
        // Визуальный эффект иллюзии (можно добавить Instantiate(иллюзия))
        yield return new WaitForSeconds(0.2f);
        // На самом деле не телепортируемся, просто сбиваем игрока с толку
        currentState = State.Pursuing;
    }

    IEnumerator MultiTeleportRoutine(int count = 3)
    {
        currentState = State.MultiTeleporting;
        for (int i = 0; i < count; i++)
        {
            Vector2? pos = FindSmartTeleportPosition();
            if (pos.HasValue)
            {
                yield return TeleportToPositionRoutine(pos.Value);
                yield return new WaitForSeconds(0.1f);
            }
        }
        currentState = State.Pursuing;
    }

    IEnumerator TeleportBehindPlayerRoutine()
    {
        currentState = State.TeleportBehindPlayer;
        Vector2 dir = (player.right != Vector3.zero ? (Vector2)player.right : Vector2.right);
        Vector2 behind = (Vector2)player.position - dir * 1.5f;
        if (IsPositionSafe(behind))
        {
            yield return TeleportToPositionRoutine(behind);
        }
        currentState = State.Pursuing;
    }

    IEnumerator DodgeRoutine()
    {
        currentState = State.Dodging;
        float dodgeDir = Mathf.Sign(transform.position.x - player.position.x);
        Vector2 dodgeTarget = (Vector2)transform.position + Vector2.right * dodgeDir * 2f;
        if (IsPositionSafe(dodgeTarget))
        {
            float timer = 0f;
            float duration = 0.2f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                transform.position = Vector2.MoveTowards(transform.position, dodgeTarget, moveSpeed * 2 * Time.deltaTime);
                yield return null;
            }
        }
        currentState = State.Pursuing;
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, retreatDistance);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, minAttackDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxAttackDistance);
    }
}