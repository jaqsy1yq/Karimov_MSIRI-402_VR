
using System;
using System.Collections;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class MachineTelemetryClient : MonoBehaviour
{
    public string url = "https://cnc.kovalev.team/get/3";
    public int channel = 1;
    public float interval = 0.5f;

    public MachinePanel panel;

    private IEnumerator Start()
    {
        if (panel == null)
        {
            Debug.LogError("Не назначена панель MachinePanel");
            yield break;
        }

        panel.ShowMessage("Подключение к серверу...");

        while (true)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = 10;

                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        JObject json = JObject.Parse(
                            req.downloadHandler.text
                        );

                        float x = GetCoordinate(json, "X");
                        float y = GetCoordinate(json, "Y");
                        float z = GetCoordinate(json, "Z");

                        panel.ShowAxes(x, y, z);
                        panel.ShowMessage("Связь установлена");

                        Debug.Log(
                            $"Сервер CNC: X={x:F1} мм, " +
                            $"Y={y:F1} мм, Z={z:F1} мм"
                        );
                    }
                    catch (Exception e)
                    {
                        panel.ShowMessage(
                            "Ошибка данных - координаты неактуальны"
                        );

                        Debug.LogWarning(
                            "Ошибка JSON: " + e.Message
                        );
                    }
                }
                else
                {
                    panel.ShowMessage(
                        "Нет связи - координаты неактуальны"
                    );

                    Debug.LogWarning(
                        "Ошибка GET: " + req.error
                    );
                }
            }

            yield return new WaitForSecondsRealtime(
                Mathf.Max(0.1f, interval)
            );
        }
    }

    private float GetCoordinate(JObject data, string axis)
    {
        foreach (JToken section in data["data"])
        {
            if ((string)section[0] != "Оси Канала " + channel)
                continue;

            foreach (JToken row in section[1])
            {
                if ((string)row[0] == "Перемещение по оси " + axis)
                {
                    string value = ((string)row[1])
                        .Replace("мм", "")
                        .Replace(',', '.')
                        .Trim();

                    float result = float.Parse(
                        value,
                        CultureInfo.InvariantCulture
                    );

                    if (float.IsNaN(result) ||
                        float.IsInfinity(result))
                        throw new FormatException(
                            "Некорректная координата " + axis
                        );

                    return result;
                }
            }
        }

        throw new FormatException(
            "Не найдена координата " + axis
        );
    }
}
