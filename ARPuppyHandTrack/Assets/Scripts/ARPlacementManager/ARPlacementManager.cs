using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// ARPlacementManager 클래스는 AR 환경에서 객체를 배치하고 호출하는 역할을 담당합니다.
public class ARPlacementManager : MonoBehaviour
{
    [SerializeField]
    private ARRaycastManager arRaycastManager;

    [SerializeField]
    private GameObject puppyPrefab;

    // 종소리를 재생할 AudioSource 컴포넌트입니다.
    [SerializeField]
    private AudioSource bellAudioSource;

    private GameObject spawnedPuppy;
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    /// <summary>
    /// 이 함수는 UI 버튼의 OnClick 이벤트에 연결될 것입니다.
    /// </summary>
    public void OnCallPuppyButtonPressed()
    {
        // 1. 종소리를 재생합니다.
        if (bellAudioSource != null)
        {
            bellAudioSource.Play();
        }

        // 2. 화면 중앙에서 Raycast를 쏩니다.
        Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
        if (arRaycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (spawnedPuppy == null)
            {
                // 3-1. 강아지가 없으면: 새로 소환합니다.
                spawnedPuppy = Instantiate(puppyPrefab, hitPose.position, hitPose.rotation);
            }
            else
            {
                // 3-2. 강아지가 있으면: 해당 위치로 이동시킵니다. (순간이동)
                spawnedPuppy.transform.position = hitPose.position;
                
                // 강아지가 카메라를 바라보도록 방향을 회전시킵니다.
                Vector3 cameraPosition = Camera.main.transform.position;
                Vector3 directionToCamera = cameraPosition - spawnedPuppy.transform.position;
                directionToCamera.y = 0; // 강아지가 눕지 않도록 y축 회전은 고정합니다.
                
                spawnedPuppy.transform.rotation = Quaternion.LookRotation(-directionToCamera);
            }
        }
    }
}