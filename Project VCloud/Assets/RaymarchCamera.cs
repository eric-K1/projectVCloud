using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Camera))]
[ExecuteInEditMode]
public class RaymarchCamera : MonoBehaviour
{
    [SerializeField]
    private Shader shader;

    public Material raymarchMaterial 
    {
        get
        {
            if (!raymarchMat && shader)
            {
                raymarchMat = new Material(shader);
                raymarchMat.hideFlags = HideFlags.HideAndDontSave;
            }
            return raymarchMat;
        }
    }

    private Material raymarchMat;

    public Camera camera
    {
        get
        {
            if(!cam)
            {
                cam = GetComponent<Camera>();
            }
            return cam;
        }
    }

    private Camera cam;


    public void OnRenderImage(RenderTexture source, RenderTexture dest)
    {
        if(!raymarchMat)
        {
            Graphics.Blit(source, dest);
            return;
        }

        raymarchMaterial.SetMatrix("CamFrustum", CamFrustum(camera));
        raymarchMaterial.SetMatrix("CamToWorld", camera.cameraToWorldMatrix);
        raymarchMaterial.SetVector("CamWorldSpace", camera.transform.position);

        RenderTexture.active = dest;
        GL.PushMatrix();
        GL.LoadOrtho();
        raymarchMaterial.SetPass(0);
        GL.Begin(GL.QUADS);

        //BL
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 3.0f);
        
        //BR
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 2.0f);
        
        //TR
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(1.0f, 1.0f, 1.0f);
        
        //TL
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f);

        GL.End();
        GL.PopMatrix();
    }

    private Matrix4x4 CamFrustum(Camera camera)
    {
        Matrix4x4 frustum = Matrix4x4.identity;
        float fov = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

        Vector3 up = Vector3.up * fov;
        Vector3 right = Vector3.right * fov * camera.aspect;

        Vector3 topLeft     = - Vector3.forward - right + up;
        Vector3 topRight    = - Vector3.forward + right + up;
        Vector3 bottomLeft  = - Vector3.forward - right - up;
        Vector3 bottomRight = - Vector3.forward + right - up;

        frustum.SetRow(0, topLeft);
        frustum.SetRow(1, topRight);
        frustum.SetRow(2, bottomRight);
        frustum.SetRow(3, bottomLeft);

        return frustum;
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
