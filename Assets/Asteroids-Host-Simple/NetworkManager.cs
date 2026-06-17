using Asteroids.HostSimple;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public static NetworkRunner runnerInstance;

    [SerializeField] private string lobbyName = "default";

    [SerializeField] private Transform sessionListContentParet;
    [SerializeField] private GameObject sessionListEntryPrefab;
    private readonly Dictionary<string, GameObject> sessionListUiDictionary = new();

    [SerializeField] private string gameplayScene;
    [SerializeField] private string lobbyScene;

    [SerializeField] private PlayerData _playerDataPrefab = null;

    [SerializeField] private TMP_InputField _roomNameInput = null;
    [SerializeField] private TMP_InputField _nickNameInput = null;

    private bool isInLobby;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Debug.Log("NetworkManager Awake - DontDestroyOnLoad aplicado");

        runnerInstance = GetComponent<NetworkRunner>();
        if (runnerInstance == null)
            runnerInstance = gameObject.AddComponent<NetworkRunner>();

        runnerInstance.AddCallbacks(this);
        runnerInstance.ProvideInput = true;
    }

    private void Start()
    {
        runnerInstance.JoinSessionLobby(SessionLobby.Shared, lobbyName);
    }

    public void CreateRandomSession()
    {
        string roomName = !string.IsNullOrWhiteSpace(_roomNameInput.text)
            ? _roomNameInput.text
            : "Room-" + UnityEngine.Random.Range(1000, 9999);

        StartGame(roomName);
    }

    public void JoinRoom(string roomName)
    {
        StartGame(roomName);
    }

    private async void StartGame(string sessionName)
    {
        if (!isInLobby)
        {
            Debug.LogError("Aún no estás conectado al lobby");
            return;
        }

        var playerData = FindObjectOfType<PlayerData>();
        if (playerData == null)
        {
            playerData = Instantiate(_playerDataPrefab);
        }

        if (_nickNameInput != null && !string.IsNullOrWhiteSpace(_nickNameInput.text))
            playerData.SetNickName(_nickNameInput.text);
        else
            playerData.SetNickName(PlayerData.GetRandomNickName());

        await runnerInstance.StartGame(new StartGameArgs()
        {
            SessionName = sessionName,
            GameMode = GameMode.Shared,
            Scene = SceneRef.FromIndex(GetSceneIndex(gameplayScene)),
            IsVisible = true,
            PlayerCount = 4
        });
    }

    private int GetSceneIndex(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (name == sceneName)
                return i;
        }
        return -1;
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        isInLobby = true;
        DeleteOld(sessionList);
        UpdateOrCreate(sessionList);
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log($"SceneLoadDone - IsMasterClient: {runner.IsSharedModeMasterClient} | ProvideInput: {runner.ProvideInput}");

        var inputPoller = GetComponent<LocalInputPoller>();
        Debug.Log($"LocalInputPoller en NetworkManager: {inputPoller != null}");
    }

    private void UpdateOrCreate(List<SessionInfo> sessionList)
    {
        foreach (var session in sessionList)
        {
            if (sessionListUiDictionary.TryGetValue(session.Name, out GameObject entry))
                UpdateEntry(entry, session);
            else
                CreateEntry(session);
        }
    }

    private void CreateEntry(SessionInfo session)
    {
        GameObject entry = Instantiate(sessionListEntryPrefab);
        entry.transform.SetParent(sessionListContentParet, false);

        SessionListEntry script = entry.GetComponent<SessionListEntry>();

        sessionListUiDictionary.Add(session.Name, entry);

        script.roomName.text = session.Name;
        script.playerCount.text = $"{session.PlayerCount}/{session.MaxPlayers}";
        script.joinButton.interactable = session.IsOpen;

        entry.SetActive(session.IsVisible);
    }

    private void UpdateEntry(GameObject entry, SessionInfo session)
    {
        SessionListEntry script = entry.GetComponent<SessionListEntry>();

        script.roomName.text = session.Name;
        script.playerCount.text = $"{session.PlayerCount}/{session.MaxPlayers}";
        script.joinButton.interactable = session.IsOpen;

        entry.SetActive(session.IsVisible);
    }

    private void DeleteOld(List<SessionInfo> sessionList)
    {
        List<string> remove = new();

        foreach (var kvp in sessionListUiDictionary)
        {
            bool exists = sessionList.Exists(s => s.Name == kvp.Key);

            if (!exists)
            {
                Destroy(kvp.Value);
                remove.Add(kvp.Key);
            }
        }

        foreach (var key in remove)
            sessionListUiDictionary.Remove(key);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        if (runnerInstance != null)
        {
            Destroy(runnerInstance.gameObject);
            runnerInstance = null;
        }
        SceneManager.LoadScene(lobbyScene);
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        // Si el jugador que se fue era el MasterClient, forzamos que todos vuelvan al lobby
        if (runner.ActivePlayers.Count() <= 1 || WasMasterClient(player))
        {
            runner.Shutdown();
        }
    }

    private bool WasMasterClient(PlayerRef player)
    {
        // En Shared mode el primer jugador (PlayerId más bajo, normalmente 1) suele ser el creador
        return player.PlayerId == 1;
    }

    private void ReclaimOrphanedObjects(NetworkRunner runner)
    {
        var gameState = FindObjectOfType<GameStateController>();
        if (gameState != null && gameState.Object != null && !gameState.Object.HasStateAuthority)
            gameState.Object.RequestStateAuthority();

        var asteroidSpawner = FindObjectOfType<AsteroidSpawner>();
        if (asteroidSpawner != null && asteroidSpawner.Object != null && !asteroidSpawner.Object.HasStateAuthority)
            asteroidSpawner.Object.RequestStateAuthority();

        // Reclamar todos los asteroides huérfanos
        var asteroids = FindObjectsOfType<AsteroidBehaviour>();
        foreach (var asteroid in asteroids)
        {
            if (asteroid.Object != null && !asteroid.Object.HasStateAuthority)
                asteroid.Object.RequestStateAuthority();
        }
    }

    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}