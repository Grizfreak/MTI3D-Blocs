using TMPro;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(Collider))]
public class NetworkGhost : NetworkBehaviour
{
    private Collider _col;
    private Rigidbody _rb;
    private Material _mat;
    private TMP_Text _txt;
    
    // Numéro de niveau où se trouve ce joueur
    private NetworkVariable<int> levelIndex = new NetworkVariable<int>(
        -1,                                           // valeur par défaut
        NetworkVariableReadPermission.Everyone,       // tout le monde peut lire
        NetworkVariableWritePermission.Owner);        // seul le propriétaire écrit

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _rb = GetComponent<Rigidbody>();
        _mat = GetComponent<Material>();
        _txt = GetComponentInChildren<TMP_Text>();
    }

    public override void OnNetworkSpawn()
    {
        levelIndex.OnValueChanged += OnRemoteLevelChanged;
        if (IsOwner)
        {
            // Ce héros est le joueur local sur CETTE machine
            // On le place au spawn du level courant
            if (Levels.instance != null)
            {
                Debug.Log($"Placing local player at spawn for level {Levels.instance.currentLevel}");
                transform.position = Levels.instance.SpawnPosition;
                levelIndex.Value = Levels.instance.currentLevel;
            }
        } else {
            // Désactiver les collisions pour les ghosts
            _col.enabled = false;
            _rb.isKinematic = true;

            // Effet visuel “fantôme”
            Color ghostColor = _mat.color;
            ghostColor.a = 0.3f;
            _mat.color = ghostColor;
            
            // Pseudo du joueur
            _txt.gameObject.SetActive(true);
            _txt.text = $"{name}";
        }
    }
    
    private void OnDestroy()
    {
        levelIndex.OnValueChanged -= OnRemoteLevelChanged;
    }

    private void Update()
    {
        // Le joueur local garde sa NetworkVariable de niveau à jour
        if (!IsOwner) return;

        if (Levels.instance == null) return;

        int localLevel = Levels.instance.currentLevel;

        if (levelIndex.Value != localLevel)
        {
            levelIndex.Value = localLevel;
        }
    }
    
    /// <summary>
    /// Appelé sur tous les clients quand ce joueur change de niveau.
    /// </summary>
    private void OnRemoteLevelChanged(int previous, int current)
    {
        if (Levels.instance == null || _mat == null)
            return;

        // On ne touche pas au rendu de notre propre joueur ici
        if (IsOwner) return;

        bool sameLevelAsLocal = (current == Levels.instance.currentLevel);

        gameObject.SetActive(sameLevelAsLocal);
    }
}