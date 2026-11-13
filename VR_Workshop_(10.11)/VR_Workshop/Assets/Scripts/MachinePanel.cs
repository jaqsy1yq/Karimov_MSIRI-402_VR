
using TMPro;
using UnityEngine;

public class MachinePanel : MonoBehaviour
{
    public TMP_Text stateText;
    public TMP_Text axesText;
    public TMP_Text messageText;

    public void ShowState(MachineState data)
    {
        stateText.text = data.running
            ? "Работает"
            : "Остановлен";

        axesText.text =
            $"X: {data.x:F1} мм\n" +
            $"Y: {data.y:F1} мм\n" +
            $"Z: {data.z:F1} мм";
    }

    public void ShowAxes(float x, float y, float z)
    {
        axesText.text =
            $"X: {x:F1} мм\n" +
            $"Y: {y:F1} мм\n" +
            $"Z: {z:F1} мм";
    }

    public void ShowMessage(string message)
    {
        messageText.text = message;
    }
}
