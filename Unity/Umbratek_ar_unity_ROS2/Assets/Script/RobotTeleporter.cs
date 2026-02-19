using UnityEngine;

public class RobotTeleporter : MonoBehaviour
{
    public ArticulationBody robotRoot;
    private Transform currentTarget;

    void FixedUpdate()
    {
        // ターゲットが見つかっていない場合、探す
        if (currentTarget == null)
        {
            // パターンA：スペースなし
            GameObject foundObj = GameObject.Find("MarkerBeacon(Clone)");
            
            // パターンB：スペースあり（念のためこっちも探す）
            if (foundObj == null)
            {
                foundObj = GameObject.Find("MarkerBeacon (Clone)");
            }

            // パターンC：タグで見つける（タグ設定をしている場合）
            if (foundObj == null)
            {
                foundObj = GameObject.FindGameObjectWithTag("ARMarker");
            }

            // 見つかったら登録
            if (foundObj != null)
            {
                currentTarget = foundObj.transform;
            }
        }

        // ターゲットがあれば移動
        if (currentTarget != null && robotRoot != null)
        {
            robotRoot.TeleportRoot(currentTarget.position, currentTarget.rotation);
        }
    }
}