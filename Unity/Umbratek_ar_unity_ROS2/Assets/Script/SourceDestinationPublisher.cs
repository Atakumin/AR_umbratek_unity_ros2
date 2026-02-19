using System;
using System.Collections;
using System.Linq;
using RosMessageTypes.Geometry;
using RosMessageTypes.UtMsg; 
using RosMessageTypes.Std; 
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

public class SourceDestinationPublisher : MonoBehaviour
{
    const int k_NumRobotJoints = 6;
    public static readonly string[] LinkNames =
        { "world/base_link/link_1", "/link_2", "/link_3", "/link_4", "/link_5", "/link_6" };

    [Header("ROS Settings")]
    [SerializeField] string m_ServiceName = "ut_msg/MoverService";
    [SerializeField] string m_ExecuteTopicName = "/ut_msg/ExecuteTrajectory";

    [Header("Scene References")]
    [SerializeField] GameObject m_BaseLink; 
    [SerializeField] GameObject m_Utra6;
    [SerializeField] GameObject m_Target;
    [SerializeField] GameObject m_TargetPlacement;

    ArticulationBody[] m_Joints;
    ROSConnection m_Ros;

    // パフォーマンス計測用変数
    private float m_PlanRequestStartTime;
    private float m_LastTotalDelay = 0f; // 前回の合計遅延時間

    readonly Quaternion m_PickOrientation = Quaternion.Euler(90, 90, 0);
    readonly Vector3 m_PickPoseOffset = Vector3.up * 0.1f;
    string m_StatusMessage = "Ready"; 

    void Start()
    {
        m_Ros = ROSConnection.GetOrCreateInstance();
        m_Ros.RegisterRosService<MoverServiceRequest, MoverServiceResponse>(m_ServiceName);
        m_Ros.RegisterRosService<RosMessageTypes.Std.SetBoolRequest, RosMessageTypes.Std.SetBoolResponse>(m_ExecuteTopicName);

        m_Joints = new ArticulationBody[k_NumRobotJoints];
        string path = "";
        for (var i = 0; i < k_NumRobotJoints; i++)
        {
            path += LinkNames[i];
            var t = m_Utra6.transform.Find(path);
            if(t == null) Debug.LogError($"リンクが見つかりません: {path}");
            else m_Joints[i] = t.GetComponent<ArticulationBody>();
        }
    }

void OnGUI()
{
    // --- レイアウト設定 ---
    float buttonHeight = Screen.height * 0.08f; 
    float areaWidth = Screen.width * 0.7f;      
    float spacing = Screen.height * 0.02f;     
    float labelHeight = buttonHeight * 0.7f;    
    float areaHeight = (buttonHeight * 2) + labelHeight + (spacing * 3);

    float x = (Screen.width - areaWidth) / 2;
    float y = Screen.height - areaHeight - (Screen.height * 0.03f);

    // --- 全体のフォントスタイル設定 ---
    // ボタンの文字サイズを「高さの半分以上」に引き上げ、太字にします
    GUI.skin.button.fontSize = (int)(buttonHeight * 0.55f); 
    GUI.skin.button.fontStyle = FontStyle.Bold;

    GUILayout.BeginArea(new Rect(x, y, areaWidth, areaHeight));

    // --- 1. ステータス表示 ---
    GUIStyle labelStyle = new GUIStyle(GUI.skin.box);
    labelStyle.fontSize = (int)(labelHeight * 0.5f); // ラベルの文字も大きく
    labelStyle.alignment = TextAnchor.MiddleCenter;
    labelStyle.fontStyle = FontStyle.Bold;
    
    if (m_StatusMessage.Contains("Success")) labelStyle.normal.textColor = Color.green;
    else if (m_StatusMessage.Contains("Failed")) labelStyle.normal.textColor = Color.red;
    else labelStyle.normal.textColor = Color.white;

    string displayInfo = m_LastTotalDelay > 0 ? $"{m_StatusMessage} [{m_LastTotalDelay:F2}s]" : m_StatusMessage;
    GUILayout.Label(displayInfo, labelStyle, GUILayout.Height(labelHeight));
    
    GUILayout.Space(spacing);

    // --- 2. Plan ボタン ---
    GUI.backgroundColor = new Color(0.2f, 0.6f, 1.0f);
    if (GUILayout.Button("1. PLAN", GUILayout.Height(buttonHeight)))
    {
        m_StatusMessage = "Planning...";
        PublishPlanRequest();
    }

    GUILayout.Space(spacing);

    // --- 3. Execute ボタン ---
    GUI.backgroundColor = (m_StatusMessage == "Success!") ? new Color(1.0f, 0.5f, 0.0f) : new Color(0.5f, 0.3f, 0.2f);
    if (GUILayout.Button("2. EXECUTE", GUILayout.Height(buttonHeight)))
    {
        m_StatusMessage = "Executing...";
        PublishExecuteTrigger();
    }

    GUI.backgroundColor = Color.white;
    GUILayout.EndArea();
}

       public void PublishPlanRequest()
    {
        // 計測開始
        m_PlanRequestStartTime = Time.realtimeSinceStartup;

        var request = new MoverServiceRequest();
        request.joints_input = new Utra6MoveitJointsMsg();
        request.joints_input.joints = new double[k_NumRobotJoints];
        for (var i = 0; i < k_NumRobotJoints; i++) 
            request.joints_input.joints[i] = m_Joints[i].jointPosition[0]; 

        request.pick_pose = CalculatePose(
            m_Target.transform.position + m_PickPoseOffset,
            Quaternion.Euler(90, m_Target.transform.eulerAngles.y, 0)
        );

        request.place_pose = CalculatePose(
            m_TargetPlacement.transform.position + m_PickPoseOffset,
            m_PickOrientation
        );
        
        m_Ros.SendServiceMessage<MoverServiceResponse>(m_ServiceName, request, VisualizeTrajectory);
    }

    void VisualizeTrajectory(MoverServiceResponse response)
    {
        // 計測終了
        m_LastTotalDelay = Time.realtimeSinceStartup - m_PlanRequestStartTime;

        if (response.trajectories.Length >= 4)
        {
            m_StatusMessage = "Success!";
            StartCoroutine(AnimateRobot(response));
        }
        else 
        {
            m_StatusMessage = "Planning Failed";
        }
    }

    // --- 以下、既存のロジック ---

    PoseMsg CalculatePose(Vector3 targetWorldPos, Quaternion targetWorldRot)
    {
        if (m_BaseLink == null) return new PoseMsg();
        Vector3 localPos = m_BaseLink.transform.InverseTransformPoint(targetWorldPos);
        Quaternion localRot = Quaternion.Inverse(m_BaseLink.transform.rotation) * targetWorldRot;
        return new PoseMsg { position = localPos.To<FLU>(), orientation = localRot.To<FLU>() };
    }

    public void PublishExecuteTrigger()
    {
        var req = new RosMessageTypes.Std.SetBoolRequest { data = true };
        m_Ros.SendServiceMessage<RosMessageTypes.Std.SetBoolResponse>(m_ExecuteTopicName, req, (resp) => 
        {
            m_StatusMessage = resp.success ? "Execution Success" : "Execution Failed";
        });
    }

    IEnumerator AnimateRobot(MoverServiceResponse response)
    {
        for (int step = 0; step < response.trajectories.Length; step++)
        {
            var trajectory = response.trajectories[step];
            int numberOfPoints = trajectory.trajectory.points.Length;
            for (int i = 0; i < numberOfPoints; i++)
            {
                var point = trajectory.trajectory.points[i];
                for (int j = 0; j < k_NumRobotJoints; j++)
                {
                    var drive = m_Joints[j].xDrive;
                    drive.target = (float)point.positions[j] * Mathf.Rad2Deg;
                    m_Joints[j].xDrive = drive;
                }
                yield return new WaitForSeconds(0.05f); 
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
}