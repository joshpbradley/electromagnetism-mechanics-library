using UnityEngine;

namespace ElectromagnetismMechanicsLibrary
{
    /// <summary>
    /// A representation of a magnetic dipole, that can simulate the motion of charged particles/plasma due to the magnetic dipole's magnetic field.
    /// This class can affect the motion of <see cref="ChargedParticleSystem"/> particles.
    /// </summary>
    [DisallowMultipleComponent]
    public class MagneticDipole : MonoBehaviour
    {
        /// <summary>
        /// The strength of the magnetic dipole's magnetic field. 
        /// </summary>
        /// <remarks>The strength should be decided in conjunction with the velocity of incoming <see cref="ChargedParticleSystem"/> particles.<remarks>
        [Tooltip("The strength of the magnetic field. This needs to be a positive value. Note: particle displacement relative to this object's transform; particle velocity; particle charge and the Particle System (external forces module) multiplier will all affect magnetic field influence as well as Strength.")]
        [SerializeField]
        float _strength = 1;

        public float Strength
        {
            get => _strength;
            set => _strength = Mathf.Abs(value);
        }

        void Awake()
        {
            // Checks whether Strength is negative, and sets to positive if it is.
            if (Strength < 0)
            {
                Debug.LogWarning("Strength should be a positive value. Automatically converted to a positive value. " +
                    "To reverse the Magnetic Flux Density vectors, rotate the Magnetic Dipole.");
                Strength = Strength;
            }
        }

        /// <summary>
        /// Calculates and returns the magnetic flux density vector, B, of a magnetic dipole at the provided location, <paramref name="position"/>.
        /// The <paramref name="position"/> value should be that of a charged particle, to determine how the particle's motion should be affected.
        /// </summary>
        /// <remarks>
        /// This method can be overriden to achieve different effects.
        /// This implementation accurately simulates charged particle caused by a magnetic dipole.
        /// The <see cref="Strength"/> field that is editable in the inspector has an effect on the value returned.
        /// </remarks>
        /// <param name="position">The location to compute the magneic flux density vector for.</param>
        /// <returns>The magnetic flux density vector induced by the magnetic dipole at location <paramref name="position"/>.</returns>
        public virtual Vector3 CalculateBVectorAtPosition(Vector3 position)
        {
            // The magnetic dipole moment of the magnetic dipole. Quantifies the magnet's orientation and strength.
            Vector3 magneticDipoleMoment = Strength * transform.TransformDirection(Vector3.up);
            // The displacement vector between the supplied position and the magnetic dipole.
            Vector3 displacement = position - transform.position;
            // The magnitude of the displacement vector.
            Vector3 unitDisplacement = Vector3.Normalize(displacement);

            // The formula for calculating magnetic flux density: 3 * _r * (_r . m) - m) / |r|^3 where "_" indicates unit vector.
            return (3 * unitDisplacement * Vector3.Dot(unitDisplacement, magneticDipoleMoment) - magneticDipoleMoment) / Mathf.Pow(displacement.magnitude, 3);
        }

        /// <summary>
        /// Draws a bar magnet gizmo to represent the magnetic dipole.
        /// </summary>
        /// <remarks>The drawing rotates to align with the object's orientation - indicating the magnetic field direction.</remarks>
        void OnDrawGizmos()
        {
            // The size of each half of the bar magnet.
            Vector3 magnetHalfScale = new(.25f, .4f, .25f);

            // Transforms the drawing space of the gizmo to account for the object's rotation.
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

            // Draws the red half of the magnet.
            Gizmos.color = Color.red;
            Gizmos.DrawCube(new Vector3(0, magnetHalfScale.y / 2, 0), magnetHalfScale);

            // Draws the blue half of the magnet.
            Gizmos.color = Color.blue;
            Gizmos.DrawCube(-new Vector3(0, magnetHalfScale.y / 2, 0), magnetHalfScale);

            // Draws a white outline around the magnet.
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(magnetHalfScale.x, magnetHalfScale.y * 2, magnetHalfScale.z));
        }
    }
}