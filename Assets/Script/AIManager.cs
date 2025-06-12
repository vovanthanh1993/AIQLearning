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
    public float wallDensity = 0.1f;

    [Header("Tham số Huấn luyện")]
    public int totalEpisodes = 50000;
    public int maxStepsPerEpisode = 50;
    public bool enableTraining = true;
    public int reportInterval = 100;

    //--------------------------------------------------------------------------------
    // --- BIẾN NỘI BỘ CỦA AI ---
    //--------------------------------------------------------------------------------

    private Transform goldInstance;
    private List<GameObject> wallInstances = new List<GameObject>();
    private Vector2 playerStartPosition = Vector2.zero;

    private Dictionary<string, float[]> qTable = new Dictionary<string, float[]>();
    private int actionCount = 4; // 0:Lên, 1:Xuống, 2:Trái, 3:Phải

    // Các tham số đã được tinh chỉnh để học tốt hơn
    private float learningRate = 0.05f;
    private float gamma = 0.99f;
    private float epsilon = 1.0f;
    private float epsilonDecay = 0.99995f;
    private float minEpsilon = 0.01f;

    // Biến để thu thập dữ liệu cho báo cáo
    private List<int> episodeSteps = new List<int>();
    private List<float> episodeRewards = new List<float>();
    private int winCount = 0;

    //================================================================================
    // --- VÒNG LẶP CHÍNH CỦA UNITY & AI ---
    //================================================================================

    void Awake()
    {
        Application.runInBackground = true;
    }
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

    // Thay thế toàn bộ IEnumerator TrainingLoop() bằng phiên bản này

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
                string currentState = GetState(playerTransform.position);
                int action = ChooseAction(currentState);

                float distanceBeforeMove = Vector2.Distance(playerTransform.position, goldInstance.position);
                Vector2 oldPos = playerTransform.position;
                MovePlayer(action);

                float reward = 0f;
                bool hitTerminalState = false;

                Collider2D hitCollider = Physics2D.OverlapCircle(playerTransform.position, 0.4f);
                if (hitCollider != null)
                {
                    if (hitCollider.CompareTag("Wall"))
                    {
                        // Đâm vào tường là kết quả tệ nhất
                        reward = -100f;
                        episodeDone = true;
                        hitTerminalState = true;
                        playerTransform.position = oldPos;
                    }
                    else if (hitCollider.CompareTag("Gold"))
                    {
                        // Ăn vàng là kết quả tốt nhất
                        reward = 100f;
                        episodeDone = true;
                        hitTerminalState = true;
                        winCount++;
                    }
                }

                // Kỹ thuật "Reward Shaping"
                if (!hitTerminalState)
                {
                    float distanceAfterMove = Vector2.Distance(playerTransform.position, goldInstance.position);
                    if (distanceAfterMove < distanceBeforeMove)
                    {
                        reward = 1f; // Thưởng nhỏ vì đi đúng hướng
                    }
                    else
                    {
                        reward = -2f; // Phạt nhỏ vì đi sai hướng
                    }
                }

                steps++; // Tăng số bước sau khi di chuyển và tính thưởng

                // Kiểm tra điều kiện hết giờ sau cùng
                if (!episodeDone && steps >= maxStepsPerEpisode)
                {
                    // Hết giờ thì tệ, nhưng vẫn tốt hơn là đâm vào tường
                    reward = -50f;
                    episodeDone = true;
                }

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
            int steps = 0;
            while (!episodeDone && steps < maxStepsPerEpisode)
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
                steps++;
                yield return new WaitForSeconds(0.15f);
            }
            yield return new WaitForSeconds(2f);
        }
    }

    //================================================================================
    // --- LOGIC CỐT LÕI CỦA AI (ĐÃ NÂNG CẤP) ---
    //================================================================================

    string GetState(Vector2 playerPos)
    {
        StringBuilder stateBuilder = new StringBuilder();

        // 1. Nhận thức về tường
        stateBuilder.Append(IsWallAt(playerPos + Vector2.up) ? '1' : '0');
        stateBuilder.Append(IsWallAt(playerPos + Vector2.down) ? '1' : '0');
        stateBuilder.Append(IsWallAt(playerPos + Vector2.left) ? '1' : '0');
        stateBuilder.Append(IsWallAt(playerPos + Vector2.right) ? '1' : '0');

        // 2. Hướng đến vàng
        Vector2 goldPos = goldInstance != null ? (Vector2)goldInstance.position : playerPos;
        stateBuilder.Append(goldPos.y > playerPos.y ? '1' : '0');
        stateBuilder.Append(goldPos.y < playerPos.y ? '1' : '0');
        stateBuilder.Append(goldPos.x < playerPos.x ? '1' : '0');
        stateBuilder.Append(goldPos.x > playerPos.x ? '1' : '0');

        // 3. NÂNG CẤP: Khoảng cách đến vàng
        float distanceToGold = Vector2.Distance(playerPos, goldPos);
        stateBuilder.Append(GetDistanceCategory(distanceToGold));

        return stateBuilder.ToString();
    }

    string GetDistanceCategory(float distance)
    {
        if (distance < 4) return "_NEAR";
        if (distance < 8) return "_MID";
        return "_FAR";
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

    //================================================================================
    // --- QUẢN LÝ GAME & TIỆN ÍCH ---
    //================================================================================

    void ResetEpisode()
    {
        foreach (var wall in wallInstances) if (wall != null) Destroy(wall);
        wallInstances.Clear();
        if (goldInstance != null) Destroy(goldInstance.gameObject);

        for (int x = -gridWidth / 2; x < gridWidth / 2; x++)
        {
            for (int y = -gridHeight / 2; y < gridHeight / 2; y++)
            {
                if (Mathf.Abs(x) < 2 && Mathf.Abs(y) < 2) continue;
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
        return (position.x >= -gridWidth / 2f && position.x < gridWidth / 2f && position.y >= -gridHeight / 2f && position.y < gridHeight / 2f);
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

    //================================================================================
    // --- LƯU & TẢI MODEL ---
    //================================================================================

    // Bên trong hàm SaveQTable()

    private void SaveQTable()
    {
        // Lấy đường dẫn đến màn hình Desktop một cách tự động
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        // Đặt một tên file mới để phân biệt
        string fileName = "qtable_on_desktop.dat";
        // Kết hợp đường dẫn Desktop và tên file
        string path = Path.Combine(desktopPath, fileName);

        Debug.Log("Đang lưu Q-Table ra Desktop tại: " + path);

        try
        {
            // ... (phần còn lại của hàm giữ nguyên) ...
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

    // Bên trong hàm LoadQTable()
    private bool LoadQTable()
    {
        // Lấy đường dẫn đến màn hình Desktop
        string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        string fileName = "qtable_on_desktop.dat";
        string path = Path.Combine(desktopPath, fileName);

        if (File.Exists(path))
        {
            Debug.Log("Tìm thấy model đã lưu trên Desktop. Đang tải...");
            try
            {
                // ... (phần còn lại của hàm giữ nguyên) ...
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
                Debug.Log($"Model đã tải thành công. Có {qTable.Count} trạng thái đã biết.");
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
}