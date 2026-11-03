using UnityEngine;

public class RobotController : MonoBehaviour
{
    // Поля второго класса
    public string operatorName = "Жакс";
    private Robot robot;

    private void Start()
    {
        // Получаем первый класс на том же объекте Unity
        robot = GetComponent<Robot>();

        if (robot == null)
        {
            Debug.LogError("Добавьте компонент Robot на этот объект!");
            return;
        }

        ControlRobot();
    }

    // Обращаемся к полям, свойствам и методам первого класса
    public void ControlRobot()
    {
        Debug.Log("Оператор: " + operatorName);
        Debug.Log("Управляем роботом: " + robot.robotName);
        Debug.Log("Скорость робота: " + robot.speed);
        Debug.Log("Заряд до задания: " + robot.Battery);

        robot.DoTask();
        robot.ShowInfo();
    }
}
