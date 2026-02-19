using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARFreePlacement : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] ARRaycastManager m_RaycastManager;
    [SerializeField] GameObject m_RobotBase; // 動かしたいロボットの根本

    [Header("Settings")]
    [SerializeField] bool m_ShowDebugVisuals = true; // 平面検知の粒々を表示するかどうか

    // 内部変数
    bool m_IsPlaced = false; // 配置が完了したかどうかのフラグ
    List<ARRaycastHit> m_Hits = new List<ARRaycastHit>(); // レイキャストの結果格納用

    void Start()
    {
        // 最初はロボットを非表示にしておく（床が見つかるまで）
        if (m_RobotBase != null) m_RobotBase.SetActive(false);
    }

    void Update()
    {
        // 既に配置済みなら何もしない（ロボットはその場に固定される）
        if (m_IsPlaced) return;

        // 画面の中央
        var screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);

        // 画面中央からレイ（光線）を飛ばして、検出された平面（Plane）に当たるか判定
        if (m_RaycastManager.Raycast(screenCenter, m_Hits, TrackableType.PlaneWithinPolygon))
        {
            // 平面に当たった場合
            Pose hitPose = m_Hits[0].pose;

            // ロボットを表示
            if (m_RobotBase != null)
            {
                if (!m_RobotBase.activeSelf) m_RobotBase.SetActive(true);

                // ロボットの位置をヒットした場所に移動
                m_RobotBase.transform.position = hitPose.position;

                // ロボットの向きを「カメラの方」に向けつつ、傾きは水平を保つ
                Vector3 lookPos = Camera.main.transform.position;
                lookPos.y = hitPose.position.y; // 高さは合わせる（見上げ/見下ろし回転を防ぐ）
                m_RobotBase.transform.LookAt(lookPos);
                
                // ※もしロボットが180度裏を向く場合は以下を使ってください
                // m_RobotBase.transform.LookAt(2 * m_RobotBase.transform.position - lookPos);
            }
        }
    }

    void OnGUI()
    {
        // 配置が完了していない場合のみ「決定ボタン」を表示
        if (!m_IsPlaced)
        {
            float buttonHeight = Screen.height * 0.1f;
            float buttonWidth = Screen.width * 0.4f;
            
            // 画面下部にボタン配置
            Rect rect = new Rect(
                (Screen.width - buttonWidth) / 2, 
                Screen.height - buttonHeight - 50, 
                buttonWidth, 
                buttonHeight
            );

            // スタイル調整（文字大きく）
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.fontSize = 30;

            if (GUI.Button(rect, "ここに配置 (Place)", style))
            {
                PlaceRobot();
            }

            // ユーザーへのガイド表示
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 40;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.normal.textColor = Color.white; // 背景が明るいと見にくいので必要なら調整
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 100), "床を映して\nロボットを表示させてください", labelStyle);
        }
        else
        {
            // 配置済みなら「再配置」ボタンを小さく表示（やり直し用）
            if (GUI.Button(new Rect(20, 20, 200, 80), "再配置 (Reset)"))
            {
                m_IsPlaced = false;
                // 平面表示を復活させたい場合はここで制御
            }
        }
    }

    // 決定ボタンが押されたときの処理
// 決定ボタンが押されたときの処理
    public void PlaceRobot()
    {
        m_IsPlaced = true;

        // ★修正ポイント: 物理エンジンの位置情報を強制的に更新する
        // ArticulationBodyがついている場合、transformを変えただけでは
        // 次の瞬間に元の場所に戻されることがあります。
        ArticulationBody body = m_RobotBase.GetComponent<ArticulationBody>();
        
        // もしルートにArticulationBodyがなければ、子供（Utra6など）にあるかもしれないので探す
        if (body == null) body = m_RobotBase.GetComponentInChildren<ArticulationBody>();

        if (body != null)
        {
            // 現在の見た目の位置・回転に、物理演算の「根っこ」をテレポートさせる
            body.TeleportRoot(m_RobotBase.transform.position, m_RobotBase.transform.rotation);
        }

        // オプション：配置が決まったら、邪魔な平面（床の粒々）を非表示にする
        if (!m_ShowDebugVisuals)
        {
            TogglePlaneVisuals(false);
        }
    }

    void TogglePlaneVisuals(bool active)
    {
        var planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager != null)
        {
            planeManager.enabled = active; // 新規検出を停止
            foreach (var plane in planeManager.trackables)
            {
                plane.gameObject.SetActive(active); // 既存の床を非表示
            }
        }
    }
}
