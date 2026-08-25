using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class VehicleBehavior : MonoBehaviour
{
    public GameObject m_Vehicle;
    public float progress = 0f;
    public float speed = 0.1f;

    
    
    
    public SplineContainer path;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        progress += speed * Time.deltaTime;

        if (progress > 1f)
            progress = 1f;

        Vector3 position = path.EvaluatePosition(progress);
        Vector3 direction = path.EvaluateTangent(progress);

        transform.position = position;

        if (direction != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(direction);
        
    }

}
