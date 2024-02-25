using UnityEngine;
using UnityEngine.Windows.WebCam;

public class CameraManager : MonoBehaviour
{   
    Camera[] cameras;
    private void Awake() 
    {
        MakeAllCamerasRenderDepth();
    }

    public void MakeAllCamerasRenderDepth()
    {
        cameras = GameObject.FindObjectsOfType<Camera>();
        foreach(Camera cam in cameras)
            cam.depthTextureMode = DepthTextureMode.Depth;
    }
}
