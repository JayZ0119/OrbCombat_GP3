using UnityEngine;

namespace OrbCombat
{
    // Add to each existing mouth/black-hole GameObject.
    public sealed class BlackHole : MonoBehaviour
    {
        [Min(0.01f)] public float baseRadius = 1.1f;
        public float EffectiveRadius => baseRadius * Mathf.Abs(transform.lossyScale.x);

        public bool Contains(Vector3 position) =>
            CombatPhysics.Flat(transform.position - position).magnitude <= EffectiveRadius;
    }
}
