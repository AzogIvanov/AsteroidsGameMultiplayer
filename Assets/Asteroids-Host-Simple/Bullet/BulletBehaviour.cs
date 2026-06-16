using Fusion;
using System.Collections.Generic;
using UnityEngine;
namespace Asteroids.HostSimple
{
    public class BulletBehaviour : NetworkBehaviour
    {
        [SerializeField] private float _maxLifetime = 3.0f;
        [SerializeField] private float _speed = 200.0f;
        [SerializeField] private LayerMask _asteroidLayer;

        [Networked] private TickTimer _currentLifetime { get; set; }

        public override void Spawned()
        {
            _currentLifetime = TickTimer.CreateFromSeconds(Runner, _maxLifetime);
        }

        public override void FixedUpdateNetwork()
        {
            //Debug.Log($"Bala - HasInputAuthority: {Object.HasInputAuthority} | HasStateAuthority: {Object.HasStateAuthority}");

            if (Object.HasInputAuthority == false) return;

            //Debug.Log("Bala pasó el guard");

            if (HasHitAsteroid() == false)
            {
                transform.Translate(transform.forward * _speed * Runner.DeltaTime, Space.World);
            }
            else
            {
                Runner.Despawn(Object);
                return;
            }
            CheckLifetime();
        }

        private void CheckLifetime()
        {
            if (_currentLifetime.Expired(Runner) == false) return;
            Runner.Despawn(Object);
        }

        private bool HasHitAsteroid()
        {
            var colliders = Physics.OverlapSphere(transform.position, 0.5f, _asteroidLayer);

            if (colliders.Length == 0) return false;

            foreach (var col in colliders)
            {
                var asteroid = col.GetComponent<AsteroidBehaviour>();
                if (asteroid == null) continue;
                if (!asteroid.IsAlive) continue;
                asteroid.HitAsteroid(Object.InputAuthority);
                return true;
            }

            return false;
        }
    }
}