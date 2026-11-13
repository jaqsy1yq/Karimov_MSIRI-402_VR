using System;
using System.Collections;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// CNC VR panel client: tested GET parser for /get/3 and configurable POST commands.
/// IMPORTANT: POST is disabled until the server's official endpoint and JSON format
/// are verified by the API owner/instructor. This script never guesses a write route.
/// </summary>
public class MachineClient : MonoBehaviour
{
    [Header("GET: confirmed telemetry endpoint")]
    public string getUrl = "https://cnc.kovalev.team/get/3";
    public int channel = 1;
    [Min(0.1f)] public float interval = 0.5f;
    public MachinePanel panel;

    [Header("POST: enter only verified API settings")]
    public bool enablePost = false;
    public string postUrl = "";
    [TextArea(2, 4)] public string startJson = "";
    [TextArea(2, 4)] public string stopJson = "";

    private bool sendingCommand;
    private bool waitingForState;
    private bool haveCoordinates;
    private float lastX, lastY, lastZ;

    private IEnumerator Start()
    {
        if (panel == null)
        {
            Debug.LogError("MachineClient: assign MachineCanvas (MachinePanel) to Panel.");
            yield break;
        }

        panel.ShowMessage("Подключение к серверу…");
        while (true)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(getUrl))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    ShowGetError("Ошибка GET: " + request.error);
                }
                else
                {
                    try
                    {
                        JObject response = JObject.Parse(request.downloadHandler.text);
                        float x = GetAxis(response, "X");
                        float y = GetAxis(response, "Y");
                        float z = GetAxis(response, "Z");

                        lastX = x;
                        lastY = y;
                        lastZ = z;
                        haveCoordinates = true;

                        // Only use running when the server REALLY provides it as a bool.
                        JToken runningToken = response["running"];
                        bool hasRunning = runningToken != null &&
                                          runningToken.Type == JTokenType.Boolean;
                        if (hasRunning)
                        {
                            MachineState state = new MachineState
                            {
                                running = (bool)runningToken,
                                x = x, y = y, z = z
                            };
                            panel.ShowState(state);
                        }
                        else
                        {
                            panel.ShowAxes(x, y, z);
                            panel.stateText.text = "Статус не передаётся";
                        }

                        if (!sendingCommand)
                        {
                            if (waitingForState && hasRunning)
                            {
                                panel.ShowMessage("Связь есть. Состояние обновлено через GET.");
                                waitingForState = false;
                            }
                            else if (!waitingForState)
                            {
                                panel.ShowMessage("Связь установлена");
                            }
                        }
                        Debug.Log($"GET CNC: X={x:F1} мм, Y={y:F1} мм, Z={z:F1} мм" +
                                  (hasRunning ? $", running={(bool)runningToken}" : ", running отсутствует"));
                    }
                    catch (Exception exception)
                    {
                        ShowGetError("Ошибка разбора GET: " + exception.Message);
                    }
                }
            }
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, interval));
        }
    }

    private float GetAxis(JObject json, string axis)
    {
        JToken sections = json["data"];
        if (sections == null || sections.Type != JTokenType.Array)
            throw new FormatException("В JSON нет массива data");

        foreach (JToken section in sections)
        {
            if ((string)section[0] != "Оси Канала " + channel)
                continue;

            foreach (JToken row in section[1])
            {
                if ((string)row[0] != "Перемещение по оси " + axis)
                    continue;

                string value = ((string)row[1])
                    .Replace("мм", "")
                    .Replace(',', '.')
                    .Trim();

                float result = float.Parse(value, CultureInfo.InvariantCulture);
                if (float.IsNaN(result) || float.IsInfinity(result))
                    throw new FormatException("Недопустимая координата " + axis);
                return result;
            }
        }
        throw new FormatException("Не найдена координата " + axis);
    }

    private void ShowGetError(string message)
    {
        Debug.LogWarning(message);
        panel.stateText.text = "Нет связи";
        if (haveCoordinates)
        {
            panel.ShowAxes(lastX, lastY, lastZ);
            panel.axesText.text += "\n(неактуальны)";
        }
        panel.ShowMessage("Нет связи — проверьте сервер");
    }

    // Assign these methods directly to Button.onClick in the Inspector.
    public void StartMachine() { TrySendCommand("start", startJson); }
    public void StopMachine()  { TrySendCommand("stop", stopJson); }

    private void TrySendCommand(string action, string jsonBody)
    {
        if (sendingCommand)
            return;

        if (!enablePost || string.IsNullOrWhiteSpace(postUrl))
        {
            panel?.ShowMessage("POST не настроен: нужен адрес API");
            Debug.LogWarning($"POST {action} not sent: configure and verify POST API.");
            return;
        }

        if (string.IsNullOrWhiteSpace(jsonBody))
        {
            panel?.ShowMessage("POST не настроен: нужен формат JSON");
            Debug.LogWarning($"POST {action} not sent: JSON body is empty.");
            return;
        }

        try { JToken.Parse(jsonBody); }
        catch (Exception e)
        {
            panel?.ShowMessage("Ошибка JSON команды");
            Debug.LogWarning("Invalid POST JSON: " + e.Message);
            return;
        }

        StartCoroutine(SendCommand(action, jsonBody));
    }

    private IEnumerator SendCommand(string action, string body)
    {
        sendingCommand = true;
        panel?.ShowMessage("Отправка команды…");

        using (UnityWebRequest request = new UnityWebRequest(postUrl, UnityWebRequest.kHttpVerbPOST))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                panel?.ShowMessage("Ошибка команды: " + request.error);
                Debug.LogWarning($"POST {action}: {request.responseCode} {request.error}");
            }
            else
            {
                waitingForState = true;
                panel?.ShowMessage("Команда принята. Ожидание состояния…");
                Debug.Log($"POST {action}: HTTP {request.responseCode}; ответ: {request.downloadHandler.text}");
            }
        }
        sendingCommand = false;
    }
}
