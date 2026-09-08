using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    [Header("Grid")]
    public Grid grid;

    [Header("Prefabs")]
    public GameObject prefabTierraCultivo;
    public GameObject[] prefabsObstaculos;

    [Header("Tamaño (input de prueba)")]
    [Range(1, 20)] public int filas = 5;
    [Range(1, 20)] public int columnas = 5;

    [Header("Obstáculos")]
    [Range(0, 1)] public float probabilidadObstaculo = 0.1f;

    private GameObject[,] celdas;

    private void Start()
    {
        GenerarParcela(filas, columnas);
    }

    public void GenerarParcela(int nuevasFilas, int nuevasColumnas)
    {
        filas = nuevasFilas;
        columnas = nuevasColumnas;
        celdas = new GameObject[filas, columnas];

        for (int f = 0; f < filas; f++)
        {
            for (int c = 0; c < columnas; c++)
            {
                Vector3 resultadoGrid = grid.CellToWorld(new Vector3Int(c, f, 0));
                Vector3 posicionMundo = new Vector3(resultadoGrid.x, 0f, resultadoGrid.y);

                GameObject prefabAUsar = DecidirPrefab();
                GameObject instancia = Instantiate(prefabAUsar, transform.position + posicionMundo, Quaternion.identity, transform);
                celdas[f, c] = instancia;
            }
        }

        Debug.Log($"Parcela generada: {filas}x{columnas}, {celdas.Length} celdas instanciadas.");
    }

    private GameObject DecidirPrefab()
    {
        if (Random.value < probabilidadObstaculo && prefabsObstaculos.Length > 0)
        {
            int index = Random.Range(0, prefabsObstaculos.Length);
            return prefabsObstaculos[index];
        }
        return prefabTierraCultivo;
    }
}