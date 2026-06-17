using Fusion;
using UnityEngine;

namespace Asteroids.HostSimple
{
    public class SpaceshipSpawner : NetworkBehaviour, IPlayerJoined, IPlayerLeft
    {
        [SerializeField] private NetworkPrefabRef _spaceshipNetworkPrefab = NetworkPrefabRef.Empty;
        [Networked] private NetworkBool _gameIsReady { get; set; }
        private GameStateController _gameStateController = null;
        private SpawnPoint[] _spawnPoints = null;

        public override void Spawned()
        {
            _spawnPoints = FindObjectsOfType<SpawnPoint>();
            StartCoroutine(CheckAndSpawnSelf());
        }

        private System.Collections.IEnumerator CheckAndSpawnSelf()
        {
            // Esperar a que el juego esté listo (puede tardar si nos unimos antes de la cuenta atrás)
            while (!_gameIsReady)
            {
                yield return null;
            }

            if (!Runner.TryGetPlayerObject(Runner.LocalPlayer, out _))
            {
                SpawnSpaceship(Runner.LocalPlayer);
            }
        }

        public void StartSpaceshipSpawner(GameStateController gameStateController)
        {
            Debug.Log($"StartSpaceshipSpawner ejecutado por: {Runner.LocalPlayer}");
            _gameIsReady = true;
            _gameStateController = gameStateController;
        }

        public void PlayerJoined(PlayerRef player)
        {
            
        }

        private System.Collections.IEnumerator WaitAndSpawn()
        {
            // Esperamos hasta 2 segundos a que _gameIsReady se sincronice por red
            float timeout = 2f;
            while (!_gameIsReady && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (_gameIsReady && !Runner.TryGetPlayerObject(Runner.LocalPlayer, out _))
            {
                SpawnSpaceship(Runner.LocalPlayer);
            }
        }

        public void PlayerLeft(PlayerRef player)
        {
            DespawnSpaceship(player);
        }

        private void SpawnSpaceship(PlayerRef player)
        {
            Debug.Log($"SpawnSpaceship llamado para: {player}");
            int index = player.PlayerId % _spawnPoints.Length;
            var spawnPosition = _spawnPoints[index].transform.position;

            var playerObject = Runner.Spawn(
                _spaceshipNetworkPrefab,
                spawnPosition,
                Quaternion.identity,
                player
            );

            Debug.Log($"Nave creada - InputAuthority asignado: {playerObject.InputAuthority} | player pasado: {player} | Runner.LocalPlayer: {Runner.LocalPlayer}");

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