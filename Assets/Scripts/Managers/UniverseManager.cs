using UnityEngine;

public class UniverseManager : MonoBehaviour
{
    [SerializeField]
    Transform player;
    [SerializeField]
    int distance = 1000;

    void FixedUpdate()
    {
        if (player.position.magnitude > distance)
            transform.position -= player.position;
    }
}
