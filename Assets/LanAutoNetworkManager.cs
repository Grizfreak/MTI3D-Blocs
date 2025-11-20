using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Auto LAN bootstrap :
/// - essaie de trouver un host existant (broadcast UDP)
/// - sinon devient host
/// - vérifie la version via ConnectionApproval
/// </summary>
[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(UnityTransport))]
public class LanAutoNetworkManager : MonoBehaviour
{
    [Header("Paramètres réseau LAN")]
    [SerializeField] private ushort gamePort = 7777;
    [SerializeField] private ushort discoveryPort = 7778;
    [SerializeField] private float discoveryTimeoutSeconds = 0.75f;
    
    private string _buildVersion = "1.0.0";

    private NetworkManager _networkManager;
    private UnityTransport _transport;
    private CancellationTokenSource _discoveryCts;

    private const string DiscoveryRequestPrefix = "DISCOVER_GHOST_GAME";
    private const string DiscoveryResponsePrefix = "GHOST_GAME_HOST";

    private void Awake()
    {
        _buildVersion = Application.version;
        _networkManager = GetComponent<NetworkManager>();
        _transport = GetComponent<UnityTransport>();

        if (_networkManager == null || _transport == null)
        {
            Debug.LogError("LanAutoNetworkManager doit être sur le même GameObject que NetworkManager + UnityTransport.");
            enabled = false;
            return;
        }

        // Approval : vérification de version côté host
        _networkManager.ConnectionApprovalCallback = ApprovalCheck;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private async void Start()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log($"[LAN] buildVersion = {_buildVersion}, gamePort = {gamePort}, discoveryPort = {discoveryPort}");
        
        // 1. Essayer de trouver un host déjà présent sur le LAN
        var hostEndPoint = await TryDiscoverHostAsync();

        if (hostEndPoint != null)
        {
            Debug.Log($"[LAN] Host trouvé sur {hostEndPoint.Address}:{hostEndPoint.Port}, démarrage en client.");
            StartAsClient(hostEndPoint);
        }
        else
        {
            Debug.Log("[LAN] Aucun host trouvé, démarrage en host.");
            StartAsHost();
            // 2. Si on est host, on se met à écouter les futurs broadcasts
            StartDiscoveryListener();
        }
    }

    /// <summary>
    /// Envoie un broadcast UDP et attend une réponse d'un host éventuel.
    /// </summary>
    private async Task<IPEndPoint> TryDiscoverHostAsync()
    {
        using (var udp = new UdpClient())
        {
            udp.EnableBroadcast = true;
            udp.MulticastLoopback = false;

            string msg = $"{DiscoveryRequestPrefix};{_buildVersion}";
            byte[] data = Encoding.UTF8.GetBytes(msg);
            var broadcast = new IPEndPoint(IPAddress.Broadcast, discoveryPort);

            try
            {
                await udp.SendAsync(data, data.Length, broadcast);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LAN] Erreur d'envoi broadcast: {e.Message}");
                return null;
            }

            var receiveTask = udp.ReceiveAsync();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(discoveryTimeoutSeconds));

            var finished = await Task.WhenAny(receiveTask, timeoutTask);
            if (finished != receiveTask)
            {
                // Timeout → aucun host ne s'est manifesté
                return null;
            }

            UdpReceiveResult result;
            try
            {
                result = receiveTask.Result;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LAN] Erreur de réception broadcast: {e.Message}");
                return null;
            }

            string text = Encoding.UTF8.GetString(result.Buffer);
            // Format attendu : "GHOST_GAME_HOST;port;version"
            string[] parts = text.Split(';');
            if (parts.Length < 3 || parts[0] != DiscoveryResponsePrefix)
                return null;

            if (parts[2] != _buildVersion)
            {
                Debug.LogWarning($"[LAN] Host trouvé mais version différente ({parts[2]}), on l'ignore.");
                return null;
            }

            if (!ushort.TryParse(parts[1], out ushort hostPort))
                return null;

            return new IPEndPoint(result.RemoteEndPoint.Address, hostPort);
        }
    }

    /// <summary>
    /// On a trouvé un host → on se connecte en client sur son IP/port.
    /// </summary>
    private void StartAsClient(IPEndPoint hostEndPoint)
    {
        // Pour un client : address = IP du host, port = port du host
        _transport.SetConnectionData(hostEndPoint.Address.ToString(), (ushort)hostEndPoint.Port);
        // On encode aussi la version dans le payload de connexion
        _networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(_buildVersion);

        _networkManager.StartClient();
    }

    /// <summary>
    /// Aucun host trouvé → on devient host.
    /// </summary>
    private void StartAsHost()
    {
        // Pour le host : "0.0.0.0" écoute sur toutes les interfaces réseau
        _transport.SetConnectionData("0.0.0.0", gamePort, "0.0.0.0");
        _networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(_buildVersion);

        _networkManager.StartHost();
    }

    /// <summary>
    /// Côté host : boucle en tâche de fond qui répond aux broadcasts DISCOVER.
    /// </summary>
    private void StartDiscoveryListener()
    {
        _discoveryCts = new CancellationTokenSource();
        var token = _discoveryCts.Token;

        Task.Run(async () =>
        {
            using (var udp = new UdpClient(discoveryPort))
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var result = await udp.ReceiveAsync();

                        string text = Encoding.UTF8.GetString(result.Buffer);
                        string[] parts = text.Split(';');
                        if (parts.Length < 2 || parts[0] != DiscoveryRequestPrefix)
                            continue;

                        string clientVersion = parts[1];
                        if (clientVersion != _buildVersion)
                        {
                            // On ignore les clients de version différente
                            continue;
                        }

                        // Réponse : "GHOST_GAME_HOST;port;version"
                        string responseText = $"{DiscoveryResponsePrefix};{gamePort};{_buildVersion}";
                        byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
                        await udp.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // udp fermé, on sort proprement
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LAN] Erreur listener discovery: {e.Message}");
                }
            }
        }, token);
    }

    private void OnDestroy()
    {
        if (_discoveryCts != null)
        {
            _discoveryCts.Cancel();
            _discoveryCts.Dispose();
            _discoveryCts = null;
        }

        if (_networkManager != null)
        {
            _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    /// <summary>
    /// Vérification de la version côté host (ConnectionApproval).
    /// </summary>
    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
                               NetworkManager.ConnectionApprovalResponse response)
    {
        var payload = request.Payload;
        string clientVersion = (payload != null && payload.Length > 0)
            ? Encoding.UTF8.GetString(payload)
            : "UNKNOWN";

        if (clientVersion != _buildVersion)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = $"Version incompatible (serveur {_buildVersion}, client {clientVersion})";
            return;
        }

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Reason = string.Empty;
    }

    /// <summary>
    /// Permet de récupérer la raison de déconnexion côté client.
    /// </summary>
    private void OnClientDisconnected(ulong clientId)
    {
        if (_networkManager.IsServer)
        {
            Debug.Log($"[LAN] Client {clientId} déconnecté.");
        }
        else if (clientId == _networkManager.LocalClientId)
        {
            var reason = _networkManager.DisconnectReason;
            if (!string.IsNullOrEmpty(reason))
            {
                Debug.LogWarning($"[LAN] Déconnecté du host : {reason}");
                // Ici tu peux afficher la raison dans ton UI
            }
        }
    }
}
