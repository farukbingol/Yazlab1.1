using UnityEngine;


public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 8f, -11f);
    public float followSharpness = 5f;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 targetPos = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPos, followSharpness * Time.deltaTime);
    }
}
