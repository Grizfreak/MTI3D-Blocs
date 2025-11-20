using TMPro;
using UnityEngine;

public class VersionData : MonoBehaviour
{
    void Start()
    {
        this.GetComponent<TextMeshProUGUI>().text = "Version " + Application.version;
    }
}
