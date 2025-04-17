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

    // --- Параметры цепления за стену (Wall Hang)
    public float wallHangTime = 0.5f;    // Время, в течение которого персонаж висит на стене перед скольжением
    public float wallSlideSpeed = 2f;    // Скорость скольжения по стене
    public float wallJumpForce = 10f;    // Сила отталкивания при wall jump
    private bool isSlidingOnWall = false;
    public float wallDetachCooldown = 0.3f;   // Время, в течение которого нельзя повторно зацепиться за стену
    private float timeSinceDetached;

    // --- Флаг направления (куда смотрит персонаж)
    private bool facingRight = true;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        collisionController = GetComponent<CollisionController>();
        boxCollider = GetComponent<BoxCollider2D>();

        // Сохраняем исходные параметры хитбокса
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

        // --- Движение (если не цепляемся за стену, не в подкате и не в приседе)
        if (!isSlidingOnWall && !isSliding && !isCrouching)
        {
            rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

            // Используем направление персонажа, чтобы определить сторону
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }
        else if (isSlidingOnWall)
        {
            if (moveInput > 0 && !facingRight)
                Flip();
            else if (moveInput < 0 && facingRight)
                Flip();
        }

        // --- Прыжок (работает если на земле, если ещё есть прыжки или цепляясь за стену)
        if (Input.GetButtonDown("Jump") && (grounded || jumpCount < maxJumps || isSlidingOnWall))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            if (isSlidingOnWall)
            {
                // Wall jump: отталкиваемся от стены (просто сбрасываем цепление)
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

        // --- Цепление за стену (Wall Hang)
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
        // Рывок НЕ разрешается в состояниях подката (isSliding) или приседа (isCrouching)
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isSliding && !isCrouching)
        {
            StartCoroutine(Dash());
        }

        // --- Подкат (Slide) и приседание
        if (Input.GetKeyDown(KeyCode.LeftControl) && grounded && Mathf.Abs(moveInput) > 0.01f && !isSliding)
        {
            StartCoroutine(Slide(moveInput));
        }
        else if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.S)) && grounded && Mathf.Abs(moveInput) < 0.01f)
        {
            isCrouching = true;
            rb.velocity = Vector2.zero;
        }
        else if (!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.S))
        {
            isCrouching = false;
        }

        // Если не в подкате и не в приседе, восстанавливаем стандартный хитбокс
        if (!isSliding && !isCrouching)
        {
            boxCollider.size = normalSize;
            boxCollider.offset = normalOffset;
        }

        // Если цепляемся за стену, уведомляем CollisionController об этом (чтобы игнорировать flip)
        collisionController.ignoreFlipForWallChecks = isSlidingOnWall;
    }

    // --- Цепление за стену (Wall Hang)
    private void StartWallHang()
    {
        if (!isSlidingOnWall)
        {
            isSlidingOnWall = true;
            rb.velocity = Vector2.zero;
            jumpCount = 0; // Сброс для возможности двойного прыжка
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
    private IEnumerator Dash()
    {
        // Условия: работает только если не в подкате и не в приседе (условия проверены в Update)
        canDash = false;
        isInvulnerable = true;

        // Если персонаж находится в воздухе, обнуляем вертикальную скорость (иначе продолжается падение)
        if (!collisionController.IsGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0);
        }

        // Если персонаж висит на стене, то dash выполняется только в сторону, отдаляясь от стены.
        // В негабаритном состоянии (на земле или в воздухе) dash производится в ту сторону, куда смотрит персонаж.
        float dashDirection = (facingRight ? 1 : -1);
        // Рассчитываем скорость так, чтобы за dashDuration пройти dashDistance
        float dashSpeed = dashDistance / dashDuration;

        // Отключаем гравитацию, чтобы движение было строго горизонтальным
        float originalGravity = rb.gravityScale;
        rb.gravityScale = 0;

        // Запускаем рывок: задаём горизонтальную скорость
        rb.velocity = new Vector2(dashDirection * dashSpeed, 0);

        yield return new WaitForSeconds(dashDuration);

        rb.velocity = Vector2.zero;
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
        rb.velocity = new Vector2(slideDirection * slideSpeed, rb.velocity.y);
        yield return new WaitForSeconds(slideDuration);
        rb.velocity = Vector2.zero;
        isSliding = false;
    }

    // --- Изменение направления (Flip)
    private void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}