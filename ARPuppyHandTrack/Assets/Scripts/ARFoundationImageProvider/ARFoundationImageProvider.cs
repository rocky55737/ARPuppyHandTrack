using System;
using System.Collections;
using Mediapipe;
using Mediapipe.Tasks.Vision.HandLandmarker;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.HandLandmarkDetection;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

using Stopwatch = System.Diagnostics.Stopwatch;

public class ARFoundationImageProvider : MonoBehaviour
{
    [SerializeField] private ARCameraManager _cameraManager;

    private NativeArray<byte> _imageBuffer;
    private HandLandmarker _handLandmarker;
    private readonly Stopwatch _stopwatch = new();
    private readonly HandLandmarkDetectionConfig _config = new();

    public event Action<HandLandmarkerResult> OnHandLandmarksDetected;

    private IEnumerator Start()
    {
        var bootstrapObj = GameObject.Find("Bootstrap");
        if (bootstrapObj == null)
        {
            Debug.LogError("Bootstrap not found in scene");
            yield break;
        }

        yield return new WaitUntil(() => bootstrapObj.GetComponent<Bootstrap>().isFinished);
        yield return AssetLoader.PrepareAssetAsync(_config.ModelPath);

        var options = _config.GetHandLandmarkerOptions(OnHandLandmarkDetectionOutput);
        _handLandmarker = HandLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
        _stopwatch.Start();
    }

    private void OnEnable()
    {
        _cameraManager.frameReceived += OnFrameReceived;
    }

    private void OnDisable()
    {
        _cameraManager.frameReceived -= OnFrameReceived;
    }

    private void OnFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (_handLandmarker == null) return;

        if (!_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            return;

        int width = image.width;
        int height = image.height;

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, width, height),
            outputDimensions = new Vector2Int(width, height),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        int size = width * height * 4;
        if (!_imageBuffer.IsCreated || _imageBuffer.Length != size)
        {
            if (_imageBuffer.IsCreated) _imageBuffer.Dispose();
            _imageBuffer = new NativeArray<byte>(size, Allocator.Persistent);
        }

        image.Convert(conversionParams, _imageBuffer);
        image.Dispose();

        var mpImage = new Image(ImageFormat.Types.Format.Srgba, width, height, width * 4, _imageBuffer);
        _handLandmarker.DetectAsync(mpImage, _stopwatch.ElapsedTicks / TimeSpan.TicksPerMillisecond);
    }

    private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
    {
        OnHandLandmarksDetected?.Invoke(result);
    }

    private void OnDestroy()
    {
        _handLandmarker?.Close();
        if (_imageBuffer.IsCreated) _imageBuffer.Dispose();
    }
}
