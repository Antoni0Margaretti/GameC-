using System.Collections;
using UnityEngine;

public class CombatController : MonoBehaviour
{
    // ==========================
    //  Настройки стандартной атаки (ЛКМ)
    // ==========================
    [Header("Combo Attack Settings")]
    [Tooltip("Максимальное число ударов в комбо")]
    public int maxCombo = 3;
    [Tooltip("Время, после которого комбо сбрасывается")]
    public float comboResetTime = 1.0f;
    [Tooltip("Массив объектов-хитбоксов для стандартной атаки. Каждый элемент соответствует отдельному удару в комбо.")]
    public GameObject[] comboAttackHitboxes;

    private int currentCombo = 0;
    private float comboTimer = 0f;
    private bool isAttacking = false;

    // ==========================
    //  Настройки парирования (ПКМ)
    // ==========================
    [Header("Parry Settings")]
    [Tooltip("Длительность режима парирования")]
    public float parryDuration = 0.5f;
    [Tooltip("Кулдаун после парирования")]
    public float parryCooldown = 1.0f;
    [Tooltip("Объект-хитбокс для парирования")]
    public GameObject parryHitbox;

    private bool isParrying = false;
    private float parryCooldownTimer = 0f;

    // ==========================
    //  Настройки урона от dash-атаки
    // ==========================
    [Header("Dash Attack Damage Settings")]
    [Tooltip("Объект-хитбокс, наносящий урон при рывке")]
    public GameObject dashAttackHitbox;
    [Tooltip("Длительность активации hitbox'а при dash-атаке")]
    public float dashDamageDuration = 0.3f;
    private bool isDashAttacking = false;

    // ==========================
    //  Флаги текущего состояния игрока (настраиваются извне или другим скриптом)
    // ==========================
    [Header("Player State Flags (set externally)")]
    [Tooltip("Персонаж в состоянии присяда?")]
    public bool isCrouching;
    [Tooltip("Персонаж в состоянии подката?")]
    public bool isSliding;
    [Tooltip("Персонаж в состоянии рывка? (из движенческого скрипта)")]
    public bool isDashing;
    [Tooltip("Персонаж в состоянии взбирания на край стены?")]
    public bool isLedgeClimbing;
    [Tooltip("Персонаж прикреплён к стене (висит, скользит, автоклаймбится)")]
    public bool isWallAttached;

    void Start()
    {
        // Отключаем все hitbox'ы при старте
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

        // Обновляем таймер комбо (если атака запущена)
        if (isAttacking)
        {
            comboTimer += Time.deltaTime;
            if (comboTimer > comboResetTime)
            {
                ResetCombo();
            }
        }

        // Обновляем кулдаун парирования
        if (parryCooldownTimer > 0f)
        {
            parryCooldownTimer -= Time.deltaTime;
        }
    }

    // ==========================
    //  Вход для стандартной атаки (ЛКМ)
    // ==========================
    void HandleAttackInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Если персонаж находится в запрещённых состояниях — атака не может быть выполнена
            if (isCrouching || isSliding || isDashing || isLedgeClimbing)
            {
                Debug.Log("Attack input ignored due to forbidden state (crouching/sliding/dashing/ledge climbing).");
                return;
            }

            // Если персонаж прикреплён к стене — автоматически отцепляемся
            if (isWallAttached)
            {
                DetachFromWall();
            }

            // Если персонаж уже не занят другой атакой/парированием/рывком — запускаем комбо
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

        // Если для данного удара определён отдельный hitbox — активируем его
        ActivateAttackHitboxForCombo(currentCombo);
        yield return new WaitForSeconds(0.3f);
        DeactivateAttackHitboxForCombo(currentCombo);

        // Ожидаем ввод для следующего удара
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
    //  Вход для парирования (ПКМ)
    // ==========================
    void HandleParryInput()
    {
        if (Input.GetMouseButtonDown(1))
        {
            // Парировать нельзя, если персонаж выполняет рывок или взбирание
            if (isDashing || isLedgeClimbing)
            {
                Debug.Log("Parry input ignored due to dash/ledge climbing state.");
                return;
            }

            // Если персонаж прикреплён к стене — отсоединяемся
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

    // Если во время парирования происходит попытка рывка, прерываем парирование
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

    // Метод, который может быть вызван извне (например, при удачном отражении атаки) для сброса кулдауна
    public void OnSuccessfulParry()
    {
        // Завершаем парирование немедленно и обнуляем кулдаун
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
    //  Вход для dash attack damage (клавиша E)
    // ==========================
    void HandleDashAttackDamageInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Если во время парирования совершают dash – прерываем парирование и НЕ выполняем dash
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
    //  Метод отсоединения от стены (переход в свободное падение)
    // ==========================
    void DetachFromWall()
    {
        if (isWallAttached)
        {
            isWallAttached = false;
            Debug.Log("Detached from wall, entering free fall.");
            // Здесь можно, например, сообщить другому скрипту движения о смене состояния
        }
    }
}
