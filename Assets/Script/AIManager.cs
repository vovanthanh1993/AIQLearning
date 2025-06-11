using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Globalization;

public class AIManager : MonoBehaviour
{
    //--------------------------------------------------------------------------------
    // --- THIẾT LẬP TRONG UNITY INSPECTOR ---
    //--------------------------------------------------------------------------------

    [Header("Tham chiếu Đối tượng (Kéo từ Scene/Project)")]
    public Transform playerTransform;
    public GameObject wallPrefab;
    public GameObject goldPrefab;

    [Header("Cài đặt Lưới & Môi trường")]
    public int gridWidth = 20;
    public int gridHeight = 10;
    [Range(0, 1)]
    public float wallDensity = 0.15f;

    [Header("Tham số Huấn luyện")]
    public int totalEpisodes = 30000;
    public int maxStepsPerEpisode = 200;
    public bool enableTraining = true;
    public int reportInterval = 100; // In báo cáo sau mỗi 100 ván

    //--------------------------------------------------------------------------------
    // --- BIẾN NỘI BỘ CỦA AI ---
    //--------------------------------------------------------------------------------

    private Transform goldInstance;
    private List<GameObject> wallInstances = new List<GameObject>();
    private Vector2 playerStartPosition = Vector2.zero;

    private Dictionary<string, float[]> qTable = new Dictionary<string, float[]>();
    private int actionCount = 4; // 0:Lên, 1:Xuống, 2:Trái, 3:Phải

    private float learningRate = 0.1f;
    private float gamma = 0.95f;
    private float epsilon = 1.0f;
    private float epsilonDecay = 0.9999f;
    private float minEpsilon = 0.01f;

    // Biến để thu thập dữ liệu cho báo cáo
    private List<int> episodeSteps = new List<int>();
    private List<float> episodeRewards = new List<float>();
    private int winCount = 0;


    //================================================================================
    // --- VÒNG LẶP CHÍNH CỦA UNITY & AI ---
    //================================================================================

    void Start()
    {
        if (enableTraining)
        {
            if (LoadQTable()) { Debug.Log("Model đã được tải. TIẾP TỤC HUẤN LUYỆN..."); }
            else { Debug.Log("Không tìm thấy model. Bắt đầu huấn luyện từ đầu..."); }
            StartCoroutine(TrainingLoop());
        }
        else
        {
            if (LoadQTable())
            {
                Debug.Log("Model đã tải. Bắt đầu chế độ chơi.");
                epsilon = 0;
                StartCoroutine(PlayMode());
            }
            else { Debug.LogError("Không tìm thấy model đã lưu để chơi! Vui lòng bật chế độ huấn luyện trước."); }
        }
    }

    IEnumerator TrainingLoop()
    {
        Debug.Log("Bắt đầu huấn luyện... Vui lòng đợi.");
        // Time.timeScale = 100f; // Bỏ comment để học nhanh

        for (int i = 0; i < totalEpisodes; i++)
        {
            ResetEpisode();
            bool episodeDone = false;
            int steps = 0;
            float totalRewardForEpisode = 0f;

            while (!episodeDone && steps < maxStepsPerEpisode)
            {
                // ... (Lấy trạng thái, chọn hành động, di chuyển) ...
                string currentState = GetState(playerTransform.position);
                int action = ChooseAction(currentState);
                Vector2 oldPos = playerTransform.position;
                MovePlayer(action);

                float reward = -1f; // Phạt -1 cho mỗi bước đi mặc định
                bool hitWall = false;

                Collider2D hitCollider = Physics2D.OverlapCircle(playerTransform.position, 0.4f);
                if (hitCollider != null)
                {
                    if (hitCollider.CompareTag("Wall"))
                    {
                        reward = -100f;
                        episodeDone = true;
                        hitWall = true;
                    }
                    else if (hitCollider.CompareTag("Gold"))
                    {
                        reward = 100f;
                        episodeDone = true;
                        winCount++;
                    }
                }
                if (hitWall) playerTransform.position = oldPos;

                
                steps++;

                // KIỂM TRA ĐIỀU KIỆN HẾT GIỜ SAU CÙNG
                if (!episodeDone && steps >= maxStepsPerEpisode)
                {
                    // Nếu không có sự kiện gì xảy ra và đây là bước cuối cùng
                    reward = -20f; // Gán một hình phạt cụ thể cho việc hết giờ
                    episodeDone = true;
                }


                // Cập nhật Q-Table với reward cuối cùng đã được xác định
                totalRewardForEpisode += reward;
                string newState = GetState(playerTransform.position);
                UpdateQTable(currentState, action, reward, newState);
                yield return null;
            }

            episodeSteps.Add(steps);
            episodeRewards.Add(totalRewardForEpisode);
            epsilon = Mathf.Max(minEpsilon, epsilon * epsilonDecay);

            if ((i + 1) % reportInterval == 0)
            {
                PrintReport(i + 1);
                // Lưu model định kỳ
                if ((i + 1) % (reportInterval * 10) == 0) SaveQTable();
            }
        }

        Debug.Log("Huấn luyện hoàn tất!");
        Time.timeScale = 1f;
        SaveQTable();
        StartCoroutine(PlayMode());
    }

    IEnumerator PlayMode()
    {
        Debug.Log("--- Chế độ Chơi ---");
        epsilon = 0;
        while (true)
        {
            ResetEpisode();
            bool episodeDone = false;
            while (!episodeDone)
            {
                string currentState = GetState(playerTransform.position);
                int action = ChooseAction(currentState);
                MovePlayer(action);

                Collider2D hitCollider = Physics2D.OverlapCircle(playerTransform.position, 0.4f);
                if (hitCollider != null)
                {
                    if (hitCollider.CompareTag("Gold")) { episodeDone = true; Debug.Log("Thắng!"); }
                    else if (hitCollider.CompareTag("Wall")) { episodeDone = true; Debug.Log("Thua!"); }
                }
                yield return new WaitForSeconds(0.15f);
            }
            yield return new WaitForSeconds(2f);
        }
    }

    //================================================================================
    // --- LOGIC CỐT LÕI CỦA AI ---
    //================================================================================

    #region AI Logic Functions

    string GetState(Vector2 playerPos)
    {
        StringBuilder stateBuilder = new StringBuilder();
        stateBuilder.Append(IsWallAt(playerPos + Vector2.up) ? '1' : '0');
        stateBuilder.Append(IsWallAt(playerPos + Vector2.down) ? '1' : '0');
        stateBuilder.Append(IsWallAt(playerPos + Vector2.left) ? '1' : '0');
        stateBuilder.Append(IsWallAt(playerPos + Vector2.right) ? '1' : '0');

        Vector2 goldPos = goldInstance != null ? (Vector2)goldInstance.position : new Vector2(999, 999);
        stateBuilder.Append(goldPos.y > playerPos.y ? '1' : '0');
        stateBuilder.Append(goldPos.y < playerPos.y ? '1' : '0');
        stateBuilder.Append(goldPos.x < playerPos.x ? '1' : '0');
        stateBuilder.Append(goldPos.x > playerPos.x ? '1' : '0');

        return stateBuilder.ToString();
    }

    bool IsWallAt(Vector2 position)
    {
        Collider2D hitCollider = Physics2D.OverlapCircle(position, 0.4f);
        return (hitCollider != null && hitCollider.CompareTag("Wall"));
    }

    int ChooseAction(string state)
    {
        if (Random.Range(0f, 1f) > epsilon)
            return System.Array.IndexOf(GetQValues(state), GetQValues(state).Max());
        return Random.Range(0, actionCount);
    }

    void UpdateQTable(string state, int action, float reward, string nextState)
    {
        float[] currentQ = GetQValues(state);
        float[] nextQ = GetQValues(nextState);
        float oldQValue = currentQ[action];
        float nextMaxQ = nextQ.Max();
        currentQ[action] = oldQValue + learningRate * (reward + gamma * nextMaxQ - oldQValue);
    }

    float[] GetQValues(string state)
    {
        if (!qTable.ContainsKey(state)) qTable[state] = new float[actionCount];
        return qTable[state];
    }

    #endregion

    //================================================================================
    // --- QUẢN LÝ GAME & TIỆN ÍCH ---
    //================================================================================

    #region Game Management & Helpers

    void ResetEpisode()
    {
        foreach (var wall in wallInstances) Destroy(wall);
        wallInstances.Clear();
        if (goldInstance != null) Destroy(goldInstance.gameObject);

        for (int x = -gridWidth / 2; x < gridWidth / 2; x++)
        {
            for (int y = -gridHeight / 2; y < gridHeight / 2; y++)
            {
                if ((Mathf.Abs(x) < 2 && Mathf.Abs(y) < 2)) continue;
                if (Random.Range(0f, 1f) < wallDensity)
                {
                    GameObject wall = Instantiate(wallPrefab, new Vector2(x, y), Quaternion.identity);
                    wallInstances.Add(wall);
                }
            }
        }
        playerTransform.position = GetRandomSafePosition();
        goldInstance = Instantiate(goldPrefab, GetRandomSafePosition(), Quaternion.identity).transform;
    }

    Vector2 GetRandomSafePosition()
    {
        Vector2 pos;
        int safetyNet = 0;
        do
        {
            pos = new Vector2(Random.Range(-gridWidth / 2, gridWidth / 2), Random.Range(-gridHeight / 2, gridHeight / 2));
            safetyNet++;
        } while (Physics2D.OverlapCircle(pos, 0.4f) != null && safetyNet < 100);
        return pos;
    }

    void MovePlayer(int action)
    {
        Vector2 moveVector = Vector2.zero;
        if (action == 0) moveVector = Vector2.up;
        if (action == 1) moveVector = Vector2.down;
        if (action == 2) moveVector = Vector2.left;
        if (action == 3) moveVector = Vector2.right;

        Vector2 targetPosition = (Vector2)playerTransform.position + moveVector;
        if (IsPositionInGrid(targetPosition)) playerTransform.position = targetPosition;
    }

    bool IsPositionInGrid(Vector2 position)
    {
        return (position.x >= -gridWidth / 2f && position.x < gridWidth / 2f &&
                position.y >= -gridHeight / 2f && position.y < gridHeight / 2f);
    }

    void PrintReport(int currentEpisode)
    {
        if (episodeRewards.Count == 0) return;
        float avgReward = episodeRewards.Average();
        float avgSteps = (float)episodeSteps.Average();
        float winRate = (float)winCount / reportInterval * 100f;

        Debug.Log($"<b>--- BÁO CÁO: Ván {currentEpisode - reportInterval}-{currentEpisode} ---</b>\n" +
                  $"Tỷ lệ thắng: <color=green>{winRate:F2}%</color> | " +
                  $"Phần thưởng TB: <color=yellow>{avgReward:F2}</color> | " +
                  $"Số bước TB: {avgSteps:F2} | " +
                  $"Epsilon: {epsilon:F4}");

        episodeSteps.Clear();
        episodeRewards.Clear();
        winCount = 0;
    }

    #endregion

    //================================================================================
    // --- LƯU & TẢI MODEL ---
    //================================================================================

    #region Save & Load

    private void SaveQTable()
    {
        string path = Path.Combine(Application.persistentDataPath, "qtable_random_env.dat");
        try
        {
            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.WriteLine(epsilon.ToString(CultureInfo.InvariantCulture));
                foreach (var entry in qTable)
                {
                    string values = string.Join(",", entry.Value.Select(v => v.ToString(CultureInfo.InvariantCulture)));
                    writer.WriteLine($"{entry.Key}:{values}");
                }
            }
        }
        catch (System.Exception e) { Debug.LogError("Lỗi khi lưu Bảng Q: " + e.Message); }
    }

    private bool LoadQTable()
    {
        string path = Path.Combine(Application.persistentDataPath, "qtable_random_env.dat");
        if (File.Exists(path))
        {
            try
            {
                qTable.Clear();
                string[] lines = File.ReadAllLines(path);
                if (lines.Length < 1) return false;
                epsilon = float.Parse(lines[0], CultureInfo.InvariantCulture);
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split(':');
                    if (parts.Length != 2) continue;
                    string state = parts[0];
                    string[] valueStrings = parts[1].Split(',');
                    float[] values = new float[actionCount];
                    for (int j = 0; j < valueStrings.Length; j++)
                    {
                        values[j] = float.Parse(valueStrings[j], CultureInfo.InvariantCulture);
                    }
                    qTable[state] = values;
                }
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("Lỗi khi tải Bảng Q, sẽ huấn luyện lại từ đầu: " + e.Message);
                qTable.Clear();
                return false;
            }
        }
        return false;
    }
    #endregion

    // Dán hàm này vào trong script AIManager.cs của bạn

    void OnDrawGizmos()
    {
        // Đặt màu cho các đường lưới (ví dụ: một màu xám bán trong suốt)
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        // Tính toán các đường biên của lưới
        // Giả sử lưới của bạn đối xứng quanh gốc tọa độ (0,0)
        float startX = -gridWidth / 2f;
        float startY = -gridHeight / 2f;
        float endX = gridWidth / 2f;
        float endY = gridHeight / 2f;

        // Vẽ các đường kẻ dọc
        for (int x = 0; x <= gridWidth; x++)
        {
            float xPos = startX + x;
            Vector3 from = new Vector3(xPos, startY, 0);
            Vector3 to = new Vector3(xPos, endY, 0);
            Gizmos.DrawLine(from, to);
        }

        // Vẽ các đường kẻ ngang
        for (int y = 0; y <= gridHeight; y++)
        {
            float yPos = startY + y;
            Vector3 from = new Vector3(startX, yPos, 0);
            Vector3 to = new Vector3(endX, yPos, 0);
            Gizmos.DrawLine(from, to);
        }
    }
}