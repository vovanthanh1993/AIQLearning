using UnityEngine;

public class AgentController : MonoBehaviour
{
    // Di chuyển Agent một ô theo hướng được chỉ định
    public void Move(Vector2 direction)
    {
        Vector2 newPos = (Vector2)transform.position + direction;

        // Giả sử bạn truyền gridWidth, gridHeight từ AIManager hoặc đặt giá trị cố định
        int gridWidth = 17;
        int gridHeight = 10;

        float minX = -gridWidth / 2f;
        float maxX = gridWidth / 2f - 1;
        float minY = -gridHeight / 2f;
        float maxY = gridHeight / 2f - 1;

        if (newPos.x >= minX && newPos.x <= maxX && newPos.y >= minY && newPos.y <= maxY)
        {
            transform.position = newPos;
        }
        // Nếu không hợp lệ thì không di chuyển
    }

    // Đưa Agent về vị trí ban đầu
    public void ResetPosition(Vector2 startPos)
    {
        transform.position = startPos;
    }
}