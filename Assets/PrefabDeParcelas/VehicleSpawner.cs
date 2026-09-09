using UnityEngine;

public class VehicleSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject prefabTractor;
    public GameObject prefabCosechadora;

    [Header("Cantidad")]
    [Range(0, 20)] public int cantidadTractores = 2;
    [Range(0, 20)] public int cantidadCosechadoras = 1;

    [Header("Espaciado")]
    public float espacioEntreVehiculos = 4f;
    private float offsetActual = 0f;

    private void Start()
    {
        ColocarVehiculos(prefabTractor, cantidadTractores);
        ColocarVehiculos(prefabCosechadora, cantidadCosechadoras);
    }

    private void ColocarVehiculos(GameObject prefab, int cantidad)
    {
        for (int i = 0; i < cantidad; i++)
        {
            Vector3 posicion = transform.position + new Vector3(offsetActual, 0f, 0f);
            Instantiate(prefab, posicion, Quaternion.identity);

            offsetActual += espacioEntreVehiculos;
        }
    }
}