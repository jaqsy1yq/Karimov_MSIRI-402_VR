using UnityEngine;

// Практическая работа: обработчик столкновений объектов.
public class FigureCollision : MonoBehaviour
{
    private Renderer figureRenderer;
    private Color originalColor;

    private void Start()
    {
        figureRenderer = GetComponent<Renderer>();
        if (figureRenderer != null)
            originalColor = figureRenderer.material.color;
    }

    // Unity автоматически вызывает этот метод при столкновении.
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("СТОЛКНОВЕНИЕ: " + gameObject.name +
                  " столкнулся с объектом " + collision.gameObject.name);

        if (figureRenderer != null)
        {
            figureRenderer.material.color = Color.red;
            CancelInvoke(nameof(RestoreColor));
            Invoke(nameof(RestoreColor), 0.5f);
        }
    }

    private void RestoreColor()
    {
        if (figureRenderer != null)
            figureRenderer.material.color = originalColor;
    }
}
