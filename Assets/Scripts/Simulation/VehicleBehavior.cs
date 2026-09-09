using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using TMPro;

public class VehicleBehavior : MonoBehaviour
{
    public GameObject m_Vehicle;
    public float progress = 0f;
    public float speed = 0.1f;
    public int load = 0;
    public int capacity = 8;
    public TextMeshProUGUI loadText;

    


    
    
    
    public SplineContainer path;
    void Start()
    {
        loadText.text = "0/" + capacity.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Corn"))
        {
            other.gameObject.SetActive(false);
            load++;
            loadText.text = load.ToString() + "/" + capacity.ToString();
        }
    }

}
