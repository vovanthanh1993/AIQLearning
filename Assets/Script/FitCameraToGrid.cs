using UnityEngine;

[RequireComponent(typeof(Camera))] // Đảm bảo script này chỉ có thể gắn vào đối tượng có Camera
public class FitCameraToGrid : MonoBehaviour
{
    // Kéo GameObject AIManager của bạn vào đây trong Inspector
    public AIManager aiManager;

    // Thêm một chút khoảng đệm xung quanh lưới cho đẹp mắt
    public float padding = 2.0f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();

        // Đảm bảo camera là Orthographic
        if (!cam.orthographic)
        {
            Debug.LogError("Camera must be Orthographic to use this script.");
            return;
        }

        AdjustCameraSize();
    }

    void AdjustCameraSize()
    {
        if (aiManager == null)
        {
            Debug.LogError("AIManager reference is not set in FitCameraToGrid script.");
            return;
        }

        // Lấy kích thước lưới từ AIManager
        float gridWidth = aiManager.gridWidth;
        float gridHeight = aiManager.gridHeight;

        // 1. Tính toán tỷ lệ khung hình của màn hình (ví dụ: 16:9 = 1.777)
        float aspectRatio = (float)Screen.width / Screen.height;

        // 2. Tính toán 'orthographicSize' cần thiết để vừa chiều rộng của lưới
        // orthographicSize là 1/2 chiều cao của camera.
        // Chiều rộng của camera = chiều cao * tỷ lệ khung hình = (orthographicSize * 2) * aspectRatio
        // => orthographicSize cần cho chiều rộng = (gridWidth + padding) / aspectRatio / 2
        float requiredSizeForWidth = (gridWidth + padding) / aspectRatio / 2.0f;

        // 3. Tính toán 'orthographicSize' cần thiết để vừa chiều cao của lưới
        float requiredSizeForHeight = (gridHeight + padding) / 2.0f;

        // 4. Chọn giá trị LỚN HƠN trong hai giá trị trên để đảm bảo cả chiều rộng và chiều cao đều nằm trong khung hình
        cam.orthographicSize = Mathf.Max(requiredSizeForWidth, requiredSizeForHeight);
    }
}