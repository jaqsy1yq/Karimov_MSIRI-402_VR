using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Local CNC training cycle for the 12.11 VR assignment.
/// Does NOT send HTTP requests or control a physical machine.
/// Keep MachineClient enabled separately for read-only GET telemetry.
/// </summary>
public class MachineTrainer : MonoBehaviour
{
    [Header("Scene references")]
    public Transform blank;
    public Rigidbody blankBody;
    public Renderer blankRenderer;
    public Transform blankHome;
    public Transform workPoint;
    public Transform outputPoint;
    public BoxCollider workZone;
    public BoxCollider outputZone;
    public Transform movingNode;

    [Header("Separate training UI (NOT MachinePanel fields)")]
    public TMP_Text stageText;
    public TMP_Text hintText;
    public TMP_Text resultText;

    [Header("Training settings")]
    [Min(0.2f)] public float duration = 5f;
    [Min(0f)] public float travel = 0.12f;
    public Vector3 localTravelAxis = Vector3.up;
    public bool requireGuard = false;
    public bool guardClosed = false;
    public GameObject guardObject;

    [Header("Desktop-only testing")]
    public bool enableDesktopTests = true;

    private enum Stage { Welcome, CheckMachine, LoadBlank, Ready, Processing, Output, Complete }
    private Stage stage = Stage.Welcome;
    private Coroutine runningCycle;
    private Vector3 nodeStart;
    private Vector3 blankStart;
    private Quaternion blankStartRotation;
    private Color originalColor;
    private Material blankMaterial;
    private bool blankHeld;

    // TrainingBlank queries this before allowing a VR grab.
    public bool CanGrabBlank => (stage == Stage.LoadBlank || stage == Stage.Output) && runningCycle == null;

    private void Awake()
    {
        if (movingNode != null) nodeStart = movingNode.localPosition;
        if (blank != null)
        {
            blankStart = blankHome != null ? blankHome.position : blank.position;
            blankStartRotation = blankHome != null ? blankHome.rotation : blank.rotation;
        }
        if (blankRenderer != null)
        {
            blankMaterial = blankRenderer.material;  // unique runtime instance
            originalColor = blankMaterial.color;
        }
        ShowStage("Ознакомление", "Нажмите 'Далее', чтобы проверить станок.");
        ShowResult("Результат: не выполнено");
    }

    public void NextStep()
    {
        if (stage == Stage.Welcome)
        {
            stage = Stage.CheckMachine;
            ShowStage("Проверка станка", "Проверьте рабочую зону и нажмите 'Далее'.");
        }
        else if (stage == Stage.CheckMachine)
        {
            stage = Stage.LoadBlank;
            ShowStage("Установка", "Возьмите заготовку и отпустите в WorkZone.");
        }
        else
        {
            ShowHint("Продолжайте текущий этап, а не нажимайте 'Далее'.");
        }
    }

    // Called by TrainingBlank (replaces Throwable in the 12.11 assignment).
    public void NotifyPickedUp()
    {
        // Hand keeps the rigidbody kinematic while attached.
        blankHeld = true;
    }

    public void NotifyBlankReleased()
    {
        blankHeld = false;
        if (blank == null) return;

        if (stage == Stage.LoadBlank)
        {
            if (!IsInside(workZone, blank.position))
            {
                ShowHint("Отпустите заготовку внутри рабочей зоны.");
                return;
            }
            if (!SnapTo(workPoint)) return;
            stage = Stage.Ready;
            ShowStage("Готовность", "Заготовка установлена. Нажмите 'Пуск'.");
        }
        else if (stage == Stage.Output)
        {
            if (!IsInside(outputZone, blank.position))
            {
                ShowHint("Перенесите готовую деталь в OutputZone.");
                return;
            }
            if (!SnapTo(outputPoint)) return;
            stage = Stage.Complete;
            ShowStage("Обучение завершено", "Нажмите 'Повторить' для нового цикла.");
            ShowResult("Результат: успешно");
        }
    }

    public void StartCycle()
    {
        if (stage == Stage.Processing || runningCycle != null)
        {
            ShowHint("Цикл уже выполняется.");
            return;
        }
        if (stage != Stage.Ready || blankHeld)
        {
            ShowHint("Пуск запрещён: сначала установите и отпустите заготовку.");
            return;
        }
        if (requireGuard && !guardClosed)
        {
            ShowHint("Пуск запрещён: закройте ограждение.");
            return;
        }
        if (movingNode == null)
        {
            ShowHint("Ошибка: не задан MovingNode.");
            return;
        }
        runningCycle = StartCoroutine(RunCycle());
    }

    public void StopCycle()
    {
        if (runningCycle == null)
        {
            ShowHint("Станок уже остановлен.");
            return;
        }
        StopCoroutine(runningCycle);
        runningCycle = null;
        movingNode.localPosition = nodeStart;
        stage = Stage.Ready;
        ShowStage("Цикл остановлен", "Можно снова нажать 'Пуск'.");
    }

    public void ToggleGuard()
    {
        if (runningCycle != null)
        {
            ShowHint("Нельзя открывать ограждение во время цикла.");
            return;
        }
        guardClosed = !guardClosed;
        if (guardObject != null) guardObject.SetActive(guardClosed);
        ShowHint(guardClosed ? "Ограждение закрыто." : "Ограждение открыто.");
    }

    public void RestartTraining()
    {
        if (blankHeld)
        {
            ShowHint("Сначала отпустите предмет, затем повторите.");
            return;
        }
        if (runningCycle != null)
        {
            StopCoroutine(runningCycle);
            runningCycle = null;
        }
        if (movingNode != null) movingNode.localPosition = nodeStart;
        if (blank != null)
        {
            if (blankBody != null)
            {
                blankBody.isKinematic = false;
                blankBody.useGravity = true;
                blankBody.linearVelocity = Vector3.zero;
                blankBody.angularVelocity = Vector3.zero;
            }
            blank.SetPositionAndRotation(blankStart, blankStartRotation);
        }
        if (blankMaterial != null) blankMaterial.color = originalColor;
        stage = Stage.Welcome;
        ShowStage("Ознакомление", "Нажмите 'Далее', чтобы проверить станок.");
        ShowResult("Результат: не выполнено");
    }

    // Only for testing in Game view without a VR headset.
    public void DesktopPlaceBlank()
    {
        if (!enableDesktopTests || stage != Stage.LoadBlank) return;
        if (!SnapTo(workPoint)) return;
        stage = Stage.Ready;
        ShowStage("Готовность", "Тест: заготовка установлена. Нажмите 'Пуск'.");
    }

    public void DesktopDeliverBlank()
    {
        if (!enableDesktopTests || stage != Stage.Output) return;
        if (!SnapTo(outputPoint)) return;
        stage = Stage.Complete;
        ShowStage("Обучение завершено", "Тест пройден. Нажмите 'Повторить'.");
        ShowResult("Результат: успешно (тест ПК)");
    }

    private IEnumerator RunCycle()
    {
        stage = Stage.Processing;
        ShowStage("Обработка", "Идёт учебный цикл. Не извлекайте заготовку.");
        float elapsed = 0f;
        float cycleDuration = Mathf.Max(0.2f, duration);
        Vector3 delta = localTravelAxis.normalized * Mathf.Max(0f, travel);

        while (elapsed < cycleDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / cycleDuration);
            // Smooth outward and return, ending precisely at initial local position.
            float amplitude = Mathf.Sin(progress * Mathf.PI);
            movingNode.localPosition = nodeStart + delta * amplitude;
            yield return null;
        }
        movingNode.localPosition = nodeStart;
        runningCycle = null;
        if (blankMaterial != null) blankMaterial.color = Color.green;
        stage = Stage.Output;
        ShowStage("Обработка завершена", "Возьмите деталь и отпустите в OutputZone.");
        ShowResult("Обработка завершена. Деталь готова.");
    }

    // Fix the released object exactly to its docking point.
    // Unlike a thrown object, the training part must not drift away after docking.
    private bool SnapTo(Transform target)
    {
        if (target == null || blank == null)
        {
            ShowHint("Ошибка: не назначена точка установки.");
            return false;
        }
        if (blankBody != null)
        {
            blankBody.linearVelocity = Vector3.zero;
            blankBody.angularVelocity = Vector3.zero;
            blankBody.isKinematic = true;
            blankBody.useGravity = false;
        }
        blank.SetPositionAndRotation(target.position, target.rotation);
        return true;
    }

    private static bool IsInside(BoxCollider zone, Vector3 point)
    {
        if (zone == null) return false;
        Vector3 local = zone.transform.InverseTransformPoint(point) - zone.center;
        Vector3 half = zone.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x &&
               Mathf.Abs(local.y) <= half.y &&
               Mathf.Abs(local.z) <= half.z;
    }

    private void ShowStage(string title, string hint)
    {
        if (stageText != null) stageText.text = title;
        ShowHint(hint);
    }

    private void ShowHint(string value)
    {
        if (hintText != null) hintText.text = value;
        Debug.Log("Training: " + value);
    }

    private void ShowResult(string value)
    {
        if (resultText != null) resultText.text = value;
    }
}
