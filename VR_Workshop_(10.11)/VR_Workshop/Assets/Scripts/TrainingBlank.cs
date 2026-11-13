using UnityEngine;
using Valve.VR.InteractionSystem;

// VR training part for the 12.11 assignment.
// Replace (remove) Throwable on VR_Заготовка: never use both together.
[RequireComponent(typeof(Interactable))]
[RequireComponent(typeof(Rigidbody))]
public class TrainingBlank : MonoBehaviour
{
    public MachineTrainer trainer;
    private Rigidbody body;
    private bool held;
    private Hand activeHand;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void HandHoverUpdate(Hand hand)
    {
        if (trainer == null || !trainer.CanGrabBlank || held)
            return;

        GrabTypes grab = hand.GetGrabStarting();
        if (grab == GrabTypes.None)
            return;

        // Parent to a hand; kinematic while held, without throw velocity on release.
        Hand.AttachmentFlags flags =
            Hand.AttachmentFlags.ParentToHand |
            Hand.AttachmentFlags.DetachFromOtherHand |
            Hand.AttachmentFlags.TurnOnKinematic;
        hand.AttachObject(gameObject, grab, flags);
    }

    private void OnAttachedToHand(Hand hand)
    {
        held = true;
        activeHand = hand;
        hand.HoverLock(null);
        if (trainer != null) trainer.NotifyPickedUp();
    }

    private void HandAttachedUpdate(Hand hand)
    {
        if (hand.IsGrabEnding(gameObject))
            hand.DetachObject(gameObject, true);
    }

    private void OnDetachedFromHand(Hand hand)
    {
        held = false;
        activeHand = null;
        hand.HoverUnlock(null);
        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        if (trainer != null) trainer.NotifyBlankReleased();
    }
}
