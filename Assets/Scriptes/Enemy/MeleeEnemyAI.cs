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

    [Header("AI Variability")]
    public float feintChance = 0.25f;
    public float dashBehindChance = 0.2f;
    public float unpredictableMoveChance = 0.15f;
    public float attackRhythmVariance = 0.2f;
    private float lastAttackTime = -10f;

    private bool isDead = false;
    private Coroutine parryProjectileCoroutine;
    public bool isInvulnerable = false;

    private static bool groupTeleportActive = false;
    public bool IsDead => isDead;
    public string CurrentState => currentState.ToString();

    private bool facingRight = true;

    void Start()
    {
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
    }

    void Update()
    {
        if (player == null || isDead || isTeleporting)
            return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        // --- Вариативное и непредсказуемое поведение ---
        if (currentState == State.Pursuing)
        {
            // Слишком близко — шанс уклониться за спину или отступить
            if (distanceToPlayer < meleeAttackRange * 0.7f)
            {
                if (Random.value < dashBehindChance)
                {
                    StartCoroutine(DashBehindPlayerRoutine());
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

        // --- Всегда предпочитает телепортироваться к игроку ---
        if (!groupTeleportActive && CanTeleport())
        {
            groupTeleportActive = true;
            StartCoroutine(TeleportToPlayerRoutine());
            StartCoroutine(ReleaseGroupTeleportFlag());
            return;
        }
    }

    // --- Вариативные манёвры ---
    IEnumerator FeintAttackRoutine()
    {
        if (currentState != State.Pursuing)
            yield break;
        currentState = State.Feinting;
        SetInvulnerable(false);

        yield return new WaitForSeconds(0.15f + Random.Range(-0.05f, 0.1f));
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
        rb.velocity = dashDir * dashSpeed;
        while (timer < dashDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        rb.velocity = Vector2.zero;
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
        rb.velocity = dir * dashSpeed;
        while (timer < dashDuration * 0.7f)
        {
            timer += Time.deltaTime;
            yield return null;
        }
        rb.velocity = Vector2.zero;
        SetInvulnerable(false);
        currentState = State.Pursuing;
    }

    // --- Телепорт к игроку (упрощённо) ---
    private IEnumerator TeleportToPlayerRoutine()
    {
        isTeleporting = true;
        yield return new WaitForSeconds(1.0f); // имитация задержки
        transform.position = player.position + Vector3.right * Random.Range(-1.5f, 1.5f);
        lastTeleportTime = Time.time;
        isTeleporting = false;
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
        BlockAttack(attackerPosition);

        if (currentState == State.MeleeComboAttacking ||
            currentState == State.Dashing ||
            currentState == State.Charging)
        {
            StopAllCoroutines();
            currentState = State.Stunned;
            SetInvulnerable(false);
            rb.velocity = Vector2.zero;
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
        Die();
    }

    private void Die()
    {
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isDead) return;

        if (isInvulnerable && !IsDashingOrAttacking() && currentState != State.EvasionDashing && currentState != State.Retreating)
        {
            if (collision.CompareTag("PlayerAttack") || collision.CompareTag("DashAttack"))
            {
                TryParry(collision.transform.position);
                return;
            }
        }
        else if (isInvulnerable)
        {
            return;
        }
        else
        {
            if (collision.CompareTag("PlayerAttack") || collision.CompareTag("DashAttack"))
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
                rb.velocity = new Vector2(-Mathf.Sign(transform.position.x - player.position.x) * 5f, 3f);
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
        // Не используем gameObject.tag, чтобы не было ошибок с несуществующими тегами
    }

    private bool IsDashingOrAttacking()
    {
        return currentState == State.Dashing || currentState == State.MeleeComboAttacking;
    }
}