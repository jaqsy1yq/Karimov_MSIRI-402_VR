using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Учебный цикл на фрезерном станке (12.11–13.11).
// 9 шагов по порядку, на каждом разрешено только своё действие.
// Ошибки снимают баллы (из 100), в конце показываются время, баллы и список ошибок.
// Сценарий работает только в Unity и не отправляет команды на сервер станка.
public class MachineTrainer : MonoBehaviour
{
    public enum Stage
    {
        Welcome = 1,     // ознакомление с рабочим местом
        CheckMachine,    // проверка станка
        TakeBlank,       // взять заготовку
        LoadBlank,       // установить заготовку в тиски
        CloseGuard,      // закрыть защитный щиток
        Ready,           // пуск
        Processing,      // фрезерование
        Unload,          // снять деталь и отнести на стол готовых деталей
        Complete         // итог
    }

    [Header("Заготовка и точки")]
    public TrainingBlank blank;
    public Transform blankHome;     // место на столе заготовок
    public Transform workPoint;     // место в тисках станка
    public Transform outputPoint;   // место на столе готовых деталей
    public BoxCollider workZone;
    public BoxCollider outputZone;

    [Header("Узлы станка")]
    public Transform spindle;       // шпиндель: опускается на заготовку
    public Transform cutter;        // фреза: вращается во время работы
    public Transform table;         // стол: подача вдоль оси X
    public float plunge = 0.08f;    // глубина опускания шпинделя, м
    public float feed = 0.15f;      // ход стола в каждую сторону, м
    public float duration = 8f;     // время цикла, с
    public float cutterSpeed = 900f;

    [Header("Защитный щиток")]
    public Transform guard;         // прозрачный щиток, опускается перед пуском
    public float guardDrop = 0.45f;
    public bool guardClosed;

    [Header("Пульт оператора")]
    public TMP_Text stepText;       // «Шаг 3 из 9»
    public TMP_Text stageText;      // название шага
    public TMP_Text hintText;       // что делать
    public TMP_Text scoreText;      // баллы и время
    public TMP_Text resultText;     // итог
    public Image[] stepCells;       // 9 клеток прогресса: пройдено – зелёная, текущий – оранжевая
    public Image guardLamp;         // лампа щитка: красная – открыт, зелёная – закрыт

    public Color rawColor = new Color(0.72f, 0.74f, 0.78f);
    public Color doneColor = new Color(0.25f, 0.55f, 0.95f);

    public Stage Current { get; private set; }
    public bool CanGrabBlank => Current == Stage.TakeBlank || Current == Stage.LoadBlank || (Current == Stage.Unload && !guardClosed);

    private readonly Dictionary<string, int> mistakes = new Dictionary<string, int>();
    private Coroutine cycle, guardMove;
    private Vector3 spindleStart, tableStart, guardOpenPos;
    private float startTime, lastPress = -1f;
    private bool loaded, milled;

    private void Start()
    {
        if (blank == null || blankHome == null || workPoint == null || outputPoint == null ||
            workZone == null || outputZone == null || spindle == null || table == null || guard == null)
        {
            Debug.LogError("MachineTrainer: не все ссылки заполнены в Inspector");
            enabled = false;
            return;
        }
        blank.trainer = this;
        spindleStart = spindle.localPosition;
        tableStart = table.localPosition;
        guardOpenPos = guard.localPosition;
        RestartTraining();
    }

    private void Update()
    {
        if (Current != Stage.Complete) ShowScore();
    }

    // ---------- кнопки пульта ----------

    private bool Pressed()
    {
        // одна кнопка не срабатывает дважды за одно нажатие
        if (Time.unscaledTime - lastPress < 0.3f) return false;
        lastPress = Time.unscaledTime;
        return true;
    }

    public void NextStep()
    {
        if (!Pressed()) return;
        if (Current == Stage.Welcome) GoTo(Stage.CheckMachine);
        else if (Current == Stage.CheckMachine)
        {
            if (guardClosed) Hint("Перед установкой заготовки щиток должен быть поднят.");
            else if (spindle.localPosition != spindleStart) Hint("Шпиндель должен быть в верхнем положении.");
            else GoTo(Stage.TakeBlank);
        }
        else Mistake("Лишнее нажатие «Далее»", "Кнопка «Далее» нужна только на шагах 1 и 2.");
    }

    public void ToggleGuard()
    {
        if (!Pressed()) return;
        if (Current == Stage.Processing)
        {
            // щиток открыли во время резания – аварийная остановка
            StopMilling();
            MoveGuard(false);
            Mistake("Щиток открыт во время работы", "Аварийная остановка! Закройте щиток и снова нажмите «Пуск».");
            GoTo(Stage.CloseGuard, false);
            return;
        }
        if (Current == Stage.CloseGuard && !guardClosed)
        {
            MoveGuard(true);
            GoTo(Stage.Ready);
            return;
        }
        if (Current == Stage.Ready && guardClosed) { MoveGuard(false); GoTo(Stage.CloseGuard); return; }
        if (Current == Stage.Unload && guardClosed) { MoveGuard(false); Hint("Щиток поднят. Снимите деталь из тисков."); return; }
        if (!guardClosed) Mistake("Щиток закрыт не вовремя", "Сначала установите заготовку, потом закрывайте щиток.");
        else { MoveGuard(false); Hint("Щиток поднят."); }
    }

    public void StartCycle()
    {
        if (!Pressed()) return;
        if (Current == Stage.Processing) { Mistake("Повторный пуск", "Станок уже работает."); return; }
        if (!loaded || blank.Held) { Mistake("Пуск без заготовки", "Пуск запрещён: заготовка не установлена в тиски."); return; }
        if (!guardClosed) { Mistake("Пуск с открытым щитком", "Пуск запрещён: закройте защитный щиток."); return; }
        if (Current != Stage.Ready) { Hint("Сейчас пуск не нужен."); return; }
        GoTo(Stage.Processing);
        cycle = StartCoroutine(Milling());
    }

    public void StopCycle()
    {
        if (!Pressed()) return;
        if (Current != Stage.Processing) { Hint("Станок не работает."); return; }
        StopMilling();
        GoTo(Stage.Ready, false);
        Hint("Станок остановлен. Заготовка в тисках, можно снова нажать «Пуск».");
    }

    public void RestartTraining()
    {
        if (blank.Held) { Hint("Сначала отпустите заготовку, потом нажмите «Заново»."); return; }
        StopMilling();
        if (guardMove != null) StopCoroutine(guardMove);
        guard.localPosition = guardOpenPos;
        guardClosed = false;
        loaded = milled = false;
        mistakes.Clear();
        blank.PlaceAt(blankHome, false);
        blank.Paint(rawColor);
        startTime = Time.time;
        if (resultText) resultText.text = "";
        GoTo(Stage.Welcome);
    }

    // ---------- сообщения от заготовки ----------

    public void NotifyPickedUp()
    {
        if (Current == Stage.TakeBlank) GoTo(Stage.LoadBlank);
        else if (Current == Stage.Unload) Hint("Отнесите деталь на стол готовых деталей (зелёная метка) и отпустите.");
    }

    public void NotifyBlankReleased()
    {
        Vector3 p = blank.transform.position;
        if (Current == Stage.LoadBlank)
        {
            if (Inside(workZone, p))
            {
                blank.PlaceAt(workPoint, true);
                loaded = true;
                GoTo(Stage.CloseGuard);
            }
            else Mistake("Заготовка не в тисках", "Заготовка упала мимо тисков. Возьмите её и положите в тиски.");
        }
        else if (Current == Stage.Unload && milled)
        {
            if (Inside(outputZone, p))
            {
                blank.PlaceAt(outputPoint, true);
                GoTo(Stage.Complete);
                ShowResult();
            }
            else Mistake("Деталь не на столе готовых", "Положите готовую деталь на стол готовых деталей.");
        }
    }

    // ---------- фрезерование ----------

    private IEnumerator Milling()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // шпиндель опускается в начале и поднимается в конце, стол делает проход туда и обратно
            float down = Mathf.Clamp01(Mathf.Min(k, 1f - k) * 6f) * plunge;
            float x = Mathf.Sin(k * Mathf.PI * 2f) * feed;
            spindle.localPosition = spindleStart + Vector3.down * down;
            table.localPosition = tableStart + Vector3.right * x;
            if (cutter) cutter.Rotate(Vector3.up, cutterSpeed * Time.deltaTime, Space.World);
            yield return null;
        }
        cycle = null;
        StopMilling();
        milled = true;
        loaded = false;
        blank.Paint(doneColor);
        GoTo(Stage.Unload);
    }

    private void StopMilling()
    {
        if (cycle != null) { StopCoroutine(cycle); cycle = null; }
        if (spindle) spindle.localPosition = spindleStart;
        if (table) table.localPosition = tableStart;
    }

    private void MoveGuard(bool close)
    {
        guardClosed = close;
        if (guardMove != null) StopCoroutine(guardMove);
        guardMove = StartCoroutine(GuardRoutine(close ? guardOpenPos + Vector3.down * guardDrop : guardOpenPos));
        if (guardLamp) guardLamp.color = close ? new Color(0.2f, 0.75f, 0.3f) : new Color(0.85f, 0.2f, 0.2f);
    }

    private IEnumerator GuardRoutine(Vector3 target)
    {
        while ((guard.localPosition - target).sqrMagnitude > 0.000001f)
        {
            guard.localPosition = Vector3.MoveTowards(guard.localPosition, target, 0.9f * Time.deltaTime);
            yield return null;
        }
        guardMove = null;
    }

    // ---------- вспомогательное ----------

    private static bool Inside(BoxCollider zone, Vector3 point)
    {
        Vector3 local = zone.transform.InverseTransformPoint(point) - zone.center;
        Vector3 half = zone.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    private void GoTo(Stage s, bool withHint = true)
    {
        Current = s;
        int n = (int)s;
        if (stepText) stepText.text = $"Шаг {n} из 9";
        if (stageText) stageText.text = Title(s);
        if (stepCells != null)
            for (int i = 0; i < stepCells.Length; i++)
                stepCells[i].color = i + 1 < n ? new Color(0.3f, 0.7f, 0.35f) : i + 1 == n ? new Color(0.95f, 0.55f, 0.1f) : new Color(0.3f, 0.33f, 0.38f);
        if (withHint) Hint(HintFor(s));
        if (guardLamp && !guardClosed) guardLamp.color = new Color(0.85f, 0.2f, 0.2f);
    }

    private void Hint(string text)
    {
        if (hintText) hintText.text = text;
    }

    private void Mistake(string kind, string text)
    {
        mistakes.TryGetValue(kind, out int n);
        mistakes[kind] = n + 1;
        Hint(text);
        ShowScore();
    }

    private int Score()
    {
        int total = 0;
        foreach (var m in mistakes) total += m.Value;
        return Mathf.Max(0, 100 - total * 10);
    }

    private void ShowScore()
    {
        if (scoreText) scoreText.text = $"Баллы: {Score()}    Время: {Time.time - startTime:F0} с";
    }

    private void ShowResult()
    {
        ShowScore();
        int total = 0;
        foreach (var m in mistakes) total += m.Value;
        string s = total == 0 ? "Без ошибок!" : $"Ошибок: {total}";
        foreach (var m in mistakes) s += $"\n– {m.Key}: {m.Value}";
        if (resultText) resultText.text = s;
    }

    private static string Title(Stage s)
    {
        switch (s)
        {
            case Stage.Welcome: return "Ознакомление";
            case Stage.CheckMachine: return "Проверка станка";
            case Stage.TakeBlank: return "Взять заготовку";
            case Stage.LoadBlank: return "Установка в тиски";
            case Stage.CloseGuard: return "Защитный щиток";
            case Stage.Ready: return "Пуск";
            case Stage.Processing: return "Фрезерование";
            case Stage.Unload: return "Снять деталь";
            default: return "Итог";
        }
    }

    private static string HintFor(Stage s)
    {
        switch (s)
        {
            case Stage.Welcome: return "Осмотрите фрезерный станок, стол заготовок (синяя метка) и стол готовых деталей (зелёная). Нажмите «Далее».";
            case Stage.CheckMachine: return "Шпиндель вверху, щиток поднят, тиски пустые. Нажмите «Далее».";
            case Stage.TakeBlank: return "Возьмите заготовку со стола заготовок.";
            case Stage.LoadBlank: return "Поднесите заготовку к тискам станка и отпустите.";
            case Stage.CloseGuard: return "Заготовка в тисках. Нажмите «Щиток», чтобы опустить защитный щиток.";
            case Stage.Ready: return "Щиток закрыт. Нажмите «Пуск».";
            case Stage.Processing: return "Идёт фрезерование. Не открывайте щиток до остановки.";
            case Stage.Unload: return "Готово. Поднимите щиток кнопкой «Щиток», возьмите деталь и отнесите на стол готовых деталей.";
            default: return "Обучение пройдено. «Заново» – пройти ещё раз.";
        }
    }
}
