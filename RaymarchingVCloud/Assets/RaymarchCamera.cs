using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class RaymarchCamera : SceneViewFilter
{
    [SerializeField]
    private Shader _shader;

    public Material RaymarchMaterial
    {
        get
        {
            if(!_raymarchMat && _shader)
            {
                _raymarchMat = new Material(_shader);
                _raymarchMat.hideFlags = HideFlags.HideAndDontSave;
            }

            return _raymarchMat;
        }
    }

    private Material _raymarchMat;

    public Camera Cam
    {
        get
        {
            if(!_cam)
            {
                _cam = GetComponent<Camera>();
            }
            return _cam;
        }
    }

    private Camera _cam;

    public float maximumRenderDistance;
    public float cloudLayerBottomHeight;
    public float cloudLayerTopHeight;

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if(!RaymarchMaterial)
        {
            Graphics.Blit(src, dest);
            return;
        }

        RaymarchMaterial.SetMatrix("_CamFrustum", CalculateCameraFrustum(Cam));
        RaymarchMaterial.SetMatrix("_CamToWorldMatrix", Cam.cameraToWorldMatrix);
        RaymarchMaterial.SetFloat("_maxDistance", maximumRenderDistance);
        RaymarchMaterial.SetVector("_CloudLayerHeight", new Vector4(cloudLayerBottomHeight, cloudLayerTopHeight, 0, 0));

        RenderTexture.active = dest;
        RaymarchMaterial.SetTexture("_MainTex", src);

        GL.PushMatrix();
        GL.LoadOrtho();
        RaymarchMaterial.SetPass(0);

        GL.Begin(GL.QUADS);
        
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 3.0f);
        
        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 2.0f);
        
        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 1.0f);
        
        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f);

        GL.End();
        GL.PopMatrix();
    }

    private static Matrix4x4 CalculateCameraFrustum(Camera camera)
    {
        Matrix4x4 frustum = Matrix4x4.identity;
        float fov = Mathf.Tan( (camera.fieldOfView / 2.0f) * Mathf.Deg2Rad );
        
        Vector3 up = Vector3.up * fov;
        Vector3 right = Vector3.right * fov * camera.aspect;

        Vector3 topLeftCorner =     -Vector3.forward + up - right;
        Vector3 topRightCorner =    -Vector3.forward + up + right;
        Vector3 bottomLeftCorner =  -Vector3.forward - up - right;
        Vector3 bottomRightCorner = -Vector3.forward - up + right;

        frustum.SetRow(0, topLeftCorner);
        frustum.SetRow(1, topRightCorner);
        frustum.SetRow(2, bottomRightCorner);
        frustum.SetRow(3, bottomLeftCorner);

        return frustum;
    }
}
