using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARRobotAligner : MonoBehaviour
{
    [Header("AR Settings")]
    [SerializeField] ARTrackedImageManager m_ImageManager;
    
    [Header("Robot Settings")]
    [SerializeField] GameObject m_RobotBaseLink; 
    
    [Header("Offset Settings")]
    [SerializeField] Vector3 m_PositionOffset = new Vector3(0, 0, 0.2f); 
    [SerializeField] Vector3 m_RotationOffset = Vector3.zero;

    void Start()
    {
        // 起動時はまだ位置がわからないので隠しておく
        if (m_RobotBaseLink != null) m_RobotBaseLink.SetActive(false);
    }

    void OnEnable() => m_ImageManager.trackedImagesChanged += OnChanged;
    void OnDisable() => m_ImageManager.trackedImagesChanged -= OnChanged;

    void OnChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // マーカーが追加された（最初に認識した）とき
        foreach (var newImage in eventArgs.added)
        {
            UpdateRobotPosition(newImage);
        }

        // マーカーの位置が更新されたとき
        foreach (var updatedImage in eventArgs.updated)
        {
            // トラッキング中のみ位置を更新する
            if (updatedImage.trackingState == TrackingState.Tracking)
            {
                UpdateRobotPosition(updatedImage);
            }
            // ★重要: ここに「Limitedなら隠す」というコードを書かないことで、
            // 見切れてもロボットは最後の場所に残り続けます。
        }
    }

    void UpdateRobotPosition(ARTrackedImage marker)
    {
        // ロボットが非表示なら表示する
        if (!m_RobotBaseLink.activeSelf) m_RobotBaseLink.SetActive(true);

        Vector3 markerPos = marker.transform.position;
        Quaternion markerRot = marker.transform.rotation;

        Vector3 finalPos = markerPos + (markerRot * m_PositionOffset);
        Quaternion finalRot = markerRot * Quaternion.Euler(m_RotationOffset);

        m_RobotBaseLink.transform.position = finalPos;
        m_RobotBaseLink.transform.rotation = finalRot;
    }
}