using System.Collections.Generic;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class HandTrackingPuppyController : MonoBehaviour
{
    [SerializeField] private ARFoundationImageProvider _imageProvider;
    [SerializeField] private ARPlacementManager _placementManager;
    [SerializeField] private ARRaycastManager _raycastManager;
    [SerializeField] private float _moveSpeed = 3f;

    private readonly List<ARRaycastHit> _hits = new();
    private readonly object _resultLock = new();
    private Vector2 _latestWristScreenPos;
    private bool _hasNewResult;

    private void OnEnable()
    {
        _imageProvider.OnHandLandmarksDetected += OnHandLandmarksDetected;
    }

    private void OnDisable()
    {
        _imageProvider.OnHandLandmarksDetected -= OnHandLandmarksDetected;
    }

    // MediaPipe 백그라운드 스레드에서 호출됨 — Unity API 사용 금지
    private void OnHandLandmarksDetected(HandLandmarkerResult result)
    {
        if (result.handLandmarks == null || result.handLandmarks.Count == 0) return;

        var wrist = result.handLandmarks[0].landmarks[0];
        lock (_resultLock)
        {
            _latestWristScreenPos = new Vector2(wrist.x * Screen.width, (1f - wrist.y) * Screen.height);
            _hasNewResult = true;
        }
    }

    private void Update()
    {
        Vector2 wristScreenPos;
        lock (_resultLock)
        {
            if (!_hasNewResult) return;
            _hasNewResult = false;
            wristScreenPos = _latestWristScreenPos;
        }

        var puppy = _placementManager.SpawnedPuppy;
        if (puppy == null) return;

        if (!_raycastManager.Raycast(wristScreenPos, _hits, TrackableType.PlaneWithinPolygon)) return;

        Vector3 target = _hits[0].pose.position;

        puppy.transform.position = Vector3.MoveTowards(
            puppy.transform.position, target, _moveSpeed * Time.deltaTime);

        Vector3 dir = target - puppy.transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.0001f)
        {
            puppy.transform.rotation = Quaternion.Lerp(
                puppy.transform.rotation,
                Quaternion.LookRotation(dir),
                _moveSpeed * 2f * Time.deltaTime);
        }
    }
}
