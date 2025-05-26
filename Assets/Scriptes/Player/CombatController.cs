using System.Collections;
using UnityEngine;

public class CombatController : MonoBehaviour
{
    [Header("Combo Attack Settings")]
    public int maxCombo = 3;
    public float comboResetTime = 1.0f;
    public GameObject[] comboAttackHitboxes;
    public float[] attackWindupTimes = { 0.18f, 0.22f, 0.25f }; // �������������� ����� ��� ������� �����
    public float[] attackActiveTimes = { 0.3f, 0.32f, 0.35f };  // ������������ ����� �����

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

    [Header("Player State Flags (set externally)")]
    public bool isCrouching;
    public bool isSliding;
    public bool isDashing;
    public bool isLedgeClimbing;
    public bool isWallAttached;

    public bool IsAttacking => isAttacking;
    public bool IsParrying => isParrying;

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
    }

    void HandleAttackInput()
    {
        if (Input.GetKeyDown(KeyBindings.Attack))
        {
            // ������ ��������� � �������, �������, �����, �� ����
            if (isCrouching || isSliding || isDashing || isLedgeClimbing)
                return;

            // ���� ����� �� ����� � ����������� � �������� �����
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
        isAttackWindup = true;
        int comboIndex = 0;
        while (comboIndex < maxCombo)
        {
            // --- ����� ---
            float windup = attackWindupTimes.Length > comboIndex ? attackWindupTimes[comboIndex] : 0.2f;
            float windupTimer = 0f;
            while (windupTimer < windup)
            {
                // ���� ����� ����� dash, slide, crouch, wall hang � ��������� �����
                if (isCrouching || isSliding || isDashing || isLedgeClimbing || isWallAttached)
                {
                    isAttackWindup = false;
                    yield break;
                }
                windupTimer += Time.deltaTime;
                yield return null;
            }
            isAttackWindup = false;

            // --- ����� ---
            isAttacking = true;
            comboTimer = 0f;
            currentCombo = comboIndex + 1;
            ActivateAttackHitboxForCombo(currentCombo);

            float activeTime = attackActiveTimes.Length > comboIndex ? attackActiveTimes[comboIndex] : 0.3f;
            float attackTimer = 0f;
            while (attackTimer < activeTime)
            {
                // ���� ����� ����� dash, slide, crouch, wall hang � ��������� �����
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

            // �������� ����� ��� ��������� �����
            float timer = 0f;
            bool nextAttackQueued = false;
            while (timer < comboResetTime)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    nextAttackQueued = true;
                    break;
                }
                // ���������� �����, ���� ����� ����� ����������� ��������
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
            // ������ ���������� �� ����� ��������� �� ����
            if (isLedgeClimbing || isDashing)
                return;

            // ���� ����� �� ����� � ����������� � ��������
            if (isWallAttached)
            {
                DetachFromWall();
                // �������� ���� isWallAttached, ����� Movemant.cs ����, ��� ����� ����������
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
        parryCooldownTimer = 0f;
    }

    void HandleDashAttackDamageInput()
    {
        if (Input.GetKeyDown(KeyBindings.Dash)) // Dash � DashAttack � ���� ������
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