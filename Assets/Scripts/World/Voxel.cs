using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace TerrainEngine.Fluid.New
{
	/// <summary>
	/// Possible neighbours in 3D in Von Neumann's neighbourhood.
	/// </summary>
	public enum Neighbour
	{
		Forward,
		Backward,
		Top,
		Bottom,
		Right,
		Left
	}

	/// <summary>
	/// One voxel. The smallest unit in the world and a single cell in cellular automata used for fluid simulation.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public struct Voxel
	{
		#region constants

		/// <summary>
		/// Max volume allowed in one voxel.
		/// </summary>
		public const byte kMaxVolume = byte.MaxValue / 2;

		/// <summary>
		/// To convert Fluid or Solid values to float.
		/// </summary>
		public const float kByteToFloat = 1f / kMaxVolume;

		/// <summary>
		/// To convert byte values to float.
		/// </summary>
		public const float kByteMaxValueToFloat = 1f / byte.MaxValue;

		/// <summary>
		/// Proportional max value to share amongst voxel's horizontal neighbours.
		/// </summary>
		public const float kNeighbourShareFloat = 1f / (kNeighbourCount - 1);

		/// <summary>
		/// Voxels settled with fluid less than this will have their fluid removed.
		/// </summary>
		public const byte kEpsilon = kNeighbourCount - 1;

		/// <summary>
		/// Number of voxel's neighbours. Must correspond to number of members of Neighbour enum.
		/// </summary>
		public const int kNeighbourCount = 6;

		#endregion

		public readonly static Voxel Invalid;

		// TODO BitVector32 or BitArrays per chunk/block or lower the ranges and pack everything to just few bytes
		public byte Solid;
		public byte Fluid;
		public byte Viscosity;

		private ushort _settleCounter;
		private byte _settled;
		private byte _valid;

		#region bool wrappers

		/// <summary>
		/// Only unsettled voxels or voxels with unsettled neighbours are taken into account during simulation.
		/// Once the voxel becomes settled it can be assigned to a FluidComponent.
		/// Defaults to true.
		/// </summary>
		public bool Settled
		{
			get
			{
				return _settled == 0;
			}
			set
			{
				_settled = (byte)(value ? 0 : 1);
			}
		}

		/// <summary>
		/// Used instead of null check.
		/// Defaults to false.
		/// </summary>
		public bool Valid
		{
			get
			{
				return _valid == 1;
			}
			set
			{
				_valid = (byte)(value ? 1 : 0);
			}
		}

		#endregion

		// TODO faster?
		/// <summary>
		/// Marks voxel as unsettled so that it takes part in the simulation.
		/// Also its settle counter is raised by the given difference in volume since the previous state.
		/// </summary>
		public void Unsettle(int diff)
		{
			_settleCounter = (ushort)math.min(_settleCounter + math.abs(diff), ushort.MaxValue);
			Settled = false;
		}

		/// <summary>
		/// Decreases the settle counter by voxel's viscosity in order for it to reach a settled state.
		/// </summary>
		public void DecreaseSettle()
		{
			// ready to settle or air
			if (_settleCounter == 0 || (Solid == 0 && Fluid == 0))
			{
				Settle();
			}
			else
			{
				int value = math.select((int)Viscosity, byte.MaxValue, Viscosity == 0);

				_settleCounter = (ushort)math.max(_settleCounter - value, 0);
			}
		}

		/// <summary>
		/// Puts voxel to a settled state.
		/// </summary>
		public void Settle()
		{
			if (Fluid <= kEpsilon && Solid + Fluid < kMaxVolume)
			{
				Fluid = 0;
				Viscosity = 0;
			}

			if (Fluid > kMaxVolume - Solid)
			{
				Fluid = (byte)(kMaxVolume - Solid);
			}

			_settleCounter = 0;
			Settled = true;
		}

		/// <summary>
		/// Returns true if the neighbour is not null and has a compatible viscosity.
		/// </summary>
		public bool HasCompatibleViscosity(in Voxel neighbour)
		{
			return neighbour.Valid && (Viscosity == 0 || neighbour.Viscosity == 0 || Viscosity == neighbour.Viscosity);
		}

		public bool HasFluid
		{
			get
			{
				return Fluid > 0;
			}
		}

		public bool IsFull
		{
			get
			{
				return Solid + Fluid >= kMaxVolume;
			}
		}

		public byte CurrentVolume
		{
			get
			{
				return (byte)(Solid + Fluid);
			}
		}

		public byte ExcessVolume
		{
			get
			{
				return (byte)math.max(Solid + Fluid - kMaxVolume, 0);
			}
		}

		public byte FreeVolume
		{
			get
			{
				return (byte)math.max(kMaxVolume - Solid - Fluid, 0);
			}
		}

		public override string ToString()
		{
			return String.Format("{0}: {1}/{2}", Settled, Fluid.ToString(), Solid.ToString());
		}
	}
}