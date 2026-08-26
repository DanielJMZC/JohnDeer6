using System.Collections.Generic;
using UnityEngine;

public class GlobalObstacleManager : MonoBehaviour
{
    public GameObject turbina;
    public GameObject[] prefabsReemplazo;

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
        bool turbinaCreada = false;

        foreach (Transform CornField in todosLosCampos)
        {
            if (arbolesCreados >= maxArbolesTotales) break;

            List<Transform> plantasMaiz = new List<Transform>();
            foreach (Transform maiz in CornField)
            {
                plantasMaiz.Add(maiz);
            }

            if (plantasMaiz.Count > 0)
            {
                Transform targetMaiz = plantasMaiz[Random.Range(0, plantasMaiz.Count)];
                GameObject prefabSeleccionado;

                if (!turbinaCreada)
                {
                    prefabSeleccionado = turbina;
                    turbinaCreada = true;
                } else
                {
                    prefabSeleccionado = prefabsReemplazo[Random.Range(0, prefabsReemplazo.Length)];
                }

                GameObject obstaculo = Instantiate(
                    prefabSeleccionado,
                    targetMaiz.position,
                    targetMaiz.rotation,
                    CornField
                );

                obstaculo.name = prefabSeleccionado.name;
                Destroy(targetMaiz.gameObject);

                arbolesCreados++;
            }
        }
    }
}