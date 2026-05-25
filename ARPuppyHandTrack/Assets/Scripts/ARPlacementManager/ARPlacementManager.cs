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
    public GameObject SpawnedPuppy => spawnedPuppy;
    private List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private string _statusMessage = "";
    private float _statusClearTime;

    public void OnCallPuppyButtonPressed()
    {
        if (bellAudioSource != null)
            bellAudioSource.Play();

        Vector2 screenCenter = new Vector2(Screen.width / 2, Screen.height / 2);
        if (arRaycastManager.Raycast(screenCenter, hits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (spawnedPuppy == null)
            {
                spawnedPuppy = Instantiate(puppyPrefab, hitPose.position, hitPose.rotation);
                ShowStatus("강아지 소환!");
            }
            else
            {
                spawnedPuppy.transform.position = hitPose.position;

                Vector3 cameraPosition = Camera.main.transform.position;
                Vector3 directionToCamera = cameraPosition - spawnedPuppy.transform.position;
                directionToCamera.y = 0;
                spawnedPuppy.transform.rotation = Quaternion.LookRotation(-directionToCamera);
                ShowStatus("강아지 이동!");
            }
        }
        else
        {
            ShowStatus("바닥 감지 실패 — 바닥을 향해 천천히 스캔하세요");
        }
    }

    private void ShowStatus(string message)
    {
        _statusMessage = message;
        _statusClearTime = Time.time + 3f;
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(_statusMessage) || Time.time > _statusClearTime) return;

        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = Mathf.RoundToInt(Screen.height * 0.04f),
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = Color.white;

        float w = Screen.width * 0.8f;
        float h = Screen.height * 0.1f;
        GUI.Box(new Rect((Screen.width - w) / 2, Screen.height * 0.15f, w, h), _statusMessage, style);
    }
}