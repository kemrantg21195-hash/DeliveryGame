using UnityEngine;

public class GasStation : MonoBehaviour
{
    // Метод срабатывает автоматически, когда любой объект с Rigidbody входит в триггер заправки
    void OnTriggerEnter(Collider other)
    {
        // Ищем скрипт топливной системы на въехавшем объекте (или в его родителе)
        CarFuelSystem fuelSystem = other.GetComponentInParent<CarFuelSystem>();

        // Если это машина и у неё есть скрипт топлива
        if (fuelSystem != null)
        {
            // Заправляем до полного бака
            fuelSystem.RefuelToMax();
        }
    }
}
