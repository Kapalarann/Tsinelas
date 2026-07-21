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
        public event Action OnBallHit;



        private void OnCollisionEnter(Collision collision)
        {
            var slipper = collision.gameObject.GetComponentInParent<Tsinelas.TumbangPreso.TumbangPresoSlipper>();
            if (slipper != null)
            {
                OnBallHit?.Invoke();
            }
        }

        private void OnJointBreak(float breakForce)
        {
            Debug.Log($"PunoBall: Joint broke with force {breakForce}");
            OnBallFreed?.Invoke();
        }
    }
}
