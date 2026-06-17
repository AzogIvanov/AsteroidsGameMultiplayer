using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Asteroids.HostSimple
{
    public class SpaceshipController : NetworkBehaviour
    {
        [SerializeField] private float _respawnDelay = 4.0f;
        [SerializeField] private float _spaceshipDamageRadius = 2.5f;
        [SerializeField] private LayerMask _asteroidCollisionLayer;

        private ChangeDetector _changeDetector;
        private Rigidbody _rigidbody = null;
        private PlayerDataNetworked _playerDataNetworked = null;
        private SpaceshipVisualController _visualController = null;

        private List<LagCompensatedHit> _lagCompensatedHits = new List<LagCompensatedHit>();

        public bool AcceptInput => _isAlive && Object.IsValid;

        [Networked] private NetworkBool _isAlive { get; set; }
        [Networked] private TickTimer _respawnTimer { get; set; }

        public override void Spawned()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _playerDataNetworked = GetComponent<PlayerDataNetworked>();
            _visualController = GetComponent<SpaceshipVisualController>();
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

            _visualController.SetColorFromPlayerID(Object.InputAuthority.PlayerId);

            // En Shared mode el StateAuthority de la nave es el propio jugador
            if (Object.HasStateAuthority == false) return;
            _isAlive = true;
        }

        public override void Render()
        {
            foreach (var change in _changeDetector.DetectChanges(this, out var previousBuffer, out var currentBuffer))
            {
                switch (change)
                {
                    case nameof(_isAlive):
                        var reader = GetPropertyReader<NetworkBool>(nameof(_isAlive));
                        var (previous, current) = reader.Read(previousBuffer, currentBuffer);
                        ToggleVisuals(previous, current);
                        break;
                }
            }
        }

        private void ToggleVisuals(bool wasAlive, bool isAlive)
        {
            if (wasAlive == false && isAlive == true)
                _visualController.TriggerSpawn();
            else if (wasAlive == true && isAlive == false)
                _visualController.TriggerDestruction();
        }

        public override void FixedUpdateNetwork()
        {
            // Solo el StateAuthority (el propio jugador en Shared mode) escribe estado
            if (Object.HasStateAuthority)
            {
                if (_respawnTimer.Expired(Runner))
                {
                    _isAlive = true;
                    _respawnTimer = default;
                }

                if (_isAlive && HasHitAsteroid())
                {
                    ShipWasHit();
                }
            }
        }

        private bool HasHitAsteroid()
        {
            if (_rigidbody == null) return false;

            var colliders = Physics.OverlapSphere(_rigidbody.position, _spaceshipDamageRadius, _asteroidCollisionLayer);

            if (colliders.Length == 0) return false;

            colliders = colliders.OrderBy(c => Vector3.Distance(c.transform.position, _rigidbody.position)).ToArray();

            foreach (var col in colliders)
            {
                var asteroid = col.GetComponent<AsteroidBehaviour>();
                if (asteroid == null) continue;
                if (asteroid.Object == null || !asteroid.Object.IsValid) continue;

                if (!asteroid.IsAlive) continue;
                asteroid.HitAsteroid(PlayerRef.None);
                return true;
            }

            return false;
        }

        private void ShipWasHit()
        {
            _isAlive = false;
            ResetShip();

            if (_playerDataNetworked.Lives > 1)
            {
                _respawnTimer = TickTimer.CreateFromSeconds(Runner, _respawnDelay);
            }
            else
            {
                _respawnTimer = default;
            }

            _playerDataNetworked.SubtractLife();

            // Solo comprobar fin de juego si no quedan vidas
            if (_playerDataNetworked.Lives <= 0)
                FindObjectOfType<GameStateController>().CheckIfGameHasEnded();
        }

        private void ResetShip()
        {
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }
}