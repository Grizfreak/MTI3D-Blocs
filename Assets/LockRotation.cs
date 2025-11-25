using UnityEngine;

public class LockRotation : MonoBehaviour
{
    private Quaternion initialRotation;

    void Start()
    {
        // Rotation initiale : vers le haut (+y) et face à la caméra principale
        initialRotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
    }

    void LateUpdate()
    {
        // Le canvas garde toujours la même rotation
        transform.rotation = initialRotation;
    }

}
