using UnityEngine;

public class RandomizeObstacles : MonoBehaviour
{
    public GameObject prefabReemplazo;
    public float probabilidadAparicion = 0.7f; 

    
    
    void Start()
    {   
        int indexRand = Random.Range(0, transform.childCount);
        Transform targetMaiz = transform.GetChild(indexRand);

        GameObject obstaculo = Instantiate(
            prefabReemplazo,
            targetMaiz.position,
            targetMaiz.rotation,
            transform
        );

        obstaculo.name = prefabReemplazo.name;
        Destroy(targetMaiz.gameObject);
        
    }

    void Update()
    {
        
    }
}
