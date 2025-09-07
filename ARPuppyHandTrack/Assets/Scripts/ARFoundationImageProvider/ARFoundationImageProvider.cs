using Mediapipe;
using Mediapipe.Unity;
using System;
using System.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// ARFoundation의 카메라 영상을 받아 MediaPipe에 전달하는 모든 로직을 담당하는 클래스
public class ARFoundationImageProvider : MonoBehaviour
{
    // [TODO] 이 스크립트를 MediaPipe 그래프를 실행하는 실제 Solution/Runner와 연결해야 합니다.
    // public HandLandmarkerRunner solution;

    [Tooltip("AR 카메라를 제어하는 AR Camera Manager를 연결해주세요. (Hierarchy 창의 XR Origin)")]
    [SerializeField]
    private ARCameraManager _cameraManager;

    // 이미지 변환을 위한 버퍼. 한 번 할당 후 재사용하여 성능을 최적화합니다.
    private NativeArray<byte> _imageBuffer;

    private void OnEnable()
    {
        // AR 카메라의 새 프레임이 들어올 때마다 OnFrameReceived 함수를 호출하도록 등록
        _cameraManager.frameReceived += OnFrameReceived;
    }

    private void OnDisable()
    {
        // 스크립트가 비활성화될 때 등록을 해제하여 오류를 방지
        _cameraManager.frameReceived -= OnFrameReceived;
    }

    private unsafe void OnFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // 1단계: ARFoundation에서 CPU 이미지 가져오기
        if (!_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            return;
        }

        // 2단계: 이미지 형식 변환하기 (XRCpuImage → RGBA)
        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width, image.height),
            outputFormat = TextureFormat.RGBA32,
            // AR Foundation 카메라는 상하 반전되어 있으므로 Y축 미러링
            transformation = XRCpuImage.Transformation.MirrorY
        };

        int size = image.width * image.height * 4;
        
        // 버퍼가 없거나 크기가 다르면 새로 할당
        if (!_imageBuffer.IsCreated || _imageBuffer.Length != size)
        {
            if (_imageBuffer.IsCreated)
            {
                _imageBuffer.Dispose();
            }
            // Allocator.Persistent를 사용하여 프레임 간에 메모리를 유지
            _imageBuffer = new NativeArray<byte>(size, Allocator.Persistent);
        }
        
        // 변환 실행 및 결과물을 버퍼에 저장
        image.Convert(conversionParams, new IntPtr(NativeArrayUnsafeUtility.GetUnsafePtr(_imageBuffer)), _imageBuffer.Length);

        // 중요: 사용한 이미지는 반드시 Dispose 해주어야 메모리 누수를 막을 수 있습니다.
        image.Dispose();
        
        // 3단계: 변환된 데이터를 MediaPipe에 전달하기
        SendToMediaPipe(_imageBuffer, conversionParams.outputDimensions.x, conversionParams.outputDimensions.y);
    }

    // 3단계 구현
    private void SendToMediaPipe(NativeArray<byte> buffer, int width, int height)
    {
        // MediaPipe의 ImageFrame 객체를 생성합니다.
        var imageFrame = new ImageFrame(
            ImageFormat.Types.Format.Srgba,
            width,
            height,
            width * 4, // 한 줄의 바이트 수 (width * 4 bytes for RGBA)
            buffer
        );

        // [TODO] 여기서 생성한 imageFrame을 실제 MediaPipe 그래프(Solution/Runner)의
        //        입력 스트림으로 전달해야 합니다.
        // 예시: graph.AddPacketToInputStream("input_video", Packet.CreateImageFrame(imageFrame));
        // 또는: handLandmarker.DetectAsync(imageFrame, ...);

        // 지금은 디버깅을 위해 로그만 출력합니다.
        // Debug.Log($"ImageFrame created with size {width}x{height}, ready to be sent to MediaPipe.");
    }

    private void OnDestroy()
    {
        // 앱이 종료될 때 할당된 버퍼 메모리를 깨끗하게 해제
        if (_imageBuffer.IsCreated)
        {
            _imageBuffer.Dispose();
        }
    }
}