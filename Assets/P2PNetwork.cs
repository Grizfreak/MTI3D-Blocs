using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Globalization;

public class P2PNetwork : MonoBehaviour
{
    public static P2PNetwork Instance { get; private set; }

    [Header("Réseau")]
    [SerializeField] private int port = 7777;
    [SerializeField] private float sendInterval = 0.05f; // 20 FPS réseau
    [SerializeField] private float remoteTimeout = 3f;   // 3s sans nouvelle → on détruit le ghost

    [Header("Ghosts")]
    [SerializeField] private GameObject ghostPrefab; // prefab d’un joueur fantôme (voir GhostPlayer plus bas)

    private UdpClient _udp;
    private IPEndPoint _broadcastEndPoint;
    private string _localId;
    private string _localName;

    private Heros _localHero;
    private float _sendTimer;

    private class RemotePlayer
    {
        public string Id;
        public string Name;
        public GameObject GameObject;
        public GhostPlayer Ghost;
        public float LastSeen;
    }

    private readonly Dictionary<string, RemotePlayer> _remotes = new Dictionary<string, RemotePlayer>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _localId = Guid.NewGuid().ToString();
        _localName = Environment.UserName;
        
        Debug.Log($"[P2P] LocalId={_localId}, LocalName={_localName}");
    }

    private async void Start()
    {
        try
        {
            _udp = new UdpClient(port);
            _udp.EnableBroadcast = true;
            _broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, port);

            Debug.Log($"[P2P] UDP ouvert sur le port {port}");

            // On lance l’écoute (async) sur le thread Unity
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError($"[P2P] Erreur d'ouverture UDP : {e.Message}");
        }
    }


    private void OnDestroy()
    {
        if (_udp != null)
        {
            _udp.Close();
            _udp = null;
        }
    }

    // --- API publique ---

    public void RegisterLocalHero(Heros hero)
    {
        _localHero = hero;
        Debug.Log($"[P2P] RegisterLocalHero: {_localHero.name}");
    }


    // --- Boucle d’envoi / timeout ---

    private void Update()
    {
        // Envoi régulier de l’état local
        if (_localHero != null && Levels.instance != null && _udp != null)
        {
            _sendTimer += Time.deltaTime;
            if (_sendTimer >= sendInterval)
            {
                _sendTimer = 0f;
                BroadcastLocalState();
            }
        }

        // Timeout des ghosts
        float now = Time.time;
        List<string> toRemove = null;

        foreach (var kvp in _remotes)
        {
            if (now - kvp.Value.LastSeen > remoteTimeout)
            {
                (toRemove ??= new List<string>()).Add(kvp.Key);
            }
        }

        if (toRemove != null)
        {
            foreach (var id in toRemove)
            {
                var rp = _remotes[id];
                if (rp.GameObject != null)
                    Destroy(rp.GameObject);
                _remotes.Remove(id);
                Debug.Log($"[P2P] Remote {id} timed out, ghost détruit.");
            }
        }
    }


    // --- Envoi de l’état local ---

    private void BroadcastLocalState()
    {
        if (Levels.instance == null || _localHero == null || _udp == null)
            return;

        Vector3 pos = _localHero.transform.position;
        int level = Levels.instance.currentLevel;

        // On force une culture invariante pour avoir un '.' comme séparateur
        string msg = string.Format(
            CultureInfo.InvariantCulture,
            "STATE|{0}|{1}|{2}|{3:F3}|{4:F3}|{5:F3}",
            _localId,
            _localName,
            level,
            pos.x,
            pos.y,
            pos.z
        );

        byte[] data = Encoding.UTF8.GetBytes(msg);

        try
        {
            _udp.Send(data, data.Length, _broadcastEndPoint);
            // Debug.Log($"[P2P] Envoi STATE: {msg}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[P2P] Erreur envoi UDP : {e.Message}");
        }
    }


    // --- Réception des messages ---

    private async Task ReceiveLoop()
    {
        Debug.Log("[P2P] ReceiveLoop démarrée");
    
        while (_udp != null)
        {
            UdpReceiveResult result;
            try
            {
                result = await _udp.ReceiveAsync();
            }
            catch (ObjectDisposedException)
            {
                Debug.Log("[P2P] ReceiveLoop arrêtée (socket fermée)");
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[P2P] Erreur ReceiveAsync : {e.Message}");
                continue;
            }

            string text = Encoding.UTF8.GetString(result.Buffer);
            // Debug.Log($"[P2P] Reçu brut : {text}");

            try
            {
                HandleMessage(text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[P2P] Erreur traitement message '{text}' : {e.Message}");
            }
        }
    }

    private void HandleMessage(string msg)
    {
        // Format attendu : STATE|id|name|level|x|y|z
        var parts = msg.Split('|');
        if (parts.Length < 7)
        {
            Debug.LogWarning($"[P2P] Message invalide (pas assez de champs) : {msg}");
            return;
        }

        if (parts[0] != "STATE")
        {
            Debug.LogWarning($"[P2P] Type de message inconnu : {msg}");
            return;
        }

        string id = parts[1];
        string name = parts[2];

        // Ignorer nos propres messages (comparaison exacte sur l'id)
        if (id == _localId)
        {
            // Debug.Log("[P2P] Message STATE ignoré (provenant de nous-mêmes).");
            return;
        }

        if (!int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
        {
            Debug.LogWarning($"[P2P] Impossible de parser level dans '{msg}'");
            return;
        }

        if (!float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
            !float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
            !float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
        {
            Debug.LogWarning($"[P2P] Impossible de parser position dans '{msg}'");
            return;
        }

        Vector3 pos = new Vector3(x, y, z);

        Debug.Log($"[P2P] STATE décodé: id={id}, name={name}, level={level}, pos={pos}");

        if (!_remotes.TryGetValue(id, out var remote))
        {
            // Nouveau joueur distant → créer un ghost
            if (ghostPrefab == null)
            {
                Debug.LogWarning("[P2P] ghostPrefab non assigné.");
                return;
            }

            GameObject go = Instantiate(ghostPrefab);
            var ghost = go.GetComponent<GhostPlayer>();
            if (ghost == null)
            {
                Debug.LogError("[P2P] ghostPrefab n'a pas de GhostPlayer.");
                Destroy(go);
                return;
            }

            ghost.SetName(name);
            ghost.SetLevelAndPosition(level, pos);

            remote = new RemotePlayer
            {
                Id = id,
                Name = name,
                GameObject = go,
                Ghost = ghost,
                LastSeen = Time.time
            };
            _remotes[id] = remote;

            Debug.Log($"[P2P] Nouveau joueur distant : {name} ({id})");
        }
        else
        {
            remote.Ghost.SetLevelAndPosition(level, pos);
            remote.LastSeen = Time.time;
            Debug.Log($"[P2P] Update joueur distant : {remote.Name} ({id}) -> level={level}, pos={pos}");
        }
    }

}
