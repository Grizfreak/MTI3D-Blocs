using TMPro;
using UnityEngine;

public class GhostPlayer : MonoBehaviour
{
    [Header("Références visuelles")]
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private TMP_Text nameLabel;

    private Renderer[] _renderers;
    private Collider[] _colliders;

    private int _remoteLevel;
    private Vector3 _targetPosition;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);

        // Pas de collisions sur les ghosts
        if (_colliders != null)
        {
            foreach (var c in _colliders)
                c.enabled = false;
        }

        // Matériau de fantôme
        if (ghostMaterial != null && _renderers != null)
        {
            foreach (var r in _renderers)
                r.material = ghostMaterial;
        }
    }

    public void SetName(string name)
    {
        if (nameLabel != null)
        {
            nameLabel.text = name;
            nameLabel.enabled = true;
        }
    }

    public void SetLevelAndPosition(int level, Vector3 pos)
    {
        _remoteLevel = level;
        _targetPosition = pos;

        Debug.Log($"[P2P] Ghost '{(nameLabel != null ? nameLabel.text : "?")}' -> level={_remoteLevel}, pos={_targetPosition}");
        RefreshVisibility();
    }

    private void Update()
    {
        // Lerp vers la position réseau pour lisser un peu
        transform.position = Vector3.Lerp(transform.position, _targetPosition, 0.2f);
    }

    private void RefreshVisibility()
    {
        if (Levels.instance == null) return;

        bool sameLevel = (Levels.instance.currentLevel == _remoteLevel);

        if (_renderers != null)
        {
            foreach (var r in _renderers)
                r.enabled = sameLevel;
        }

        if (nameLabel != null)
        {
            nameLabel.enabled = sameLevel;
        }

        Debug.Log($"[P2P] Ghost '{(nameLabel != null ? nameLabel.text : "?")}' visible={sameLevel} (localLevel={Levels.instance.currentLevel}, remoteLevel={_remoteLevel})");
    }

}