using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- Динамический хитбокс
    private Vector2 normalSize, normalOffset;
    // Хитбокс для приседа (задаваемый через Inspector)
    public Vector2 crouchSize = new Vector2(0.8f, 0.5f);
    public Vector2 crouchOffset = new Vector2(0, -0.25f);
    // Хитбокс для подката
    public Vector2 slideSize = new Vector2(0.8f, 0.4f);
    public Vector2 slideOffset = new Vector2(0, -0.3f);

    // --- Флаг неуязвимости (для логики получения урона)
    private bool isInvulnerable = false;

    // --- Параметры движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;  // Длительность рывка (не мгновенная телепортация)
    public float dashCooldown = 1f;
    private bool canDash = true;

    // --- Параметры подката (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- Приседание
    private bool isCrouching = false;

    // --- Параметры цепления за стену (Wall Hang)
    public float wallHangTime = 0.5f;    // Время, в течение которого персонаж висит на стене перед скольжением
    public float wallSlideSpeed = 2f;    // Скорость скольжения по стене
    public float wallJumpForce = 10f;    // Сила отталкивания при wall jump
    private bool isSlidingOnWall = false;
    public float wallDetachCooldown = 0.3f;   // Время, в течение которого нельзя повторно зацепиться
    private float timeSinceDetached;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Сохраняем стандартные параметры хитбокса
        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        timeSinceDetached += Time.deltaTime;
        float moveInput = Input.GetAxis("Horizontal");

        // Кэширование состояний коллизий
        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // --- Движение (если не цепляемся за стену, не в подкате и не в приседе)
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        else if (isSlidingOnWall)
        {
            // При цеплении за стену по-прежнему отслеживаем изменения направления
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            if (isSlidingOnWall)
            {
                // Wall jump: отталкиваемся от стены
                rb.velocity = new Vector2((facingRight ? -1 : 1) * speed, wallJumpForce);
                isSlidingOnWall = false;
                timeSinceDetached = 0f;
            }
            jumpCount++;
        }
        if (grounded)
        {
            jumpCount = 0;
            isSlidingOnWall = false;
        }

        // --- Логика цепления за стену (Wall Hang)
        if (touchingWall && !grounded &&
            Mathf.Abs(moveInput) > 0.01f &&
            ((facingRight && moveInput > 0) || (!facingRight && moveInput < 0)) &&
            timeSinceDetached >= wallDetachCooldown)
        {
            StartWallHang();
        }
        if (Input.GetKeyDown(KeyCode.S) && isSlidingOnWall)
        {
            StopWallSlide();
        }

        // --- Рывок (Dash)
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
        // Если персонаж стоит (без горизонтального ввода) – приседаем (CTRL или S)
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && grounded && Mathf.Abs(moveInput) < 0.01f)
        {
            StartCrouch();
        }
        else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
        {
            EndCrouch();
        }

        // --- Обновляем хитбокс согласно состоянию (содержимое метода гарантирует, что при нормальном состоянии хитбокс восстанавливается)
        UpdateHitbox();

        // Если цепляемся за стену – информируем CollisionController, чтобы он игнорировал flip при проверке стены
        collisionController.ignoreFlipForWallChecks = isSlidingOnWall;
    }

    // Универсальный метод обновления хитбокса (для восстановления, если мы не в состоянии slide/crouch)
    private void UpdateHitbox()
    {
        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }
    }

    // --- Приседание (Crouch)
    private void StartCrouch()
    {
        if (!isCrouching)
        {
            isCrouching = true;
            rb.velocity = Vector2.zero; // Мгновенная остановка
            boxCollider.size = crouchSize;
            boxCollider.offset = crouchOffset;
        }
    }
    private void EndCrouch()
    {
        if (isCrouching)
        {
            isCrouching = false;
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }
    }

    // --- Подкат (Slide)
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        // Мгновенно меняем хитбокс для подката
        boxCollider.size = slideSize;
        boxCollider.offset = slideOffset;
        float slideDirection = Mathf.Sign(moveInput);
        rb.velocity = new Vector2(slideDirection * slideSpeed, rb.velocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.velocity = Vector2.zero;
        isSliding = false;
        // Восстанавливаем хитбокс
        boxCollider.size = normalSize;
        boxCollider.offset = normalOffset;
    }

    // --- Цепление за стену (Wall Hang)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            rb.velocity = Vector2.zero;
            jumpCount = 0; // Сбрасываем счётчик прыжков для возможности wall jump
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
        float dashSpeed = dashDistance / dashDuration;
        rb.velocity = new Vector2(dashDirection * dashSpeed, 0);
        yield return new WaitForSeconds(dashDuration);
        rb.velocity = new Vector2(0, rb.velocity.y);
        boxCollider.enabled = true;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
        isInvulnerable = false;
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
