using System.Collections.Generic;
using UnityEngine;

public class ObstaculoRandomPlacer : MonoBehaviour
{
    [System.Serializable]
    public class ObstaculoConfig
    {
        public GameObject prefab;
        public int cantidad;
    }

    [Header("Configuración de obstáculos")]
    public ObstaculoConfig[] obstaculos;

    [Header("Referencia al área")]
    public Transform soils;

    void Start()
    {
        ColocarObstaculos();
    }

    void ColocarObstaculos()
    {
        List<Transform> todosLosMaiz = new List<Transform>();

        foreach (Transform soil in soils)
        {
            foreach (Transform maiz in soil)
            {
                todosLosMaiz.Add(maiz);
            }
        }

        for (int i = 0; i < todosLosMaiz.Count; i++)
        {
            int randomIndex = Random.Range(i, todosLosMaiz.Count);
            Transform temp = todosLosMaiz[i];
            todosLosMaiz[i] = todosLosMaiz[randomIndex];
            todosLosMaiz[randomIndex] = temp;
        }

        List<GameObject> colaObstaculos = new List<GameObject>();
        foreach (ObstaculoConfig config in obstaculos)
        {
            for (int j = 0; j < config.cantidad; j++)
            {
                colaObstaculos.Add(config.prefab);
            }
        }

        int cantidadAColocar = Mathf.Min(colaObstaculos.Count, todosLosMaiz.Count);

        for (int i = 0; i < cantidadAColocar; i++)
        {
            Transform posicionMaiz = todosLosMaiz[i];
            GameObject prefabSeleccionado = colaObstaculos[i];

            GameObject obstaculo = Instantiate(
                prefabSeleccionado,
                posicionMaiz.position,
                posicionMaiz.rotation,
                posicionMaiz.parent
            );

            obstaculo.name = prefabSeleccionado.name;

        }
    }
}