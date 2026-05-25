using System.Collections.Generic;
using Mediapipe.Tasks.Components.Containers;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class HandTrackingPuppyController : MonoBehaviour
{
    // Inspector에서 연결: AR 카메라 이미지를 MediaPipe로 넘기는 컴포넌트
    [SerializeField] private ARFoundationImageProvider _imageProvider;
    // Inspector에서 연결: 강아지 소환/참조용 (SpawnedPuppy 프로퍼티 사용)
    [SerializeField] private ARPlacementManager _placementManager;
    // Inspector에서 연결: 손목 위치로 AR 평면 Raycast 하기 위한 컴포넌트
    [SerializeField] private ARRaycastManager _raycastManager;
    // 강아지가 손목 목표 지점으로 이동하는 속도 (m/s)
    [SerializeField] private float _moveSpeed = 3f;

    private readonly List<ARRaycastHit> _hits = new();

    // 백그라운드 스레드(MediaPipe)와 메인 스레드(Update) 간 데이터 공유용 락
    private readonly object _resultLock = new();
    // 가장 최근 감지된 손목의 화면 좌표 (픽셀)
    private Vector2 _latestWristScreenPos;
    // Update가 아직 처리하지 않은 새 결과가 있으면 true
    private bool _hasNewResult;
    // 백그라운드에서 감지된 제스처명 (Update에서 읽고 null로 초기화)
    private string _pendingGesture;

    // 현재 화면에 표시 중인 제스처명 (메인 스레드 전용)
    private string _displayedGesture;
    // 이 시간 이후에는 제스처 텍스트를 화면에서 숨김
    private float _gestureClearTime;

    private void OnEnable()
    {
        // AR 카메라 프레임마다 MediaPipe 결과를 받도록 이벤트 구독
        _imageProvider.OnHandLandmarksDetected += OnHandLandmarksDetected;
    }

    private void OnDisable()
    {
        _imageProvider.OnHandLandmarksDetected -= OnHandLandmarksDetected;
    }

    // ※ MediaPipe 백그라운드 스레드에서 호출됨 — UnityEngine API 직접 호출 금지
    // (Screen.width/height는 Unity 2020+ 에서 스레드 안전하게 캐싱되어 예외적으로 허용)
    private void OnHandLandmarksDetected(HandLandmarkerResult result)
    {
        if (result.handLandmarks == null || result.handLandmarks.Count == 0) return;

        var lm = result.handLandmarks[0].landmarks; // 첫 번째 손의 21개 랜드마크
        var wrist = lm[0]; // 랜드마크 0번 = 손목

        // MediaPipe y좌표는 위→아래(0→1), Unity 화면은 아래→위이므로 y를 반전
        var wristScreenPos = new Vector2(wrist.x * Screen.width, (1f - wrist.y) * Screen.height);
        string gesture = DetectGesture(lm);

        lock (_resultLock)
        {
            _latestWristScreenPos = wristScreenPos;
            _hasNewResult = true;
            // 인식된 제스처가 있을 때만 덮어씀 (없으면 이전 값 유지)
            if (gesture != "") _pendingGesture = gesture;
        }
    }

    // MediaPipe 손 랜드마크 인덱스 (각 손가락의 관절):
    //   0  = 손목(Wrist)
    //   1~ 4 = 엄지(Thumb)  CMC→MCP→IP→TIP
    //   5~ 8 = 검지(Index)  MCP→PIP→DIP→TIP
    //   9~12 = 중지(Middle) MCP→PIP→DIP→TIP
    //  13~16 = 약지(Ring)   MCP→PIP→DIP→TIP
    //  17~20 = 새끼(Pinky)  MCP→PIP→DIP→TIP
    private string DetectGesture(List<NormalizedLandmark> lm)
    {
        // 각 손가락이 펴져 있는지 (TIP이 MCP보다 손목에서 충분히 멀면 '폈다'고 판단)
        bool index  = IsExtended(lm[8],  lm[5],  lm[0]); // 검지
        bool middle = IsExtended(lm[12], lm[9],  lm[0]); // 중지
        bool ring   = IsExtended(lm[16], lm[13], lm[0]); // 약지
        bool pinky  = IsExtended(lm[20], lm[17], lm[0]); // 새끼

        // 4가지 손가락 모두 펴면 → 안녕 👋
        if (index && middle && ring && pinky)     return "안녕";
        // 검지만 펴고 나머지 접으면 → 빵야! 🔫
        if (index && !middle && !ring && !pinky)  return "빵야!";
        // 4가지 모두 접으면 → 잼잼 ✊
        if (!index && !middle && !ring && !pinky) return "잼잼";
        return "";
    }

    // TIP(손가락 끝)이 MCP(손가락 관절)보다 손목 기준 3D 거리로 1.5배 이상 멀면 '폈다'고 판단.
    // 3D 거리를 사용하는 이유: 손이 카메라를 향해 수평으로 뻗을 때
    // x·y 좌표 변화는 거의 없고 z(깊이)로만 달라지기 때문.
    // 인식이 잘 안 되면 1.5f 값을 낮춰서 조정 (낮을수록 감도 ↑)
    private bool IsExtended(NormalizedLandmark tip, NormalizedLandmark mcp, NormalizedLandmark wrist)
    {
        float tipDist = Dist3D(tip, wrist);
        float mcpDist = Dist3D(mcp, wrist);
        return tipDist > mcpDist * 1.5f;
    }

    private float Dist3D(NormalizedLandmark a, NormalizedLandmark b)
    {
        float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private void Update()
    {
        Vector2 wristScreenPos;
        string gesture;

        // 락 안에서 최소한의 작업만 (복사 후 즉시 해제)
        lock (_resultLock)
        {
            if (!_hasNewResult) return;
            _hasNewResult = false;
            wristScreenPos = _latestWristScreenPos;
            gesture = _pendingGesture;
            _pendingGesture = null;
        }

        // 새 제스처가 있으면 표시 시작, 2초 뒤 자동으로 사라짐
        if (gesture != null)
        {
            _displayedGesture = gesture;
            _gestureClearTime = Time.time + 2f;
        }

        // 강아지가 소환되지 않았으면 이동 처리 건너뜀
        var puppy = _placementManager.SpawnedPuppy;
        if (puppy == null) return;

        // 손목 화면 좌표로 AR 평면에 Raycast해서 3D 목표 지점을 구함
        if (!_raycastManager.Raycast(wristScreenPos, _hits, TrackableType.PlaneWithinPolygon)) return;

        Vector3 target = _hits[0].pose.position;

        // 강아지를 목표 지점으로 일정 속도로 이동
        puppy.transform.position = Vector3.MoveTowards(
            puppy.transform.position, target, _moveSpeed * Time.deltaTime);

        // 이동 방향을 바라보도록 회전 (y축만 사용해 수평 회전만 적용)
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

    private void OnGUI()
    {
        // 표시할 제스처가 없거나 표시 시간이 지났으면 아무것도 그리지 않음
        if (string.IsNullOrEmpty(_displayedGesture) || Time.time > _gestureClearTime) return;

        var style = new GUIStyle(GUI.skin.box)
        {
            // 화면 높이 기준 비율로 폰트 크기 결정 (해상도 독립적)
            fontSize = Mathf.RoundToInt(Screen.height * 0.07f),
            alignment = TextAnchor.MiddleCenter
        };
        style.normal.textColor = Color.yellow;

        float w = Screen.width * 0.5f;
        float h = Screen.height * 0.12f;
        // 화면 가로 중앙, 세로 35% 지점에 반투명 박스로 표시
        GUI.Box(new UnityEngine.Rect((Screen.width - w) / 2, Screen.height * 0.35f, w, h), _displayedGesture, style);
    }
}
