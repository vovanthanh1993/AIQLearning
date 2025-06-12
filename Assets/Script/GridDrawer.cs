using UnityEngine;

public class GridDrawer : MonoBehaviour
{
    [Header("Cài đặt Lưới")]
    public int gridWidth = 20;
    public int gridHeight = 10;
    public float lineThickness = 0.05f; // Độ dày của đường kẻ

    [Header("Tham chiếu")]
    public GameObject linePrefab; // Kéo Prefab đường kẻ bạn đã tạo vào đây

    void Start()
    {
        DrawGrid();
    }

    void DrawGrid()
    {
        if (linePrefab == null)
        {
            Debug.LogError("Chưa gán Prefab cho đường kẻ!");
            return;
        }

        // Tạo một GameObject cha để chứa các đường kẻ cho gọn gàng
        GameObject gridHolder = new GameObject("GridLines");

        // Tính toán các đường biên
        float startX = -gridWidth / 2f;
        float startY = -gridHeight / 2f;

        // Vẽ các đường kẻ dọc
        for (int x = 0; x <= gridWidth; x++)
        {
            float xPos = startX + x;

            GameObject verticalLine = Instantiate(linePrefab, gridHolder.transform);
            verticalLine.name = $"Vertical_Line_{x}";
            verticalLine.transform.position = new Vector2(xPos, 0);
            verticalLine.transform.localScale = new Vector2(lineThickness, gridHeight);
        }

        // Vẽ các đường kẻ ngang
        for (int y = 0; y <= gridHeight; y++)
        {
            float yPos = startY + y;

            GameObject horizontalLine = Instantiate(linePrefab, gridHolder.transform);
            horizontalLine.name = $"Horizontal_Line_{y}";
            horizontalLine.transform.position = new Vector2(0, yPos);
            horizontalLine.transform.localScale = new Vector2(gridWidth + lineThickness, lineThickness);
        }
    }
}