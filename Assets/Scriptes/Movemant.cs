using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // --- Компоненты
    private CollisionController collisionController;
    private Rigidbody2D rb;
    private BoxCollider2D boxCollider;

    // --- (Для динамических хитбоксов – тема рассматривается отдельно)
    private Vector2 normalSize, normalOffset;

    // --- Флаг неуязвимости (для логики получения урона)
    private bool isInvulnerable = false;

    // --- Параметры движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // --- Параметры рывка (Dash)
    public float dashDistance = 5f;
    public float dashDuration = 0.2f;  // Длительность рывка (не телепортация)
    public float dashCooldown = 1f;
    private bool canDash = true;

    // --- Параметры подката (Slide)
    public float slideSpeed = 8f;
    public float slideDuration = 0.5f;
    private bool isSliding = false;

    // --- Приседание – для dash не допускается
    private bool isCrouching = false;

    // --- Параметры цепления за стену (Wall Hang / Slide)
    public float wallHangTime = 0.5f;    // Время, в течение которого персонаж висит неподвижно сразу после цепления за стену
    public float wallSlideAcceleration = 10f;  // Ускорение скольжения по стене (ед/сек)
    public float wallSlideMaxSpeed = 5f;       // Максимальная скорость скольжения (скорость установится отрицательной)
    public float wallJumpForce = 10f;    // Сила отталкивания при wall jump
    private bool isSlidingOnWall = false;
    private bool wallSlideActive = false;  // Флаг, что режим ускоренного скольжения (после "висения") активирован
    public float wallDetachCooldown = 0.3f;   // Период, в течение которого нельзя повторно зацепиться за стену
    private float timeSinceDetached = 0f;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        normalSize = boxCollider.size;
        normalOffset = boxCollider.offset;
    }

    void Update()
    {
        timeSinceDetached += Time.deltaTime;
        float moveInput = Input.GetAxis("Horizontal");

        bool grounded = collisionController.IsGrounded;
        bool touchingWall = collisionController.IsTouchingWall;

        // Если цепление активно, проверяем условие полного контакта.
        // Теперь функция IsAnyEdgeFullyOnWall() возвращает true, если хотя бы один из узких участков (слева или справа)
        // полностью контактирует со стеной.
        if (isSlidingOnWall)
        {
            if (!touchingWall || !IsAnyEdgeFullyOnWall())
            {
                StopWallSlide();
            }
            // Разрешаем менять направление даже при цеплении.
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Стандартное движение (если не цепляемся за стену, не в подкате и не в приседе)
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок (работает если на земле, если осталось число прыжков или цепляются за стену)
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            if (isSlidingOnWall)
            {
                // Wall jump: отталкиваемся от стены — направление противоположно тому, куда смотрит персонаж.
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

        // --- Инициирование цепления за стену (Wall Hang)
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

        // Если ускоренный режим скольжения активен, плавно увеличиваем скорость падения
        if (isSlidingOnWall && wallSlideActive && touchingWall)
        {
            float newY = Mathf.MoveTowards(rb.linearVelocity.y, -wallSlideMaxSpeed, wallSlideAcceleration * Time.deltaTime);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, newY);
        }

        // --- Остальные механики (Dash, Slide, Crouch) остаются без изменений
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

        collisionController.ignoreFlipForWallChecks = isSlidingOnWall;
    }

    // --- Метод проверки полного контакта узкой границы с стеной.
    // Теперь проверяются обе стороны: левая и правая границы хитбокса.
    private bool IsAnyEdgeFullyOnWall()
    {
        Bounds playerBounds = boxCollider.bounds;
        // Определяем узкий участок: например, ширина 0.05f, высота составляет 90% от высоты хитбокса.
        Vector2 edgeSize = new Vector2(0.05f, playerBounds.size.y * 0.9f);

        // Левый участок: берем центр на левой границе.
        Vector2 leftEdgeCenter = new Vector2(playerBounds.min.x, playerBounds.center.y);
        // Правый участок: берем центр на правой границе.
        Vector2 rightEdgeCenter = new Vector2(playerBounds.max.x, playerBounds.center.y);

        Collider2D leftWall = Physics2D.OverlapBox(leftEdgeCenter, edgeSize, 0f, collisionController.wallLayer);
        Collider2D rightWall = Physics2D.OverlapBox(rightEdgeCenter, edgeSize, 0f, collisionController.wallLayer);

        bool leftContact = false;
        bool rightContact = false;

        if (leftWall != null)
        {
            Bounds leftWallBounds = leftWall.bounds;
            Bounds leftEdgeBounds = new Bounds(leftEdgeCenter, edgeSize);
            leftContact = leftWallBounds.Contains(leftEdgeBounds.min) && leftWallBounds.Contains(leftEdgeBounds.max);
        }
        if (rightWall != null)
        {
            Bounds rightWallBounds = rightWall.bounds;
            Bounds rightEdgeBounds = new Bounds(rightEdgeCenter, edgeSize);
            rightContact = rightWallBounds.Contains(rightEdgeBounds.min) && rightWallBounds.Contains(rightEdgeBounds.max);
        }
        // Если хотя бы одна сторона полностью контактирует со стеной, возвращаем true.
        return leftContact || rightContact;
    }

    // --- Методы цепления за стену (Wall Hang / Slide)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            wallSlideActive = false;  // Сначала персонаж висит неподвижно.
            rb.linearVelocity = Vector2.zero;
            jumpCount = 0; // Сброс для возможности двойного прыжка.
            StartCoroutine(WallHangCoroutine());
        }
    }

    private IEnumerator WallHangCoroutine()
    {
        yield return new WaitForSeconds(wallHangTime);
        if (isSlidingOnWall)
        {
            wallSlideActive = true;  // Переходим в режим ускоренного скольжения.
        }
    }

    private void StopWallSlide()
    {
        isSlidingOnWall = false;
        wallSlideActive = false;
        timeSinceDetached = 0f;
    }

    // --- Рывок (Dash)
    private IEnumerator Dash()
    {
        canDash = false;
        isInvulnerable = true;

        if (!collisionController.IsGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0);
        }

        float dashDirection = (facingRight ? 1 : -1);
        float dashSpeed = dashDistance / dashDuration;

        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;
        rb.linearVelocity = new Vector2(dashDirection * dashSpeed, 0);

        yield return new WaitForSeconds(dashDuration);

        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = originalGravity;

        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
        isInvulnerable = false;
    }

    // --- Подкат (Slide)
    private IEnumerator Slide(float moveInput)
    {
        isSliding = true;
        float slideDirection = Mathf.Sign(moveInput);
        rb.linearVelocity = new Vector2(slideDirection * slideSpeed, rb.linearVelocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.linearVelocity = Vector2.zero;
        isSliding = false;
    }

    // --- Изменение направления (Flip)
    // Даже когда персонаж цепляется за стену, поворот разрешен.
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 s = transform.localScale;
        s.x *= -1;
        transform.localScale = s;
    }
}