using UnityEngine;

public class PlayerController : MonoBehaviour
{
    // Переменные для движения
    public float speed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;
    private int jumpCount;

    // Переменные для стены
    public float wallSlideSpeed = 2f;
    private bool isTouchingWall;
    private bool isWallSliding;
    private bool isHoldingWall;

    // Прочие компоненты
    private Rigidbody2D rb;

    // Слои для проверки
    public LayerMask groundLayer;
    public LayerMask wallLayer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Бег
        float moveInput = Input.GetAxis("Horizontal");
        rb.velocity = new Vector2(moveInput * speed, rb.velocity.y);

        // Поворот персонажа
        if (moveInput > 0)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }
        else if (moveInput < 0)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }

        // Прыжок
        if (Input.GetButtonDown("Jump") && (IsGrounded() || jumpCount < maxJumps || isHoldingWall))
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);

            // Если персонаж держится за стену, сбрасываем стеновое удержание
            if (isHoldingWall)
            {
                isHoldingWall = false;
            }

            jumpCount++;
        }

        // Сброс прыжков при касании земли
        if (IsGrounded())
        {
            jumpCount = 0;
        }

        // Логика стены: скольжение и удержание
        isTouchingWall = IsTouchingWall();
        if (isTouchingWall && !IsGrounded())
        {
            isHoldingWall = true; // Персонаж автоматически держится за стену
            rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed); // Замедленное скольжение
        }
        else if (!isTouchingWall)
        {
            isHoldingWall = false; // Отпускаем стену, если контакт потерян
        }
    }

    // Проверка на землю
    private bool IsGrounded()
    {
        Vector2 position = transform.position;
        float radius = 0.2f;
        return Physics2D.OverlapCircle(position, radius, groundLayer);
    }

    // Проверка на стену
    private bool IsTouchingWall()
    {
        Vector2 position = transform.position;
        float radius = 0.2f;
        return Physics2D.OverlapCircle(position, radius, wallLayer);
    }
}
