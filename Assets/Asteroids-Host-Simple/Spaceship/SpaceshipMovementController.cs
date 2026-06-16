using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

namespace Asteroids.HostSimple
{
    public class SpaceshipMovementController : NetworkBehaviour
    {
        [SerializeField] private float _rotationSpeed = 90.0f;
        [SerializeField] private float _movementSpeed = 2000.0f;
        [SerializeField] private float _maxSpeed = 200.0f;

        private Rigidbody _rigidbody = null;
        private SpaceshipController _spaceshipController = null;

        [Networked] private float _screenBoundaryX { get; set; }
        [Networked] private float _screenBoundaryY { get; set; }

        public override void Spawned()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _spaceshipController = GetComponent<SpaceshipController>();

            if (Object.HasStateAuthority == false) return;

            // Camera.main puede ser null justo al Spawned, esperamos un frame
            if (Camera.main != null)
            {
                _screenBoundaryX = Camera.main.orthographicSize * Camera.main.aspect;
                _screenBoundaryY = Camera.main.orthographicSize;
            }
            else
            {
                // Fallback con valores típicos si la cámara aún no está lista
                _screenBoundaryX = 30f;
                _screenBoundaryY = 20f;
            }
        }

        public override void FixedUpdateNetwork()
        {
            Debug.Log($"Movement - HasInputAuthority: {Object.HasInputAuthority} | HasStateAuthority: {Object.HasStateAuthority} | AcceptInput: {_spaceshipController.AcceptInput}");

            if (_spaceshipController.AcceptInput == false) return;

            if (Runner.TryGetInputForPlayer<SpaceshipInput>(Object.InputAuthority, out var input))
            {
                Debug.Log($"Input recibido - H: {input.HorizontalInput} V: {input.VerticalInput}");
                Move(input);
            }

            if (Object.HasStateAuthority)
                CheckExitScreen();
        }

        private void Move(SpaceshipInput input)
        {
            Quaternion rot = _rigidbody.rotation *
                             Quaternion.Euler(0, input.HorizontalInput * _rotationSpeed * Runner.DeltaTime, 0);
            _rigidbody.MoveRotation(rot);
            Vector3 force = (rot * Vector3.forward) * input.VerticalInput * _movementSpeed * Runner.DeltaTime;
            _rigidbody.AddForce(force);
            if (_rigidbody.velocity.magnitude > _maxSpeed)
            {
                _rigidbody.velocity = _rigidbody.velocity.normalized * _maxSpeed;
            }
        }

        private void CheckExitScreen()
        {
            if (Object.HasStateAuthority == false) return;
            if (_screenBoundaryX == 0 || _screenBoundaryY == 0) return;

            var position = _rigidbody.position;
            if (Mathf.Abs(position.x) < _screenBoundaryX && Mathf.Abs(position.z) < _screenBoundaryY) return;

            if (Mathf.Abs(position.x) > _screenBoundaryX)
                position = new Vector3(-Mathf.Sign(position.x) * _screenBoundaryX, 0, position.z);

            if (Mathf.Abs(position.z) > _screenBoundaryY)
                position = new Vector3(position.x, 0, -Mathf.Sign(position.z) * _screenBoundaryY);

            position -= position.normalized * 0.1f;

            // Mover directamente el rigidbody en vez de Teleport
            _rigidbody.position = position;
            _rigidbody.velocity = _rigidbody.velocity; // mantener velocidad
        }
    }
}