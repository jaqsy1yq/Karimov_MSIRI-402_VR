using UnityEngine;
using UnityEngine.UI;
using Valve.VR;
using Valve.VR.InteractionSystem;

// Управление без шлема (камера FallbackObjects игрока SteamVR):
//   W A S D – ходьба по цеху, Shift – быстрее, правая кнопка мыши – поворот головы,
//   левая кнопка мыши – нажать кнопку пульта, взять заготовку или положить её туда, куда смотрит курсор.
// Ходьба через CharacterController: сквозь станок и стены пройти нельзя.
// Со шлемом этот объект выключен самим SteamVR, и скрипт не работает.
[RequireComponent(typeof(CharacterController))]
public class DesktopControls : MonoBehaviour
{
    public float walkSpeed = 2f;
    public float runSpeed = 3.8f;
    public float mouseSpeed = 3f;
    public float eyeHeight = 1.7f;
    public float reach = 4f;          // как далеко можно дотянуться мышью

    private CharacterController body;
    private Camera eye;
    private Transform holder;         // точка перед камерой, где «в руке» лежит заготовка
    private TrainingBlank carried;
    private float yaw, pitch, fallSpeed;

    private void Start()
    {
        var fly = GetComponent<FallbackCameraController>();
        if (fly) fly.enabled = false;   // стандартный «полёт» SteamVR не нужен

        eye = GetComponentInChildren<Camera>();
        body = GetComponent<CharacterController>();
        body.height = eyeHeight;
        body.radius = 0.25f;
        body.center = new Vector3(0f, -eyeHeight / 2f + 0.1f, 0f);

        holder = new GameObject("Рука_ПК").transform;
        holder.SetParent(eye ? eye.transform : transform, false);
        holder.localPosition = new Vector3(0.25f, -0.25f, 0.6f);

        Vector3 p = transform.position; p.y = eyeHeight; transform.position = p;
        yaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        if (SteamVR.initializedState == SteamVR.InitializedStates.InitializeSuccess) return;
        Look();
        Walk();
        if (Input.GetMouseButtonDown(0)) Click();
    }

    private void Look()
    {
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * mouseSpeed;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSpeed, -75f, 75f);
        }
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void Walk()
    {
        Quaternion flat = Quaternion.Euler(0f, yaw, 0f);
        Vector3 move = flat * new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        move = move.normalized * (Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed);
        fallSpeed = body.isGrounded ? -1f : fallSpeed - 9.8f * Time.deltaTime;
        body.Move((move + Vector3.up * fallSpeed) * Time.deltaTime);
    }

    private void Click()
    {
        Camera cam = eye ? eye : Camera.main;
        if (cam == null) return;
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        // заготовка в руке не должна загораживать луч
        Collider own = carried ? carried.GetComponent<Collider>() : null;
        if (own) own.enabled = false;
        bool found = Physics.Raycast(ray, out RaycastHit hit, reach, ~0, QueryTriggerInteraction.Ignore);
        if (own) own.enabled = true;
        if (!found) return;

        // кнопка пульта
        Button button = hit.collider.GetComponentInParent<Button>();
        if (button != null)
        {
            if (button.interactable) button.onClick.Invoke();
            return;
        }

        // положить заготовку туда, куда указывает курсор
        if (carried != null)
        {
            TrainingBlank b = carried;
            carried = null;
            b.DesktopRelease(hit.point + hit.normal * 0.04f);
            return;
        }

        // взять заготовку
        TrainingBlank blank = hit.collider.GetComponentInParent<TrainingBlank>();
        if (blank != null && blank.trainer != null && blank.trainer.CanGrabBlank)
        {
            carried = blank;
            blank.DesktopGrab(holder);
        }
    }
}
