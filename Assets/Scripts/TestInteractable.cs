using UnityEngine;

public class TestInteractable : MonoBehaviour, IInteractable
{
    public void Interact(ulong clientId)
    {
        Debug.Log($"[TestInteractable] '{gameObject.name}' interacted with by client {clientId}");
    }
}
