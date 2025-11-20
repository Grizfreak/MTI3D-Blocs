using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class NetworkGhost : NetworkBehaviour
{
    public Material ghostMaterial;
    
    private Collider[] _colliders;
    private Rigidbody _rb;
    private Renderer[] _renderers;
    private TMP_Text _txt;
    private Heros _heros;
    
    // Numéro de niveau où se trouve ce joueur
    private NetworkVariable<int> _levelIndex = new NetworkVariable<int>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        // On récupère tout ce qu’il faut avant OnNetworkSpawn
        _colliders = GetComponentsInChildren<Collider>(true);
        _rb = GetComponent<Rigidbody>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _txt = GetComponentInChildren<TMP_Text>(true);
        _heros = GetComponent<Heros>();

        Debug.Log("Colliders: " + _colliders.Length);
        Debug.Log("Rigidbody: " + _rb);
        Debug.Log("Renderers: " + _renderers.Length);
        Debug.Log("TMP_Text: " + _txt);
    }

    public override void OnNetworkSpawn()
    {
        _levelIndex.OnValueChanged += OnRemoteLevelChanged;
        
        if (_heros != null)
        {
            _heros.PlayerName.OnValueChanged += OnPlayerNameChanged;
        }

        if (IsOwner)
        {
            // Joueur local
            if (Levels.instance != null)
            {
                Debug.Log($"Placing local player at spawn for level {Levels.instance.currentLevel}");
                transform.position = Levels.instance.SpawnPosition;
                _levelIndex.Value = Levels.instance.currentLevel;
            }

            // Le pseudo côté owner sera géré dans Heros (voir section 2)
        }
        else
        {
            // --- GHOST ---

            // Désactiver TOUTES les collisions de ce joueur sur cette machine
            if (_colliders != null)
            {
                foreach (var col in _colliders)
                    col.enabled = false;
            }

            if (_rb != null)
            {
                _rb.isKinematic = true;
            }

            // Appliquer le material de fantôme à TOUS les renderers
            if (ghostMaterial != null && _renderers != null)
            {
                foreach (var r in _renderers)
                {
                    r.material = ghostMaterial;
                }
            }

            // Affichage du pseudo 
            if (_txt != null)
            {
                _txt.enabled = true;
                UpdateNameFromHeros();
            }
        }
        
        RefreshVisibility();
    }
    
    private void UpdateNameFromHeros()
    {
        if (_txt == null || _heros == null) return;

        var nameValue = _heros.PlayerName.Value.ToString();
        _txt.text = nameValue;
        _txt.gameObject.SetActive(!string.IsNullOrEmpty(nameValue));
    }
    
    private void OnDestroy()
    {
        _levelIndex.OnValueChanged -= OnRemoteLevelChanged;

        if (_heros != null)
        {
            _heros.PlayerName.OnValueChanged -= OnPlayerNameChanged;
        }
    }

    private void Update()
    {
        if (Levels.instance == null) return;

        int localLevel = Levels.instance.currentLevel;

        if (IsOwner)
        {
            // Le joueur local garde sa NetworkVariable de niveau à jour
            if (_levelIndex.Value != localLevel)
            {
                _levelIndex.Value = localLevel;
            }
        }
        else
        {
            // Ici on s'intéresse aux fantômes : si NOTRE niveau local change,
            // il faut re-appliquer la règle de visibilité
            RefreshVisibility();
        }
    }
    
    private void RefreshVisibility()
    {
        if (Levels.instance == null || _renderers == null)
            return;

        // On ne gère la visibilité que pour les fantômes (pas pour notre propre joueur)
        if (IsOwner) return;

        bool sameLevelAsLocal = (_levelIndex.Value == Levels.instance.currentLevel);
        bool visible = sameLevelAsLocal;

        // On laisse l'objet réseau actif, on masque juste le rendu + le pseudo
        foreach (var r in _renderers)
        {
            if (r != null)
                r.enabled = visible;
        }

        if (_txt != null)
        {
            _txt.enabled = visible;
        }
    }
    
    private void OnRemoteLevelChanged(int previous, int current)
    {
        string playerName = _heros != null ? _heros.PlayerName.Value.ToString() : "?";
        Debug.Log($"[LAN] OnRemoteLevelChanged called for player \"{playerName}\" from {previous} to {current}");

        RefreshVisibility();
    }
    
    
    private void OnPlayerNameChanged(FixedString64Bytes previous, FixedString64Bytes current)
    {
        UpdateNameFromHeros();
    }
}