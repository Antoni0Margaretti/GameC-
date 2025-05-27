using UnityEngine;

public class Parallax : MonoBehaviour
{
    // ����������� ���������� ��� ����� ���� (0 �������� �����������, 1 � �������� ������ � �������)
    [SerializeField]
    private float parallaxMultiplier = 0.5f;

    private Transform cam;
    private Vector3 previousCamPosition;

    void Start()
    {
        cam = Camera.main.transform;
        previousCamPosition = cam.position;
    }

    void LateUpdate()
    {
        // ���������� ��������� ������� ������ � �������� �����
        Vector3 deltaMovement = cam.position - previousCamPosition;

        // ���������� ���� ��������������� ������������
        transform.position += new Vector3(deltaMovement.x * parallaxMultiplier, deltaMovement.y * parallaxMultiplier, 0);

        previousCamPosition = cam.position;
    }
}