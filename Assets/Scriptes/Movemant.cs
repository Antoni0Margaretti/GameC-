using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // === Компоненты
    [Header("Components")]
    public CollisionController collisionController; // Назначьте этот компонент через Inspector
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // === Настройка хитбокса (если требуются динамические эффекты)
    [Header("Hitbox Settings")]
    [SerializeField] private Vector2 normalSize;
    [SerializeField] private Vector2 normalOffset;

    // === Параметры получения урона
    [Header("Damage & Invulnerability")]
    public bool isInvulnerable = false;

    // === Параметры движения
    [Header("Movement")]
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount = 0;

    // === Параметры рывка (Dash)
    [Header("Dash")]
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;  // длительность рывка
    public float dashCooldown = 1f;
    private bool canDash = true;

    // === Параметры подката (Slide)
    [Header("Slide")]
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // === Параметры приседания (Crouch)
    [Header("Crouch")]
    public bool isCrouching = false;

    // === Параметры цепления за стену (Wall Hang / Slide)
    [Header("Wall Clinging")]
    public float wallHangTime = 2.0f;          // Персонаж висит 2 секунды, прежде чем начнёт скользить
    public float wallSlideAcceleration = 10f;  // Ускорение скольжения (ед/сек)
    public float wallSlideMaxSpeed = 5f;       // Максимальная скорость скольжения (абсолютное значение)
    public float wallJumpForce = 10f;          // Сила отталкивания при wall jump
    public float wallDetachCooldown = 0.3f;    // Cooldown перед новым цеплением
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;      // false – режим "висения", true – режим ускоренного скольжения
    private float timeSinceDetached = 0f;

    // === Параметры направления
    [Header("Direction")]
    public bool facingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        // Если collisionController не назначен через Inspector – пробуем получить его из того же объекта
        if (collisionController == null)
            collisionController = GetComponent<CollisionController>();

        boxCollider = GetComponent<BoxCollider2D>();

        // Сохраняем исходные размеры хитбокса. (Убедитесь, что их не переопределяют в коде после этого)
        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        // Обновляем таймер для cooldown цепления
        timeSinceDetached += Time.deltaTime;
        float moveInput = Input.GetAxis("Horizontal");
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // Если персонаж цепляется за стену, но стена больше не обнаруживается, прекращаем цепление.
        if (isSlidingOnWall && !touchingWall)
        {
            StopWallSlide();
        }

        // Если персонаж уже в цеплении, горизонтальный ввод используется только для поворота (Flip())
        if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        // Если не цепляемся, выполняем стандартное движение
        else if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            if (isSlidingOnWall)
            {
                // При wall jump задаём горизонтальный импульс в противоположном направлении.
                rb.linearVelocity = new Vector2((facingRight ? -1 : 1) * speed, wallJumpForce);
                StopWallSlide();
                timeSinceDetached = 0f;
            }
            jumpCount++;
        }
        if (grounded)
        {
            jumpCount = 0;
            StopWallSlide();
        }

        // --- Цепление за стену (Wall Hang)
        // Теперь условие простое: если есть касание стены, персонаж не на земле и прошёл cooldown – запускаем цепление.
        if (collisionController.IsTouchingWall && !grounded && timeSinceDetached >= wallDetachCooldown)
        {
            StartWallHang();
        }
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
        {
            StopWallSlide();
        }

        // Если режим ускоренного скольжения активен, постепенно увеличиваем вертикальную скорость до -wallSlideMaxSpeed.
        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
        }

        // --- Остальные механики (Dash, Slide, Crouch)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            StartCoroutine(Dash());
        }
        if (Input.GetKeyDown(KeyCode.LeftControl) && grounded && Mathf.Abs(moveInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(moveInput));
        }
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && grounded && Mathf.Abs(moveInput) < 0.01f)
        {
            isCrouching = true;
            rb.linearVelocity = Vector2.zero;
        }
        else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
        {
            isCrouching = false;
        }

        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }

        // Всегда используем TransformPoint корректно в CollisionController – здесь выставляем значение фиксированным.
        collisionController.ignoreFlipForWallChecks = false;
    }

    // === Методы цепления за стену
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;  // Сначала режим "висения"
            rb.linearVelocity = Vector2.zero;
            jumpCount = 0;
            StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true;  // По истечении wallHangTime (2 секунды) включаем режим ускоренного скольжения.
        }
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;
    }

    // === Рывок (Dash)
    private IEnumerator Dash()
    {
        canDash = false;
        isInvulnerable = true;
        float currentVertical = rb.linearVelocity.y;
        if (collisionController.IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
            currentVertical = 0;
        }
        float dashDirection = (facingRight ? 1 : -1);
        float dashSpeed = dashDistance / dashDuration;
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, currentVertical);
        yield return new WaitForSeconds(dashDuration);
        rb.gravityScale = originalGravity;
        if (collisionController.IsGrounded)
            rb.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
        isInvulnerable = false;
    }

    // === Подкат (Slide)
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        rb.linearVelocity = new Vector2(slideDirection * slideSpeed, rb.linearVelocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.linearVelocity = Vector2.zero;
        isSliding = false;
    }

    // === Изменение направления (Flip)
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }
}