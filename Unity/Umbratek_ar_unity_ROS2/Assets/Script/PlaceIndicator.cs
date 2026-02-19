using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class PlaceIndicator : MonoBehaviour
{
    private ARRaycastManager raycastManager;
    
    [Header("ここに「白い丸」の画像をドラッグ＆ドロップ")]
    public GameObject reticleVisual; 

    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Start()
    {
        raycastManager = FindObjectOfType<ARRaycastManager>();
        
        // 最初は隠しておく
        if(reticleVisual != null)
        {
            reticleVisual.SetActive(false);
        }
    }

    void Update()
    {
        // レイキャスト（画面中心から床を探す）
        var screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);

        if (raycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            // 自身の位置を床の上に移動
            transform.position = hitPose.position;
            transform.rotation = hitPose.rotation;

            // 白い丸を表示
            if (reticleVisual != null && !reticleVisual.activeInHierarchy)
            {
                reticleVisual.SetActive(true);
            }
        }
        else
        {
            // 床を見失ったら隠す
            if (reticleVisual != null && reticleVisual.activeInHierarchy)
            {
                reticleVisual.SetActive(false);
            }
        }
    }
}