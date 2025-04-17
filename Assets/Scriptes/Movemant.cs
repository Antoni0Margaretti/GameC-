using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Ссылки на компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- Динамический хитбокс
    private Vector2 normalSize, normalOffset;
    // Размеры и смещения для приседания
    public Vector2 crouchSize = new Vector2(0.8f, 0.5f);
    public Vector2 crouchOffset = new Vector2(0, -0.25f);
    // Размеры и смещения для подката
    public Vector2 slideSize = new Vector2(0.8f, 0.4f);
    public Vector2 slideOffset = new Vector2(0, -0.3f);

    // --- Флаг неуязвимости (можно использовать в логике получения урона)
    private bool isInvulnerable = false;

    // --- Параметры движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;
    public float dashDuration = 0.2f; // Длительность рывка (не мгновенная телепортация)
    public float dashCooldown = 1f;
    private bool canDash = true;

    // --- Параметры подката (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- Приседание
    private bool isCrouching = false;

    // --- Параметры цепления за стену (Wall Hang)
    public float wallHangTime = 0.5f;    // Время до начала скольжения по стене
    public float wallSlideSpeed = 2f;    // Скорость скольжения по стене
    public float wallJumpForce = 10f;    // Сила отталкивания при Wall Jump
    private bool isSlidingOnWall = false;
    // Время, в течение которого нельзя повторно зацепиться после Wall Jump
    public float wallDetachCooldown = 0.3f;
    private float timeSinceDetached;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Сохраняем исходные параметры хитбокса (нормальный размер)
        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        timeSinceDetached += Time.deltaTime;
        float moveInput = Input.GetAxis("Horizontal");

        // Кэшируем состояния из CollisionController
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // --- Обычное движение (если не цепляемся, не в подкате и не приседаем)
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

            // Обновляем направление персонажа на основе ввода
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        else if (isSlidingOnWall)
        {
            // При цеплении за стену всё равно отслеживаем ввод для смены направления
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);

            // Если прыжок выполняется от стены, это wall jump
            if (isSlidingOnWall)
            {
                rb.velocity = new Vector2((facingRight ? -1 : 1) * speed, wallJumpForce);
                isSlidingOnWall = false;
                timeSinceDetached = 0f;
            }
            jumpCount++;
        }

        // Сброс прыжков при касании земли
        if (grounded)
        {
            jumpCount = 0;
            isSlidingOnWall = false;
        }

        // --- Логика цепления за стену
        // Если персонаж касается стены, не на земле, ввод направлен в сторону стены и прошло достаточно времени после wall jump
        if (touchingWall && !grounded &&
            Mathf.Abs(moveInput) > 0.01f &&
            ((facingRight && moveInput > 0) || (!facingRight && moveInput < 0)) &&
            timeSinceDetached >= wallDetachCooldown)
        {
            StartWallHang();
        }

        // Отмена цепления по нажатию S
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
        {
            StopWallSlide();
        }

        // --- Рывок (Dash)
        // При нажатии LeftShift инициируется рывок, во время которого хитбокс отключается (что действует как неуязвимость)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash)
        {
            StartCoroutine(Dash(moveInput));
        }

        // --- Подкат (Slide) и приседание
        // Если персонаж движется на земле – подкат по нажатию LeftControl
        if (Input.GetKeyDown(KeyCode.LeftControl) && grounded && Mathf.Abs(moveInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(moveInput));
        }
        // Если персонаж стоит (нет горизонтального ввода) и зажат LeftControl – приседание (аналог S)
        else if (Input.GetKey(KeyCode.LeftControl) && grounded && Mathf.Abs(moveInput) < 0.01f)
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.velocity = Vector2.zero;
            }
        }
        // Приседание также по S
        else if (Input.GetKey(KeyCode.S) && grounded && !isSliding && !isSlidingOnWall)
        {
            if (!isCrouching)
            {
                isCrouching = true;
                rb.velocity = Vector2.zero;
            }
        }
        else if (!Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.LeftControl))
        {
            isCrouching = false;
        }

        // --- Обновление динамического хитбокса
        UpdateHitbox();

        // Если цепляемся за стену – информируем CollisionController, чтобы он игнорировал флип для стен (фиксированная зона)
        collisionController.ignoreFlipForWallChecks = isSlidingOnWall;
    }

    // Обновление размеров и смещения BoxCollider2D в зависимости от состояния
    private void UpdateHitbox()
    {
        if (isSliding)
        {
            boxCollider.size = slideSize;
            boxCollider.offset = slideOffset;
        }
        else if (isCrouching)
        {
            boxCollider.size = crouchSize;
            boxCollider.offset = crouchOffset;
        }
        else
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }
    }

    // --- Цепление за стену (Wall Hang)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            rb.velocity = Vector2.zero;
            jumpCount = 0;  // Сбрасываем прыжковый счётчик для возможности двойного прыжка
            StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            BeginWallSlide();
        }
    }

    private void BeginWallSlide()
    {
        isSlidingOnWall = true;
        rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed);
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        timeSinceDetached = 0f;
    }

    // --- Рывок (Dash)
    private IEnumerator Dash(float moveInput)
    {
        canDash = false;
        isInvulnerable = true;
        // Отключаем хитбокс, чтобы атаки проходили сквозь персонажа во время рывка
        boxCollider.enabled = false;
        float dashDirection = (Mathf.Abs(moveInput) > 0.01f) ? Mathf.Sign(moveInput) : (facingRight ? 1 : -1);
        // Вычисляем скорость рывка: dashDistance / dashDuration
        float dashSpeed = dashDistance / dashDuration;
        rb.velocity = new Vector2(dashDirection * dashSpeed, 0);
        yield return new WaitForSeconds(dashDuration);
        rb.velocity = new Vector2(0, rb.velocity.y);
        // Включаем хитбокс после рывка
        boxCollider.enabled = true;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
        isInvulnerable = false;
    }

    // --- Подкат (Slide)
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        rb.velocity = new Vector2(slideDirection * slideSpeed, rb.velocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.velocity = Vector2.zero;
        isSliding = false;
    }

    // --- Изменение направления (Flip)
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }
}
