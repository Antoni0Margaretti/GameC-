using System.Collections.Generic;
using UnityEngine;

// ������������ ������� ����������� ����������� �� �������.
[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class DynamicSpriteCollider : MonoBehaviour
{
    // ������ �� ��������� PolygonCollider2D, ������� �� ����� ���������.
    private PolygonCollider2D polyCollider;
    // ������ �� SpriteRenderer, ����� �������� ������� ������ �������.
    private SpriteRenderer spriteRenderer;
    // ��������� ��������� �������������� ������, ����� �� ��������� ������� ������ ���.
    private Sprite lastSprite;

    void Awake()
    {
        // �������������� ������ �� ����������.
        polyCollider = GetComponent<PolygonCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        // ��� ������ ��������� ������� � ������������ � �������������� ��������.
        UpdateCollider();
    }

    void Update()
    {
        // ���� ������ ��������� (��������, � ��������), ��������� ����� ��������.
        if (spriteRenderer.sprite != lastSprite)
        {
            UpdateCollider();
        }
    }

    /// <summary>
    /// ��������� ����� PolygonCollider2D �� ������ ���������� ����� �������� �������.
    /// ����� ������ �� �������� �������, �������� � Sprite Editor (Custom Physics Shape).
    /// </summary>
    public void UpdateCollider()
    {
        // ���� ������ �� ��������, ������ �� ������.
        if (spriteRenderer.sprite == null)
            return;

        // ��������� ��������� �������������� ������.
        lastSprite = spriteRenderer.sprite;

        // �������� ���������� �������� (����) ���������� �����, �������� ��� �������.
        int shapeCount = spriteRenderer.sprite.GetPhysicsShapeCount();
        // ������������� ���������� ����� � PolygonCollider2D ������ ����� ��������.
        polyCollider.pathCount = shapeCount;

        // ��������� ������ ��� �������� ����� ������ �������.
        List<Vector2> shapePoints = new List<Vector2>();

        // ��� ������� ������� �������� ����� ������ � ��������� ��� � PolygonCollider2D
        for (int i = 0; i < shapeCount; i++)
        {
            // ������� ������ �� ������, ���� �� ��� �������� ������.
            shapePoints.Clear();
            // ��������� ������ ������� ������� � �������� i.
            spriteRenderer.sprite.GetPhysicsShape(i, shapePoints);
            // ��������� ���������� ������ ����� � �������� ���� ����������.
            polyCollider.SetPath(i, shapePoints.ToArray());
        }
    }
}