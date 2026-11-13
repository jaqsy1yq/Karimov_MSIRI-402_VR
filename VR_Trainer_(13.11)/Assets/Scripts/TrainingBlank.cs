using UnityEngine;
using Valve.VR.InteractionSystem;

// Заготовка для тренажёра (вместо Throwable).
// Берётся рукой только тогда, когда это разрешает сценарий MachineTrainer.
// После отпускания не летит по инерции: сценарий сам решает, куда её поставить.
[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Rigidbody))]
public class TrainingBlank : MonoBehaviour
{
    public MachineTrainer trainer;
    public bool Held { get; private set; }

    private Rigidbody body;
    private Renderer view;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        view = GetComponentInChildren<Renderer>();
    }

    private void HandHoverUpdate(Hand hand)
    {
        if (Held || trainer == null || !trainer.CanGrabBlank) return;
        GrabTypes grab = hand.GetGrabStarting();
        if (grab == GrabTypes.None) return;

        Hand.AttachmentFlags flags =
            Hand.AttachmentFlags.ParentToHand |
            Hand.AttachmentFlags.DetachFromOtherHand |
            Hand.AttachmentFlags.TurnOnKinematic;
        hand.AttachObject(gameObject, grab, flags);
    }

    private void OnAttachedToHand(Hand hand)
    {
        Held = true;
        hand.HoverLock(null);
        trainer?.NotifyPickedUp();
    }

    private void HandAttachedUpdate(Hand hand)
    {
        if (hand.IsGrabEnding(gameObject)) hand.DetachObject(gameObject, true);
    }

    private void OnDetachedFromHand(Hand hand)
    {
        Held = false;
        hand.HoverUnlock(null);
        transform.SetParent(null, true);
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        trainer?.NotifyBlankReleased();
    }

    // Поставить точно в точку. fixedInPlace – закрепить (в тисках или на столе готовых),
    // заготовка становится дочерней к точке и едет вместе со столом станка.
    public void PlaceAt(Transform point, bool fixedInPlace)
    {
        body.isKinematic = true;
        body.useGravity = false;
        transform.SetParent(fixedInPlace ? point : null, true);
        transform.SetPositionAndRotation(point.position, point.rotation);
        if (!fixedInPlace)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    public void Paint(Color c)
    {
        if (view) view.material.color = c;
    }

    // Для ходьбы без шлема: заготовку берут и кладут мышью (DesktopControls).
    public void DesktopGrab(Transform holder)
    {
        Held = true;
        body.isKinematic = true;
        body.useGravity = false;
        transform.SetParent(holder, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        trainer?.NotifyPickedUp();
    }

    public void DesktopRelease(Vector3 dropPoint)
    {
        Held = false;
        transform.SetParent(null, true);
        transform.position = dropPoint;
        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        trainer?.NotifyBlankReleased();
    }
}
