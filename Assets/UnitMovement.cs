using UnityEngine;

public class UnitMovement : MonoBehaviour
{
    void Update()
    {
        transform.position += Vector3.right * 2f * Time.deltaTime;
    }
}
