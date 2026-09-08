using UnityEngine;

public class VehicleSpawner : MonoBehaviour
{   //Los lados los agrego claude no sabia que lado poner los vehiculos
    public enum LadoEntrada { Izquierda, Derecha, Arriba, Abajo }

    [Header("Referencia a la parcela")]
    public GridGenerator gridGenerator; // para la cantidad de columnas y filas, para hacer los calculos y colocar los vehiculos
    public Grid grid; //para obtener el tamaño de celda

    [Header("Prefabs")]
    public GameObject prefabCamion;
    public GameObject prefabCosechadora;

    [Header("Cantidad")]
    [Range(0, 10)] public int cantidadCamiones = 2;
    [Range(0, 10)] public int cantidadCosechadoras = 1;

    [Header("Configuración de Spawn")]
    public LadoEntrada ladoEntrada = LadoEntrada.Abajo;
    public float margenExterior = 5f;// distancia fuera del borde de la parcela
    public float espacioEntreVehiculos = 3f; // separación entre cada vehículo en la fila

    private void Start()
    {
        // Camiones y cosechadoras se acomodan en la misma fila de entrada, uno después del otro, para no uno encima del otro.
        int indiceActual = 0;

        indiceActual = SpawnVehiculos(prefabCamion, cantidadCamiones, indiceActual);
        indiceActual = SpawnVehiculos(prefabCosechadora, cantidadCosechadoras, indiceActual);
    }

    private int SpawnVehiculos(GameObject prefab, int cantidad, int indiceInicial)
    {
        float anchoParcela = gridGenerator.columnas * grid.cellSize.x;
        float largoParcela = gridGenerator.filas * grid.cellSize.z;

        for (int i = 0; i < cantidad; i++)
        {
            int indice = indiceInicial + i;
            Vector3 posicion = ObtenerPosicionEnEntrada(anchoParcela, largoParcela, indice);
            Instantiate(prefab, posicion, Quaternion.identity);
        }

        return indiceInicial + cantidad; //punto inicial + (indice*separacion)
    }

    private Vector3 ObtenerPosicionEnEntrada(float ancho, float largo, int indice)
    {
        float offsetLineal = indice * espacioEntreVehiculos;
        float x, z;

        switch (ladoEntrada)
        {
            case LadoEntrada.Izquierda:
                x = -margenExterior;
                z = offsetLineal;
                break;
            case LadoEntrada.Derecha:
                x = ancho + margenExterior;
                z = offsetLineal;
                break;
            case LadoEntrada.Arriba:
                x = offsetLineal;
                z = largo + margenExterior;
                break;
            default: // Abajo
                x = offsetLineal;
                z = -margenExterior;
                break;
        }

        return transform.position + new Vector3(x, 0f, z);
    }
}