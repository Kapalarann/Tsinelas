using System;
using UnityEngine;

namespace Tsinelas.Puno
{
    /// <summary>
    /// Attached to the ball in the Puno Minigame.
    /// Detects when the spring joint breaks and fires an event.
    /// </summary>
    public class PunoBall : MonoBehaviour
    {
        public event Action OnBallFreed;

        private void OnJointBreak(float breakForce)
        {
            Debug.Log($"PunoBall: Joint broke with force {breakForce}");
            OnBallFreed?.Invoke();
        }
    }
}
