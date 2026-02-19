using UnityEngine;
using UnityEngine.XR.ARFoundation; // ARAnchor削除用

public class PlaceManager : MonoBehaviour
{
    [Header("ロボットのPrefab")]
    public GameObject robotPrefab; 

    private PlaceIndicator placeIndicator;
    private GameObject spawnedRobot; // 現在出ているロボット

    void Start()
    {
        placeIndicator = FindObjectOfType<PlaceIndicator>();
    }

    void OnGUI()
    {
        // --- スマホ画面対応（ボタンサイズ自動調整） ---
        float scale = Screen.width / 1080f; 
        if (scale < 1) scale = 1;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button);
        btnStyle.fontSize = 40;

        float w = 360f;
        float h = 120f;
        float x = (Screen.width / scale - w) / 2; // 横中央
        float y = (Screen.height / scale) - h - 100; // 下から少し浮かす

        // ボタン押下時の処理
        if (GUI.Button(new Rect(x, y, w, h), "PLACE / MOVE", btnStyle))
        {
            UpdateRobotPosition();
        }
    }

    void UpdateRobotPosition()
    {
        // マーカーが見えていない（床認識なし）なら何もしない
        if (placeIndicator == null || !placeIndicator.reticleVisual.activeSelf) return;

        // 目標地点を取得
        Vector3 targetPos = placeIndicator.transform.position;
        Quaternion targetRot = placeIndicator.transform.rotation;

        // ★最強の解決策: 古いロボットがあれば破壊して消す
        if (spawnedRobot != null)
        {
            Destroy(spawnedRobot);
        }

        // ★新しい位置に新品を生成する（これで位置更新トラブルは100%解決）
        spawnedRobot = Instantiate(robotPrefab, targetPos, targetRot);

        // --- 向きとレーザーの調整 ---
        // カメラの方を向かせる
        Vector3 cameraPos = Camera.main.transform.position;
        Vector3 lookPos = new Vector3(cameraPos.x, spawnedRobot.transform.position.y, cameraPos.z);
        spawnedRobot.transform.LookAt(lookPos);
        spawnedRobot.transform.Rotate(0, 180, 0); // モデルが後ろ向きなら180、横なら90を入れる

        // レーザーを描画
        UpdateLaser(spawnedRobot);
    }

    void UpdateLaser(GameObject robot)
    {
        LineRenderer lr = robot.GetComponentInChildren<LineRenderer>();
        if (lr != null)
        {
            // ロボットの足元から少し上
            Vector3 start = robot.transform.position + Vector3.up * 0.5f;
            // ずっと下
            Vector3 end = start + Vector3.down * 5.0f;
            
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }
    }
}