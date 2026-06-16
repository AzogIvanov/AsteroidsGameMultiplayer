using Fusion;
using UnityEngine;

namespace Asteroids.HostSimple
{
    public class SpaceshipSpawner : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        [SerializeField] private NetworkPrefabRef _spaceshipNetworkPrefab = NetworkPrefabRef.Empty;
        private bool _gameIsReady = false;
        private GameStateController _gameStateController = null;
        private SpawnPoint[] _spawnPoints = null;

        public override void Spawned()
        {
            // En Shared mode todos necesitan los spawn points, quitamos el guard de StateAuthority
            _spawnPoints = FindObjectsOfType<SpawnPoint>();
        }

        public void StartSpaceshipSpawner(GameStateController gameStateController)
        {
            _gameIsReady = true;
            _gameStateController = gameStateController;

            foreach (var player in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(player, out _)) continue;
                SpawnSpaceship(player);
            }
        }

        public void PlayerJoined(PlayerRef player)
        {
            Debug.Log($"PlayerJoined - player: {player} | HasStateAuthority: {Object.HasStateAuthority} | gameIsReady: {_gameIsReady}");

            // Solo el MasterClient spawnea naves
            if (Object.HasStateAuthority == false) return;

            // Si el juego no ha empezado, StartSpaceshipSpawner lo hará
            if (_gameIsReady == false) return;

            // Si ya tiene nave no spawneamos otra
            if (Runner.TryGetPlayerObject(player, out _)) return;

            SpawnSpaceship(player);
        }

        public void PlayerLeft(PlayerRef player)
        {
            DespawnSpaceship(player);
        }

        private void SpawnSpaceship(PlayerRef player)
        {
            int index = player.PlayerId % _spawnPoints.Length;
            var spawnPosition = _spawnPoints[index].transform.position;

            var playerObject = Runner.Spawn(
                _spaceshipNetworkPrefab,
                spawnPosition,
                Quaternion.identity,
                inputAuthority: player
            );

            Runner.SetPlayerObject(player, playerObject);

            if (_gameStateController == null)
                _gameStateController = FindObjectOfType<GameStateController>();

            if (_gameStateController != null)
                _gameStateController.TrackNewPlayer(playerObject.GetComponent<PlayerDataNetworked>().Id);
            else
                Debug.LogWarning("GameStateController no encontrado");
        }

        private void DespawnSpaceship(PlayerRef player)
        {
            if (Runner.TryGetPlayerObject(player, out var spaceshipNetworkObject))
            {
                Runner.Despawn(spaceshipNetworkObject);
            }

            Runner.SetPlayerObject(player, null);
        }
    }
}