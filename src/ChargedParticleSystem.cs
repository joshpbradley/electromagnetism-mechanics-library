using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ElectromagnetismMechanicsLibrary
{
	/// <summary>
	/// Modifies a <see cref="ParticleSystem"/> component to allow particles to simulate charged particle motion in a magnetic field.
	/// </summary>
	/// <remarks>This component requires the attached GameObject to also have a <see cref="ParticleSystem"/> component attached.</remarks>
	[RequireComponent(typeof(ParticleSystem))]
	[DisallowMultipleComponent]
	public class ChargedParticleSystem : MonoBehaviour
	{
		/// <summary>
		/// Defines a pair of inclusive bounds for assigning particle charge values.
		/// If multiple <see cref="ChargeBoundPair"/> instances are defined for a single <see cref="ChargedParticleSystem"/>,
		/// an instance from the _chargeBounds array is selected at random.
		/// Once a <see cref="ChargeBoundPair"/> instance has been selected, a random value within this inclusive range
		/// is individually assigned to each particle
		/// </summary>
		[System.Serializable]
		sealed internal class ChargeBoundPair : System.IEquatable<ChargeBoundPair>
        {
			/// <summary>
			/// The first bound that can determine the charge value assigned to a charged particle.
			/// </summary>
			[SerializeField]
			internal int _boundOne = 1;
			/// <summary>
			/// The first bound that can determine the charge value assigned to a charged particle.
			/// </summary>
			[SerializeField]
			internal int _boundTwo = 1;

			/// <summary>
			/// Constructor for the <see cref="ChargeBoundPair"/> class.
			/// </summary>
			/// <param name="BoundOne">The first bound that can determine the charge value assigned to a charged particle.</param>
			/// <param name="BoundTwo">The first bound that can determine the charge value assigned to a charged particle.</param>
			internal ChargeBoundPair(int BoundOne, int BoundTwo)
			{
				_boundOne = BoundOne;
				_boundTwo = BoundTwo;

				BoundSwap();
			}

			/// <summary>
			/// Swaps the values stored in the Bound instance variables if BoundOne is less than BoundTwo.
			/// </summary>
			internal void BoundSwap()
			{
				if (_boundOne < _boundTwo)
				{
					// Swaps the ordering of the upper/lower bound pair tuple.
					(_boundOne, _boundTwo) = (_boundTwo, _boundOne);
				}
			}

			/// <summary>
			/// Redefines the equality of this type so that two objects are considered equal if they both 
			/// derive from the <see cref="ChargeBoundPair"/> class and share the same bound values.
			/// </summary>
			/// <param name="obj">The object that is compared to the this instance.</param>
			/// <returns>True if <paramref name="obj"/> and this instance both derive from the <see cref="ChargeBoundPair"/> class
			/// and share the same bounds.</returns>
			public bool Equals(ChargeBoundPair other)
            {
				return other._boundOne == _boundOne && other._boundTwo == _boundTwo;
			}

			internal static ChargeBoundPair[] RemoveDuplicates(ChargeBoundPair[] cbp)
            {
				return cbp.Distinct().ToArray();
            }
        }

		/// <summary>
		/// Holds the values of the bounds that determine the charge values of the charged particles.
		/// </summary>
		[Tooltip("A charge value is assigned to each particle by selecting a Charge Bounds element at random, and then selecting a random value between its corresponding Bound One and Bound Two values (inclusive).")]
		[SerializeField]
		ChargeBoundPair[] _chargeBounds;

		/// <summary>
		/// The collection of <see cref="MagneticDipole"/> components that can influence particles from this <see cref="ChargedParticleSystem"/>.
		/// </summary>
		[Tooltip("Assign the Magnetic Dipole component(s) that you want to influence this Charged Particle System's emitted particles.")]
		public MagneticDipole[] SubscribedMagneticDipoles;

		/// <summary>
		/// The collection of charge data and B data for each particle.
		/// Charge is stored in the w component, while the B vector is stored in the x, y, & z components.
		/// </summary>
		readonly List<Vector4> _chargeAndBData = new();

		/// <summary>
		/// Used to denote that a neutral charge for a particle has been assigned a charge value.
		/// </summary>
		const float NEUTRAL_FLAG = .5f;

		const ParticleSystemSimulationSpace psss = ParticleSystemSimulationSpace.World;

		/// <summary>
		/// The <see cref="ParticleSystem"/> component attached to this <see cref="ChargedParticleSystem"/> instance.
		/// </summary>
		ParticleSystem _attachedParticleSystem;

		public ParticleSystem AttachedParticleSystem
        {
			get => _attachedParticleSystem;
        }
			

		/// <summary>
		/// The collection of particles that are currently in transit.
		/// </summary>
		ParticleSystem.Particle[] _emittedParticles;

		void Awake()
		{
			_attachedParticleSystem = gameObject.GetComponent<ParticleSystem>();
			SubscribedMagneticDipoles = SubscribedMagneticDipoles.Distinct().ToArray();

			if (!SimulationCheck(true))
			{
				enabled = false;
				return;
			}

			// Remove duplicate values in the ChargeBound array.
			_chargeBounds = ChargeBoundPair.RemoveDuplicates(_chargeBounds);

			// The upper/lower bounds for ChargeBoundPair instances are swapped if their BoundOne value is less than the corresponding BoundTwo value.
			foreach (ChargeBoundPair cb in _chargeBounds)
			{
				cb.BoundSwap();
			}
		}

		void FixedUpdate()
		{
			// Disables this component if validation checks are not met.
			SimulationCheckRuntime();

			// Gets the particle instances associated with the _attachedParticleSystem member.
			_emittedParticles = new ParticleSystem.Particle[_attachedParticleSystem.particleCount];
			_attachedParticleSystem.GetParticles(_emittedParticles);

		
			_attachedParticleSystem.GetCustomParticleData(_chargeAndBData, ParticleSystemCustomData.Custom2);

			UpdateBData();
			UpdateChargeData();

			_attachedParticleSystem.SetCustomParticleData(_chargeAndBData, ParticleSystemCustomData.Custom2);

			/*
			 * Updates the velocities for he particles emitted by the _attachedParticleSystem field.
			 * Derived from the formula: F = E + q(vxB). (Where E is irrelevant to magnetic field interaction.)
			 */
			for (int i = 0; i < _attachedParticleSystem.particleCount; i++)
			{
				if (_chargeAndBData[i].w == NEUTRAL_FLAG || _emittedParticles[i].totalVelocity.magnitude == 0) continue;

				// The external forces module multiplier has influences the effect's strength.
				/*
				 * The cross produce value is negated to account for the difference between the left-handed coordinate system (Unity)
				 * compared to the right-handed coordinate system used in physics.
				 */
				_emittedParticles[i].velocity += _chargeAndBData[i].w * _attachedParticleSystem.externalForces.multiplier * Vector3.Cross(_chargeAndBData[i], _emittedParticles[i].totalVelocity);
			}

			// Sets the new velocities calculated for the _attachedParticleSystem member.
			_attachedParticleSystem.SetParticles(_emittedParticles);
		}

		/// <summary> Updates the charge values of emitted particles. The charge is only updated for particles that haven't had a charge applied to them previously.</summary>
		void UpdateChargeData()
		{
			float charge;
			ChargeBoundPair boundSelection;

			for (int i = 0; i < _chargeAndBData.Count; i++)
			{
				// True if the particle has had a charge assigned to it.
				if (_chargeAndBData[i].w != 0) continue;

				// Selects a random ChargeBoundPair instance for charge selection.
				boundSelection = _chargeBounds[_chargeBounds.Length == 1 ? 0 : Random.Range(0, _chargeBounds.Length)];

				charge = boundSelection._boundOne == boundSelection._boundTwo ? 
				boundSelection._boundOne : 
				Random.Range(boundSelection._boundTwo, boundSelection._boundOne + 1);

				/*
				 * If the selected charge value for the particle is zero then instead set it to the neutral flag.
				 * 
				 * This flag has the value of .5f, which cannot be assigned to a particle as it is non-integer.
				 * This means that if the charge value is found to be the flag value, velocity and B computation can be skipped.
				 */
				_chargeAndBData[i] = new Vector4(_chargeAndBData[i].x, _chargeAndBData[i].y, _chargeAndBData[i].z, charge == 0 ? NEUTRAL_FLAG : charge);
			}
		}

		/// <summary>
		/// Updates the B values of emitted particles relative to all subscribed <see cref="MagneticDipole"/> components.
		/// </summary>
		void UpdateBData()
		{
			Vector3 B;

			for (int i = 0; i < _chargeAndBData.Count; i++)
			{
				B = Vector3.zero;

				// Calculates the total B value acting on each particle, as a summation of the individial B values from each subscribed MagneticDipole instance.
				for (int j = 0; j < SubscribedMagneticDipoles.Length; j++)
				{
					B += SubscribedMagneticDipoles[j].CalculateBVectorAtPosition(_emittedParticles[i].position);
				}

				// Updates the magnetic flux density vector values.
				_chargeAndBData[i] = new Vector4(B.x, B.y, B.z, _chargeAndBData[i].w);
			}
		}

		/// <summary>
		/// Determines whether the internal state of this component passes validation for remaining enabled.
		/// the external forces module of the particle system isn't enabled;
		/// the number of ChargeBoundPair assigned to this component is zero, or
		/// the number of magnetic dipoles assigned to this component is zero.
		/// </summary>
		/// <remarks>
		/// This method should be used before attempting to enable the system so that the internal state is valid.
		/// It would have been better to update the getter and setter functions for the "enabled" property,
		/// but Unity have not marked this property as virtual in the base class, making this approach unusable.
		/// </remarks>
		public bool SimulationCheck(bool showWarnings)
		{
			bool canSimulate = true;

			if (!_attachedParticleSystem.externalForces.enabled)
			{
				if (showWarnings) Debug.LogWarning("The attached particle system's external force module is disabled. Enable it to affect particle motion.");
				canSimulate = false;
			}

			if (SubscribedMagneticDipoles.Length == 0)
			{
				if (showWarnings) Debug.LogWarning("A magnetic dipole hasn't been subscribed to a charged particle system instance.");
				canSimulate = false;
			}

			if (_chargeBounds.Length == 0)
			{
				if (showWarnings) Debug.LogError("At least one pair of charge bounds must be provided in the inspector.");
				canSimulate = false;
			}

			if (_attachedParticleSystem.main.simulationSpace != psss)
			{
				if (showWarnings) Debug.LogError("The Particle System component's Simulation Space must be set to World space.");
				canSimulate = false;
			}

			return canSimulate;
		}

		/// <summary>
		/// Disables the ChargedParticleSystem if
		/// the external forces module of the particle system isn't enabled, or
		/// the number of magnetic dipoles assigned to this component is zero.
		/// </summary>
		void SimulationCheckRuntime()
		{
			bool canSimulate = true;

			if (!_attachedParticleSystem.externalForces.enabled)
			{
				Debug.LogWarning("The attached particle system's external force module is disabled. Enable it to affect particle motion.");
				canSimulate = false;
			}

			if (SubscribedMagneticDipoles.Length == 0)
			{
				Debug.LogWarning("A Magnetic Dipole is not subscribed to a Charged Particle System component.");
				canSimulate = false;
			}

			if(_attachedParticleSystem.main.simulationSpace != psss)
            {
				Debug.LogError("The Particle System component's Simulation Space must be set to World space to simulate.");
				canSimulate = false;
			}

			if(!canSimulate)
            {
				enabled = false;
			}
		}

		/// <summary>
		/// Sets the <see cref="_chargeBounds"/> field's values to the values provided in <param name="replacementBounds">.
		/// </summary>
		/// <param name="replacementBounds"></param>
		/// <returns>True if the ChargeBound field has been updated, else false.</returns>
		/// <remarks>
		/// The ChargeBound field will only update if the <paramref name="replacementBounds"/> variable is not empty.
		/// Duplicate values will be removed prior to setting the <see cref="_chargeBounds"/> field.
		///</remarks>
		public bool SetChargeBounds(List<(int, int)> replacementBounds)
		{
			// Return if the list provided to update the bounds with is empty.
			if (replacementBounds.Count == 0) return false;

			// Variable for temporarily storing the provided bound values.
			var chargeBoundsTemp = new ChargeBoundPair[replacementBounds.Count];

			// Copies the bound data from the provided list into a temporary store.
			for(int i = 0; i < replacementBounds.Count; i++)
            {
				chargeBoundsTemp[i] = new ChargeBoundPair(replacementBounds[i].Item1, replacementBounds[i].Item2);
			}

			// Sets the ChargeBound field to the temporary store after removing duplicate values.
			_chargeBounds = ChargeBoundPair.RemoveDuplicates(chargeBoundsTemp);

			// Reset the charge values to zero - allowing them to be updated in the next FixedUpdate cycle.
			_attachedParticleSystem.GetCustomParticleData(_chargeAndBData, ParticleSystemCustomData.Custom2);

			// Update the B vector for the particles.
			for (int i = 0; i < _chargeAndBData.Count; i++)
			{
				_chargeAndBData[i] = new Vector4(_chargeAndBData[i].x, _chargeAndBData[i].y, _chargeAndBData[i].z, 0);
			}

			return true;
		}

		/// <summary>
		/// Gets a copy of the current <see cref="_chargeBounds"/> field in a List of (int, int) tuples.
		/// This method is designed to provide access to the state of the <see cref="_chargeBounds"/> array without needing to
		/// expose instances of the <see cref="ChargeBoundPair"/> class.
		/// 
		/// This method acts as a basis for updating the <see cref="_chargeBounds"/> array - the retrieved Tuple List from this
		/// method can be modified, which can then be input into the <see cref="SetChargeBounds"/> function
		/// to update the charges of particles spawned by this class.
		/// </summary>
		/// <returns>The list of tuples representation of the _chargeBounds array.</returns>
		public List<(int, int)> GetChargeBounds()
		{
			// A list of tuples that represents the ChargeBound field.
			var chargeBoundsCopy = new List<(int, int)>();

			// Copies the bound data from the ChargeBound field into the tuple list.
			for (int i = 0; i < _chargeBounds.Length; i++)
			{
				chargeBoundsCopy.Add((_chargeBounds[i]._boundOne, _chargeBounds[i]._boundTwo));
			}

			// Returns the tuple list holding the charge bound data.
			return chargeBoundsCopy;
		}
	}
}