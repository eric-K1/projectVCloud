using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathFollower : MonoBehaviour
{
    public Transform Path;

    [Range(0.01f, 5.0f)]
    [Tooltip("How long to complete one loop (seconds)")]
    public float period = 1.0f;

    private float t = 0.0f;

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
        int N = Path.childCount - 1;

        for(int i = 0; i <= N; i++)
            ret += nChooseK(N, i)  * Mathf.Pow(t, i) * Mathf.Pow(1 - t, N - i) * Path.GetChild(i).position;

        return ret;
    }
    // Update is called once per frame
    void Update()
    {
        t += Time.deltaTime / period;
        if (t > 1.0f)
            t = t % 1.0f;
        this.transform.position = bezierCalc(t);
    }
}
