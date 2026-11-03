using UnityEngine;

public class Robot : MonoBehaviour
{
    // Поля первого класса
    public string robotName = "Atlas";
    public int speed = 10;
    private int battery = 100;

    // Свойство для чтения закрытого поля
    public int Battery => battery;

    // Первый класс самостоятельно пишет в консоль
    private void Awake()
    {
        ShowInfo();
    }

    // Метод вывода информации о роботе
    public void ShowInfo()
    {
        Debug.Log("Имя робота: " + robotName);
        Debug.Log("Скорость: " + speed);
        Debug.Log("Заряд батареи: " + battery);
    }

    // Метод выполнения задания
    public void DoTask()
    {
        if (battery >= 20)
        {
            battery -= 20;
            Debug.Log(robotName + " выполнил задание!");
        }
        else
        {
            Debug.Log("Недостаточно заряда!");
        }
    }
}
