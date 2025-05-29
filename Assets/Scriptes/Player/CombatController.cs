using System.Collections;
using UnityEngine;

public class CombatController : MonoBehaviour
{
    [Header("Combo Attack Settings")]
    public int maxCombo = 3;
    public float comboResetTime = 1.0f;
    public GameObject[] comboAttackHitboxes;
    public float[] attackWindupTimes = { 0.18f, 0.22f, 0.25f }; // индивидуальный замах для каждого удара
    public float[] attackActiveTimes = { 0.3f, 0.32f, 0.35f };  // длительность самой атаки

    private int currentCombo = 0;
    private float comboTimer = 0f;
    private bool isAttacking = false;
    private bool isAttackWindup = false;
    private Coroutine attackCoroutine;

    [Header("Parry Settings")]
    public float parryDuration = 0.5f;
    public float parryCooldown = 1.0f;
    public GameObject parryHitbox;

    private bool isParrying = false;
    private float parryCooldownTimer = 0f;

    [Header("Dash Attack Damage Settings")]
    public GameObject dashAttackHitbox;
    public float dashDamageDuration = 0.3f;
    private bool isDashAttacking = false;

    [Header("VFX")]
    public GameObject[] comboAttackEffectPrefabs; // по одному на каждый удар комбо
    public Vector3[] comboAttackEffectOffsets;    // смещение для каждого удара
    public Vector3[] comboAttackEffectScales;     // масштаб для каждого удара

    public GameObject parrySuccessEffectPrefab;
    public Vector3 parrySuccessEffectOffset;
    public Vector3 parrySuccessEffectScale;

    public GameObject[] dashEffectPrefabs;        // например, dashEffectPrefabs[0] — обычный, [1] — особый
    public Vector3[] dashEffectOffsets;
    public Vector3[] dashEffectScales;

    [Header("Parry VFX")]
    public GameObject parryStartEffectPrefab;
    public Vector3 parryStartEffectOffset;
    public Vector3 parryStartEffectScale = Vector3.one;

    [Header("Player State Flags (set externally)")]
    public bool isCrouching;
    public bool isSliding;
    public bool isDashing;
    public bool isLedgeClimbing;
    public bool isWallAttached;

    private bool facingRight = true;

    public bool IsAttacking => isAttacking;
    public bool IsParrying => isParrying;
    public bool IsAttackWindup => isAttackWindup;

    private CollisionController collisionController;

    void Start()
    {
        if (comboAttackHitboxes != null)
        {
            foreach (var hitbox in comboAttackHitboxes)
                if (hitbox != null) hitbox.SetActive(false);
        }
        if (parryHitbox != null)
            parryHitbox.SetActive(false);
        if (dashAttackHitbox != null)
            dashAttackHitbox.SetActive(false);

        collisionController = GetComponent<CollisionController>();
    }

    void Update()
    {
        HandleAttackInput();
        HandleParryInput();
        HandleDashAttackDamageInput();

        if (isAttacking)
        {
            comboTimer += Time.deltaTime;
            if (comboTimer > comboResetTime)
                ResetCombo();
        }

        if (parryCooldownTimer > 0f)
            parryCooldownTimer -= Time.deltaTime;

        UpdateFacing();
    }

    private void UpdateFacing()
    {
        float h = Input.GetAxisRaw("Horizontal");
        if (h > 0.01f && !facingRight)
            facingRight = true;
        else if (h < -0.01f && facingRight)
            facingRight = false;
    }

    void HandleAttackInput()
    {
        if (Input.GetKeyDown(KeyBindings.Attack))
        {
            // Нельзя атаковать в присяде, подкате, рывке, на краю
            if (isCrouching || isSliding || isDashing || isLedgeClimbing)
                return;

            // Если висим на стене — отцепляемся и начинаем замах
            if (isWallAttached)
            {
                DetachFromWall();
            }

            if (!isAttacking && !isParrying && !isDashAttacking && !isAttackWindup)
            {
                attackCoroutine = StartCoroutine(AttackComboWithWindup());
            }
        }
    }

    IEnumerator AttackComboWithWindup()
    {
        // Гарантируем отлипание от стены при начале замаха
        if (collisionController != null && collisionController.IsTouchingWall)
            collisionController.DetachFromWall();

        isAttackWindup = true;
        int comboIndex = 0;
        while (comboIndex < maxCombo)
        {
            // --- Замах ---
            float windup = attackWindupTimes.Length > comboIndex ? attackWindupTimes[comboIndex] : 0.2f;
            float windupTimer = 0f;
            while (windupTimer < windup)
            {
                // Если игрок начал dash, slide, crouch, wall hang — прерываем замах
                if (isCrouching || isSliding || isDashing || isLedgeClimbing || isWallAttached)
                {
                    isAttackWindup = false;
                    yield break;
                }
                windupTimer += Time.deltaTime;
                yield return null;
            }
            isAttackWindup = false;

            // --- Атака ---
            isAttacking = true;
            comboTimer = 0f;
            currentCombo = comboIndex + 1;
            ActivateAttackHitboxForCombo(currentCombo);

            // Воспроизвести эффект удара
            if (comboAttackEffectPrefabs != null && comboIndex < comboAttackEffectPrefabs.Length && comboAttackEffectPrefabs[comboIndex] != null)
            {
                Vector3 offset = Vector3.zero;
                if (comboAttackEffectOffsets != null && comboIndex < comboAttackEffectOffsets.Length)
                    offset = comboAttackEffectOffsets[comboIndex];
                if (!facingRight)
                    offset.x = -offset.x;
                Vector3 pos = transform.position + offset;
                var fx = Instantiate(comboAttackEffectPrefabs[comboIndex], pos, Quaternion.identity, transform);
                if (comboAttackEffectScales != null && comboIndex < comboAttackEffectScales.Length)
                    fx.transform.localScale = comboAttackEffectScales[comboIndex];
            }

            float activeTime = attackActiveTimes.Length > comboIndex ? attackActiveTimes[comboIndex] : 0.3f;
            float attackTimer = 0f;
            while (attackTimer < activeTime)
            {
                // Если игрок начал dash, slide, crouch, wall hang — прерываем атаку
                if (isCrouching || isSliding || isDashing || isLedgeClimbing || isWallAttached)
                {
                    DeactivateAttackHitboxForCombo(currentCombo);
                    ResetCombo();
                    yield break;
                }
                attackTimer += Time.deltaTime;
                yield return null;
            }
            DeactivateAttackHitboxForCombo(currentCombo);

            // Ожидание ввода для следующей атаки
            float timer = 0f;
            bool nextAttackQueued = false;
            while (timer < comboResetTime)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    nextAttackQueued = true;
                    break;
                }
                // Прерывание комбо, если игрок начал запрещённое действие
                if (isCrouching || isSliding || isDashing || isLedgeClimbing || isWallAttached)
                {
                    ResetCombo();
                    yield break;
                }
                timer += Time.deltaTime;
                yield return null;
            }
            if (!nextAttackQueued)
                break;
            comboIndex++;
        }
        ResetCombo();
    }

    void ActivateAttackHitboxForCombo(int comboIndex)
    {
        if (comboAttackHitboxes != null && comboIndex - 1 >= 0 && comboIndex - 1 < comboAttackHitboxes.Length && comboAttackHitboxes[comboIndex - 1] != null)
            comboAttackHitboxes[comboIndex - 1].SetActive(true);
    }

    void DeactivateAttackHitboxForCombo(int comboIndex)
    {
        if (comboAttackHitboxes != null && comboIndex - 1 >= 0 && comboIndex - 1 < comboAttackHitboxes.Length && comboAttackHitboxes[comboIndex - 1] != null)
            comboAttackHitboxes[comboIndex - 1].SetActive(false);
    }

    void ResetCombo()
    {
        isAttacking = false;
        isAttackWindup = false;
        currentCombo = 0;
        comboTimer = 0f;
        if (comboAttackHitboxes != null)
            foreach (var hitbox in comboAttackHitboxes)
                if (hitbox != null) hitbox.SetActive(false);
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);
    }

    void HandleParryInput()
    {
        if (Input.GetKeyDown(KeyBindings.Parry))
        {
            // Нельзя парировать во время взбирания на край
            if (isLedgeClimbing || isDashing)
                return;

            // Если висим на стене — отцепляемся и парируем
            if (isWallAttached)
            {
                DetachFromWall();
                // Сбросить флаг isWallAttached, чтобы Movemant.cs знал, что нужно отцепиться
            }

            if (!isParrying && !isAttacking && !isDashAttacking && parryCooldownTimer <= 0f)
                StartCoroutine(PerformParry());
        }
    }

    IEnumerator PerformParry()
    {
        isParrying = true;
        if (parryHitbox != null)
            parryHitbox.SetActive(true);

        if (parryStartEffectPrefab != null)
        {
            Vector3 offset = parryStartEffectOffset;
            if (!facingRight) offset.x = -offset.x;
            var fx = Instantiate(parryStartEffectPrefab, transform.position + offset, Quaternion.identity, transform);
            fx.transform.localScale = parryStartEffectScale;
        }

        yield return new WaitForSeconds(parryDuration);

        if (parryHitbox != null)
            parryHitbox.SetActive(false);
        isParrying = false;
        parryCooldownTimer = parryCooldown;
    }

    void StopParryDueToDash()
    {
        if (isParrying)
        {
            if (parryHitbox != null)
                parryHitbox.SetActive(false);
            isParrying = false;
            parryCooldownTimer = parryCooldown;
        }
    }

    public void OnSuccessfulParry()
    {
        if (isParrying)
        {
            if (parryHitbox != null)
                parryHitbox.SetActive(false);
            isParrying = false;
        }
        if (parrySuccessEffectPrefab != null)
        {
            Vector3 offset = parrySuccessEffectOffset;
            if (!facingRight)
                offset.x = -offset.x;
            Vector3 pos = transform.position + offset;
            var fx = Instantiate(parrySuccessEffectPrefab, pos, Quaternion.identity, transform);
            fx.transform.localScale = parrySuccessEffectScale;
        }
        parryCooldownTimer = 0f;
    }

    void HandleDashAttackDamageInput()
    {
        if (Input.GetKeyDown(KeyBindings.Dash)) // Dash и DashAttack — одна кнопка
        {
            if (isParrying)
            {
                StopParryDueToDash();
                return;
            }

            if (!isAttacking && !isParrying && !isDashAttacking)
                StartCoroutine(PerformDashAttackDamage());
        }
    }

    IEnumerator PerformDashAttackDamage()
    {
        isDashAttacking = true;
        // Активировать все эффекты рывка
        if (dashEffectPrefabs != null)
        {
            for (int i = 0; i < dashEffectPrefabs.Length; i++)
            {
                if (dashEffectPrefabs[i] != null)
                {
                    Vector3 offset = Vector3.zero;
                    if (dashEffectOffsets != null && i < dashEffectOffsets.Length)
                        offset = dashEffectOffsets[i];
                    if (!facingRight)
                        offset.x = -offset.x;
                    Vector3 pos = transform.position + offset;
                    var fx = Instantiate(dashEffectPrefabs[i], pos, Quaternion.identity, transform);
                    if (dashEffectScales != null && i < dashEffectScales.Length)
                        fx.transform.localScale = dashEffectScales[i];
                }
            }
        }

        if (dashAttackHitbox != null)
            dashAttackHitbox.SetActive(true);
        yield return new WaitForSeconds(dashDamageDuration);
        if (dashAttackHitbox != null)
            dashAttackHitbox.SetActive(false);
        isDashAttacking = false;
    }

    void DetachFromWall()
    {
        if (isWallAttached)
            isWallAttached = false;
    }
}