using System.Collections.Generic;
using UnityEngine;

public class GlobalObstacleManager : MonoBehaviour
{
    public GameObject prefabReemplazo;
    
    [Range(0f, 1f)]
    public float probabilidadAparicion = 0.7f;
    public int maxArbolesTotales = 5;

    void Start()
    {   
        List<Transform> todosLosCampos = new List<Transform>();
        foreach (Transform soil in transform)
        {
            foreach (Transform CornField in soil)
            {
                if (CornField.childCount > 0)
                {
                    todosLosCampos.Add(CornField);
                }
            }
        }

        for (int i = 0; i < todosLosCampos.Count; i++)
        {
            Transform temp = todosLosCampos[i];
            int randomIndex = Random.Range(i, todosLosCampos.Count);
            todosLosCampos[i] = todosLosCampos[randomIndex];
            todosLosCampos[randomIndex] = temp;
        }

        int arbolesCreados = 0;

        foreach (Transform CornField in todosLosCampos)
        {
            if (arbolesCreados >= maxArbolesTotales) break;
            if (CornField.childCount == 0) continue;

            if (Random.value <= probabilidadAparicion)
            {
                List<Transform> plantasMaiz = new List<Transform>();
                foreach (Transform maiz in CornField)
                {
                    plantasMaiz.Add(maiz);
                }

                if (plantasMaiz.Count > 0)
                {
                    int indexRand = Random.Range(0, plantasMaiz.Count);
                    Transform targetMaiz = plantasMaiz[indexRand];

                    GameObject obstaculo = Instantiate(
                        prefabReemplazo,
                        targetMaiz.position,
                        targetMaiz.rotation,
                        CornField
                    );

                    obstaculo.name = prefabReemplazo.name;
                    Destroy(targetMaiz.gameObject);

                    arbolesCreados++;
                    
                }
            }
        }
    }
}