using UnityEngine;

// Практическая работа: создание фигур по заранее заданному массиву.
public class FigureSpawner : MonoBehaviour
{
    [System.Serializable]
    public class FigureInfo
    {
        public string figureName;
        public PrimitiveType figureType;
        public int quantity;

        public FigureInfo(string name, PrimitiveType type, int count)
        {
            figureName = name;
            figureType = type;
            quantity = count;
        }
    }

    // Название, тип и количество фигур задаются в массиве.
    public FigureInfo[] figures =
    {
        new FigureInfo("Куб", PrimitiveType.Cube, 3),
        new FigureInfo("Шар", PrimitiveType.Sphere, 2),
        new FigureInfo("Капсула", PrimitiveType.Capsule, 2)
    };

    private void Start()
    {
        CreateGround();
        CreateFigures();
        SetupCamera();
    }

    private void CreateFigures()
    {
        int total = 0;

        for (int group = 0; group < figures.Length; group++)
        {
            FigureInfo info = figures[group];
            if (info.quantity <= 0) continue;

            for (int i = 0; i < info.quantity; i++)
            {
                // Фигуры одного вида размещаются друг над другом,
                // затем падают и сталкиваются благодаря физике Unity.
                float x = (group - (figures.Length - 1) / 2f) * 4f;
                float y = 3f + i * 3f;
                Vector3 position = new Vector3(x, y, 0f);

                GameObject figure = GameObject.CreatePrimitive(info.figureType);
                figure.name = info.figureName + " " + (i + 1);
                figure.transform.position = position;

                // Rigidbody нужен, чтобы работала физика и столкновения.
                figure.AddComponent<Rigidbody>();

                // Обработчик столкновений из второго скрипта.
                figure.AddComponent<FigureCollision>();

                Renderer renderer = figure.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Color[] colors = { Color.cyan, Color.yellow, Color.green };
                    renderer.material.color = colors[group % colors.Length];
                }

                total++;
                Debug.Log("Создана фигура: " + figure.name);
            }
        }

        Debug.Log("Всего создано фигур: " + total);
    }

    private void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Земля";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(3f, 1f, 3f);
        ground.GetComponent<Renderer>().material.color = new Color(0.45f, 0.45f, 0.45f);
        // У Plane уже есть Collider, добавлять Rigidbody земле не нужно.
    }

    private void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
        }

        camera.transform.position = new Vector3(0f, 10f, -19f);
        camera.transform.LookAt(new Vector3(0f, 3.5f, 0f));
    }
}
