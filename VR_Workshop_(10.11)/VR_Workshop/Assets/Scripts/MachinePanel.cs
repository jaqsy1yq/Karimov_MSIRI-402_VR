
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
            ? "׀אבמעאוע"
            : "־סעאםמגכום";

        axesText.text =
            $"X: {data.x:F1} לל\n" +
            $"Y: {data.y:F1} לל\n" +
            $"Z: {data.z:F1} לל";
    }

    public void ShowAxes(float x, float y, float z)
    {
        axesText.text =
            $"X: {x:F1} לל\n" +
            $"Y: {y:F1} לל\n" +
            $"Z: {z:F1} לל";
    }

    public void ShowMessage(string message)
    {
        messageText.text = message;
    }
}
