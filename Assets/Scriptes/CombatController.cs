using System.Collections;
using UnityEngine;

public class CombatController : MonoBehaviour
{
    // ==========================
    //  ��������� ����������� ����� (���)
    // ==========================
    [Header("Combo Attack Settings")]
    [Tooltip("������������ ����� ������ � �����")]
    public int maxCombo = 3;
    [Tooltip("�����, ����� �������� ����� ������������")]
    public float comboResetTime = 1.0f;
    [Tooltip("������ ��������-��������� ��� ����������� �����. ������ ������� ������������� ���������� ����� � �����.")]
    public GameObject[] comboAttackHitboxes;

    private int currentCombo = 0;
    private float comboTimer = 0f;
    private bool isAttacking = false;

    // ==========================
    //  ��������� ����������� (���)
    // ==========================
    [Header("Parry Settings")]
    [Tooltip("������������ ������ �����������")]
    public float parryDuration = 0.5f;
    [Tooltip("������� ����� �����������")]
    public float parryCooldown = 1.0f;
    [Tooltip("������-������� ��� �����������")]
    public GameObject parryHitbox;

    private bool isParrying = false;
    private float parryCooldownTimer = 0f;

    // ==========================
    //  ��������� ����� �� dash-�����
    // ==========================
    [Header("Dash Attack Damage Settings")]
    [Tooltip("������-�������, ��������� ���� ��� �����")]
    public GameObject dashAttackHitbox;
    [Tooltip("������������ ��������� hitbox'� ��� dash-�����")]
    public float dashDamageDuration = 0.3f;
    private bool isDashAttacking = false;

    // ==========================
    //  ����� �������� ��������� ������ (������������� ����� ��� ������ ��������)
    // ==========================
    [Header("Player State Flags (set externally)")]
    [Tooltip("�������� � ��������� �������?")]
    public bool isCrouching;
    [Tooltip("�������� � ��������� �������?")]
    public bool isSliding;
    [Tooltip("�������� � ��������� �����? (�� ������������� �������)")]
    public bool isDashing;
    [Tooltip("�������� � ��������� ��������� �� ���� �����?")]
    public bool isLedgeClimbing;
    [Tooltip("�������� ��������� � ����� (�����, ��������, ��������������)")]
    public bool isWallAttached;

    void Start()
    {
        // ��������� ��� hitbox'� ��� ������
        if (comboAttackHitboxes != null)
        {
            foreach (var hitbox in comboAttackHitboxes)
            {
                if (hitbox != null)
                    hitbox.SetActive(false);
            }
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

        // ��������� ������ ����� (���� ����� ��������)
        if (isAttacking)
        {
            comboTimer += Time.deltaTime;
            if (comboTimer > comboResetTime)
            {
                ResetCombo();
            }
        }

        // ��������� ������� �����������
        if (parryCooldownTimer > 0f)
        {
            parryCooldownTimer -= Time.deltaTime;
        }
    }

    // ==========================
    //  ���� ��� ����������� ����� (���)
    // ==========================
    void HandleAttackInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // ���� �������� ��������� � ����������� ���������� � ����� �� ����� ���� ���������
            if (isCrouching || isSliding || isDashing || isLedgeClimbing)
            {
                Debug.Log("Attack input ignored due to forbidden state (crouching/sliding/dashing/ledge climbing).");
                return;
            }

            // ���� �������� ��������� � ����� � ������������� �����������
            if (isWallAttached)
            {
                DetachFromWall();
            }

            // ���� �������� ��� �� ����� ������ ������/������������/������ � ��������� �����
            if (!isAttacking && !isParrying && !isDashAttacking)
            {
                StartCoroutine(PerformAttackCombo());
            }
        }
    }

    IEnumerator PerformAttackCombo()
    {
        isAttacking = true;
        comboTimer = 0f;
        currentCombo = 1;
        Debug.Log("Combo Attack 1 initiated");

        // ���� ��� ������� ����� �������� ��������� hitbox � ���������� ���
        ActivateAttackHitboxForCombo(currentCombo);
        yield return new WaitForSeconds(0.3f);
        DeactivateAttackHitboxForCombo(currentCombo);

        // ������� ���� ��� ���������� �����
        float timer = 0f;
        bool nextAttackQueued = false;
        while (timer < comboResetTime)
        {
            if (Input.GetMouseButtonDown(0))
            {
                nextAttackQueued = true;
                break;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        while (nextAttackQueued && currentCombo < maxCombo)
        {
            currentCombo++;
            comboTimer = 0f;
            Debug.Log("Combo Attack " + currentCombo + " initiated");
            ActivateAttackHitboxForCombo(currentCombo);
            yield return new WaitForSeconds(0.3f);
            DeactivateAttackHitboxForCombo(currentCombo);

            nextAttackQueued = false;
            timer = 0f;
            while (timer < comboResetTime)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    nextAttackQueued = true;
                    break;
                }
                timer += Time.deltaTime;
                yield return null;
            }
        }

        ResetCombo();
    }

    void ActivateAttackHitboxForCombo(int comboIndex)
    {
        if (comboAttackHitboxes != null && comboIndex - 1 < comboAttackHitboxes.Length && comboAttackHitboxes[comboIndex - 1] != null)
        {
            comboAttackHitboxes[comboIndex - 1].SetActive(true);
        }
    }

    void DeactivateAttackHitboxForCombo(int comboIndex)
    {
        if (comboAttackHitboxes != null && comboIndex - 1 < comboAttackHitboxes.Length && comboAttackHitboxes[comboIndex - 1] != null)
        {
            comboAttackHitboxes[comboIndex - 1].SetActive(false);
        }
    }

    void ResetCombo()
    {
        isAttacking = false;
        currentCombo = 0;
        comboTimer = 0f;
    }

    // ==========================
    //  ���� ��� ����������� (���)
    // ==========================
    void HandleParryInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            // ���������� ������, ���� �������� ��������� ����� ��� ���������
            if (isDashing || isLedgeClimbing)
            {
                Debug.Log("Parry input ignored due to dash/ledge climbing state.");
                return;
            }

            // ���� �������� ��������� � ����� � �������������
            if (isWallAttached)
            {
                DetachFromWall();
            }

            if (!isParrying && !isAttacking && !isDashAttacking && parryCooldownTimer <= 0f)
            {
                StartCoroutine(PerformParry());
            }
        }
    }

    IEnumerator PerformParry()
    {
        isParrying = true;
        Debug.Log("Parry started");
        if (parryHitbox != null)
            parryHitbox.SetActive(true);

        yield return new WaitForSeconds(parryDuration);

        if (parryHitbox != null)
            parryHitbox.SetActive(false);
        isParrying = false;
        parryCooldownTimer = parryCooldown;
        Debug.Log("Parry ended, cooldown started");
    }

    // ���� �� ����� ����������� ���������� ������� �����, ��������� �����������
    void StopParryDueToDash()
    {
        if (isParrying)
        {
            if (parryHitbox != null)
                parryHitbox.SetActive(false);
            isParrying = false;
            parryCooldownTimer = parryCooldown;
            Debug.Log("Parry interrupted due to dash attempt, cooldown started.");
        }
    }

    // �����, ������� ����� ���� ������ ����� (��������, ��� ������� ��������� �����) ��� ������ ��������
    public void OnSuccessfulParry()
    {
        // ��������� ����������� ���������� � �������� �������
        if (isParrying)
        {
            if (parryHitbox != null)
                parryHitbox.SetActive(false);
            isParrying = false;
        }
        parryCooldownTimer = 0f;
        Debug.Log("Effective parry! Cooldown reset.");
    }

    // ==========================
    //  ���� ��� dash attack damage (������� E)
    // ==========================
    void HandleDashAttackDamageInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // ���� �� ����� ����������� ��������� dash � ��������� ����������� � �� ��������� dash
            if (isParrying)
            {
                StopParryDueToDash();
                return;
            }

            if (!isAttacking && !isParrying && !isDashAttacking)
            {
                StartCoroutine(PerformDashAttackDamage());
            }
        }
    }

    IEnumerator PerformDashAttackDamage()
    {
        isDashAttacking = true;
        Debug.Log("Dash Attack Damage started");
        if (dashAttackHitbox != null)
            dashAttackHitbox.SetActive(true);
        yield return new WaitForSeconds(dashDamageDuration);
        if (dashAttackHitbox != null)
            dashAttackHitbox.SetActive(false);
        Debug.Log("Dash Attack Damage ended");
        isDashAttacking = false;
    }

    // ==========================
    //  ����� ������������ �� ����� (������� � ��������� �������)
    // ==========================
    void DetachFromWall()
    {
        if (isWallAttached)
        {
            isWallAttached = false;
            Debug.Log("Detached from wall, entering free fall.");
            // ����� �����, ��������, �������� ������� ������� �������� � ����� ���������
        }
    }
}
