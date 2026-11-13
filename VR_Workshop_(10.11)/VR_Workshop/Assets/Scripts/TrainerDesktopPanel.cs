using UnityEngine;

/// <summary>
/// Test the local training scenario without VR equipment.
/// Remove/disable this component before the headset demonstration.
/// </summary>
public class TrainerDesktopPanel : MonoBehaviour
{
    public MachineTrainer trainer;

    private void OnGUI()
    {
        if (trainer == null || !trainer.enableDesktopTests) return;

        GUILayout.BeginArea(new Rect(15, 15, 255, 265), GUI.skin.box);
        GUILayout.Label("Тест учебного цикла (ПК)");
        if (GUILayout.Button("Далее")) trainer.NextStep();
        if (GUILayout.Button("Установить заготовку (тест)")) trainer.DesktopPlaceBlank();
        if (GUILayout.Button("Ограждение")) trainer.ToggleGuard();
        if (GUILayout.Button("Пуск")) trainer.StartCycle();
        if (GUILayout.Button("Стоп")) trainer.StopCycle();
        if (GUILayout.Button("Сдать деталь (тест)")) trainer.DesktopDeliverBlank();
        if (GUILayout.Button("Повторить")) trainer.RestartTraining();
        GUILayout.EndArea();
    }
}
