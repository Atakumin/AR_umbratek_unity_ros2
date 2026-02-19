using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARObjectPlacer : MonoBehaviour
{
    [Header("AR Settings")]
    [SerializeField] ARRaycastManager m_RaycastManager;

    [Header("Objects to Move")]
    [SerializeField] GameObject m_Target;          // Pick対象 (緑色のキューブなど)
    [SerializeField] GameObject m_TargetPlacement; // Place先 (赤色の印など)

    // タップ回数の管理（0: Target配置待ち, 1: Placement配置待ち, 2: 完了）
    private int tapCount = 0; 

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    void Update()
    {
        // 画面がタップされたか確認
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // タップ開始時のみ処理
            if (touch.phase == TouchPhase.Began)
            {
                // AR平面に対してレイキャスト（光線）を飛ばす
                if (m_RaycastManager.Raycast(touch.position, s_Hits, TrackableType.PlaneWithinPolygon))
                {
                    // 最も近い平面のヒット位置を取得
                    Pose hitPose = s_Hits[0].pose;

                    // 順番に配置場所を更新
                    if (tapCount == 0)
                    {
                        // 1回目のタップ: Target (Pick位置) を移動
                        if (m_Target != null)
                        {
                            m_Target.transform.position = hitPose.position;
                            // 回転は平面に合わせつつ、Y軸回転は維持したい場合は以下のように調整可
                            // m_Target.transform.rotation = hitPose.rotation; 
                            Debug.Log($"Target moved to: {hitPose.position}");
                        }
                        tapCount++;
                    }
                    else if (tapCount == 1)
                    {
                        // 2回目のタップ: TargetPlacement (Place位置) を移動
                        if (m_TargetPlacement != null)
                        {
                            m_TargetPlacement.transform.position = hitPose.position;
                            Debug.Log($"Placement moved to: {hitPose.position}");
                        }
                        tapCount++; 
                    }
                    else
                    {
                        // 3回目以降: 必要ならリセットするか、Targetを再配置するなど仕様に合わせて変更
                        // ここではループさせて最初に戻る設定にしています
                        tapCount = 0;
                        Debug.Log("Resetting tap sequence.");
                    }
                }
            }
        }
    }
}