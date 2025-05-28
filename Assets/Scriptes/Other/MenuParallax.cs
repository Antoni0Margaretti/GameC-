using UnityEngine;

public class MenuParallax : MonoBehaviour
{
    [System.Serializable]
    public class ParallaxElement
    {
        // ������, ������� ����� ������� (������, �������, ������� UI)
        public Transform target;
        // ���� �������� ��� ������� ��������
        public float strength = 0.1f;
        // ���������� �������� �������, ����� �������� ���� ������������ ��
        [HideInInspector] public Vector3 initialPosition;
    }

    // ������ ��������, �� ������� ����� ������ ������ ����������
    public ParallaxElement[] elements;

    // �������� ����������� ��������
    public float smoothSpeed = 5f;

    void Start()
    {
        // ��������� ��������� ������� ��� ���� ���������
        foreach (var elem in elements)
        {
            if (elem.target != null)
            {
                elem.initialPosition = elem.target.position;
            }
        }
    }

    void Update()
    {
        // �������� ������� ����
        Vector2 mousePos = Input.mousePosition;
        // ���������� ����� ������
        float halfScreenWidth = Screen.width / 2f;
        float halfScreenHeight = Screen.height / 2f;
        // ����������� �������� ���� ������������ ������ (�������� �� -1 �� 1)
        float normalizedX = (mousePos.x - halfScreenWidth) / halfScreenWidth;
        float normalizedY = (mousePos.y - halfScreenHeight) / halfScreenHeight;

        // ��������� �������� � ������� ��������
        foreach (var elem in elements)
        {
            if (elem.target != null)
            {
                // ��������� �������� �������� ��� ��������
                Vector3 offset = new Vector3(normalizedX * elem.strength, normalizedY * elem.strength, 0f);
                Vector3 desiredPosition = elem.initialPosition + offset;
                // ������ ���������� ������� � �������� �������
                elem.target.position = Vector3.Lerp(elem.target.position, desiredPosition, Time.deltaTime * smoothSpeed);
            }
        }
    }
}
