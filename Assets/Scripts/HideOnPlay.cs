using UnityEngine;

public class HideOnPlay : MonoBehaviour
{
    private void Awake()
    {
        gameObject.SetActive(false);
    }
}
