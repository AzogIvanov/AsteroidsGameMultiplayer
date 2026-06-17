using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using TMPro;
using UnityEngine;

namespace Asteroids.HostSimple
{
    public class GameStateController : NetworkBehaviour
    {
        enum GameState
        {
            Starting,
            Running,
            Ending
        }

        [SerializeField] private float _startDelay = 4f;
        [SerializeField] private float _endDelay = 4f;
        [SerializeField] private float _gameSessionLength = 180f;

        [SerializeField] private TextMeshProUGUI _startEndDisplay;
        [SerializeField] private TextMeshProUGUI _ingameTimerDisplay;

        [Networked] private TickTimer _timer { get; set; }
        [Networked] private GameState _gameState { get; set; }
        [Networked] private NetworkBehaviourId _winner { get; set; }

        private List<NetworkBehaviourId> _playerDataNetworkedIds = new();

        private bool _ready;

        public override void Spawned()
        {
            Debug.Log($"GameStateController Spawned - HasStateAuthority: {Object.HasStateAuthority} | LocalPlayer: {Runner.LocalPlayer}");

            _startEndDisplay.gameObject.SetActive(true);
            _ingameTimerDisplay.gameObject.SetActive(false);

            Runner.SetIsSimulated(Object, true);

            if (Object.HasStateAuthority)
            {
                _gameState = GameState.Starting;
                _timer = TickTimer.CreateFromSeconds(Runner, _startDelay);
            }
        }

        public void SetReady(bool value)
        {
            _ready = value;
        }

        public override void FixedUpdateNetwork()
        {
            Debug.Log($"GameStateController FixedUpdateNetwork - state: {_gameState} | HasStateAuthority: {Object.HasStateAuthority}");

            switch (_gameState)
            {
                case GameState.Starting:
                    UpdateStartingDisplay();
                    break;
                case GameState.Running:
                    UpdateRunningDisplay();
                    if (_timer.ExpiredOrNotRunning(Runner))
                    {
                        if (Object.HasStateAuthority)
                            DetermineWinnerAndEnd();
                    }
                    break;
                case GameState.Ending:
                    UpdateEndingDisplay();
                    break;
            }
        }
        private void DetermineWinnerAndEnd()
        {
            NetworkBehaviourId winnerId = default;
            int maxScore = -1;

            foreach (var id in _playerDataNetworkedIds)
            {
                if (Runner.TryFindBehaviour(id, out PlayerDataNetworked p))
                {
                    if (p.Score > maxScore)
                    {
                        maxScore = p.Score;
                        winnerId = id;
                    }
                }
            }

            _winner = winnerId;
            GameHasEnded();
        }

        private void UpdateStartingDisplay()
        {
            _startEndDisplay.text = $"Game Starts In {Mathf.RoundToInt(_timer.RemainingTime(Runner) ?? 0)}";

            if (!Object.HasStateAuthority) return;
            if (!_timer.ExpiredOrNotRunning(Runner)) return;

            var spawner = FindObjectOfType<SpaceshipSpawner>();
            if (spawner != null)
                spawner.StartSpaceshipSpawner(this);

            var asteroids = FindObjectOfType<AsteroidSpawner>();
            if (asteroids != null)
                asteroids.StartAsteroidSpawner();

            _gameState = GameState.Running;
            _timer = TickTimer.CreateFromSeconds(Runner, _gameSessionLength);
        }

        private void UpdateRunningDisplay()
        {
            Debug.Log($"UpdateRunningDisplay ejecutado - local player: {Runner.LocalPlayer}");
            _startEndDisplay.gameObject.SetActive(false);
            _ingameTimerDisplay.gameObject.SetActive(true);

            _ingameTimerDisplay.text =
                $"{Mathf.RoundToInt(_timer.RemainingTime(Runner) ?? 0)} seconds left";
        }

        private void UpdateEndingDisplay()
        {
            if (!Runner.TryFindBehaviour(_winner, out PlayerDataNetworked playerData))
                return;

            _startEndDisplay.gameObject.SetActive(true);
            _ingameTimerDisplay.gameObject.SetActive(false);

            _startEndDisplay.text =
                $"{playerData.NickName} won with {playerData.Score} points. Disconnecting in {Mathf.RoundToInt(_timer.RemainingTime(Runner) ?? 0)}";

            if (_timer.ExpiredOrNotRunning(Runner))
                Runner.Shutdown();
        }

        public void TrackNewPlayer(NetworkBehaviourId id)
        {
            if (Object.HasStateAuthority)
            {
                if (!_playerDataNetworkedIds.Contains(id))
                    _playerDataNetworkedIds.Add(id);
            }
            else
            {
                RpcTrackNewPlayer(id);
            }
        }

        [Rpc(sources: RpcSources.All, targets: RpcTargets.StateAuthority)]
        private void RpcTrackNewPlayer(NetworkBehaviourId id)
        {
            if (!_playerDataNetworkedIds.Contains(id))
                _playerDataNetworkedIds.Add(id);
        }

        public void CheckIfGameHasEnded()
        {
            if (Object.HasStateAuthority)
            {
                DoCheckIfGameHasEnded();
            }
            else
            {
                RpcCheckIfGameHasEnded();
            }
        }

        [Rpc(sources: RpcSources.All, targets: RpcTargets.StateAuthority)]
        private void RpcCheckIfGameHasEnded()
        {
            DoCheckIfGameHasEnded();
        }

        private void DoCheckIfGameHasEnded()
        {
            int alive = 0;

            for (int i = 0; i < _playerDataNetworkedIds.Count; i++)
            {
                if (!Runner.TryFindBehaviour(_playerDataNetworkedIds[i], out PlayerDataNetworked p))
                    continue;
                if (p.Lives > 0) alive++;
            }

            if (alive > 0) return;

            NetworkBehaviourId winnerId = default;
            int maxScore = -1;

            foreach (var id in _playerDataNetworkedIds)
            {
                if (Runner.TryFindBehaviour(id, out PlayerDataNetworked p))
                {
                    if (p.Score > maxScore)
                    {
                        maxScore = p.Score;
                        winnerId = id;
                    }
                }
            }

            _winner = winnerId;
            GameHasEnded();
        }

        private void GameHasEnded()
        {
            _timer = TickTimer.CreateFromSeconds(Runner, _endDelay);
            _gameState = GameState.Ending;
        }
    }
}