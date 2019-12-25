using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathDrawer : MonoBehaviour
{
    public Transform[] controlPoints;

    public bool Enabled = true;

    [Range(0.01f, 0.5f)]
    public float interval = 0.1f;

    private float nChooseK(int N, int K)
    {
        float result = 1;
        
        if(N - K < K)
            K = N - K;

        for (int i = 1; i <= K; i++)
        {
            result *= N - (K - i);
            result /= i;
        }
        //Debug.Log(result);
        return result;
    }

    private Vector3 bezierCalc(float t)
    {
        Vector3 ret = Vector3.zero;
        int N = controlPoints.Length - 1;

        for(int i = 0; i <= N; i++){
            ret += nChooseK(N, i)  * Mathf.Pow(t, i) * Mathf.Pow(1 - t, N - i) * controlPoints[i].position;}

        return ret;
    }
   private void OnDrawGizmos()
   {
       if(!Enabled)
            return;

       for(float t = 0.0f; t <= 1.0f; t += interval)
       {
           Gizmos.DrawSphere(bezierCalc(t), 0.1f);
       }
   }
}
