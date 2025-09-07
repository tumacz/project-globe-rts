using System;
using UnityEngine;

namespace GlobeRTS.PlanetTerrain.Runtime.Update
{
    /// <summary>
    /// Controls on-demand refresh: updates more frequently while the camera moves,
    /// and less frequently when idle.
    /// </summary>
    public sealed class CameraChangeDetector
    {
        private readonly Camera _cam;
        private readonly Func<float> _getRadius;

        private readonly float _minInterval;
        private readonly float _idleInterval;

        private readonly float _posEpsRel;
        private readonly float _angEpsDeg;
        private readonly float _fovEpsDeg;

        private Vector3 _posPrev;
        private Quaternion _rotPrev;
        private float _fovPrev;
        private float _lastTime;
        private bool _force;

        public CameraChangeDetector(
            Camera cam,
            Func<float> getSphereRadius,
            float minInterval,
            float idleInterval,
            float posEpsRelative,
            float angEpsDeg,
            float fovEpsDeg
        )
        {
            _cam = cam;
            _getRadius = getSphereRadius;
            _minInterval = Mathf.Max(0.01f, minInterval);
            _idleInterval = Mathf.Max(0.01f, idleInterval);
            _posEpsRel = Mathf.Max(0f, posEpsRelative);
            _angEpsDeg = Mathf.Max(0f, angEpsDeg);
            _fovEpsDeg = Mathf.Max(0f, fovEpsDeg);

            Snapshot();
        }

        public void ForceOnce() => _force = true;

        public bool ShouldUpdate()
        {
            float now = Time.unscaledTime;
            bool moved = CameraChanged(out _);

            float interval = moved ? _minInterval : _idleInterval;
            if (_force || now - _lastTime >= interval)
            {
                Snapshot();
                _force = false;
                return true;
            }
            return false;
        }

        private bool CameraChanged(out float posDeltaSqr)
        {
            var tr = _cam.transform;
            float posEps = Mathf.Max(0.01f, _getRadius() * _posEpsRel);

            posDeltaSqr = (tr.position - _posPrev).sqrMagnitude;
            if (posDeltaSqr > posEps * posEps) return true;
            if (Quaternion.Angle(tr.rotation, _rotPrev) > _angEpsDeg) return true;
            if (Mathf.Abs(_cam.fieldOfView - _fovPrev) > _fovEpsDeg) return true;
            return false;
        }

        private void Snapshot()
        {
            _posPrev = _cam.transform.position;
            _rotPrev = _cam.transform.rotation;
            _fovPrev = _cam.fieldOfView;
            _lastTime = Time.unscaledTime;
        }
    }
}