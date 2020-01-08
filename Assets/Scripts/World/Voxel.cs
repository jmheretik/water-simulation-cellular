using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Voxel's possible neighbours.
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
/// One voxel. A single cell in cellular automata used for fluid simulation.
/// </summary>
public struct Voxel
{
	/// <summary>
	/// Max volume allowed in one voxel.
	/// </summary>
	public const byte MaxVolume = byte.MaxValue / 2;

	/// <summary>
	/// Voxels settled with fluid less than this will have their fluid removed or differences between 2 voxels less than this will not be considered.
	/// </summary>
	public const byte Epsilon = NeighbourCount - 1;

	/// <summary>
	/// Number of voxel's neighbours. Must correspond to number of members of Neighbour enum.
	/// </summary>
	public const int NeighbourCount = 6;

	public byte solid;
	public byte fluid;
	public byte viscosity;

	public short settleCounter;

	public bool valid;
	public bool settled;
	public bool teleporting;

	/// <summary>
	/// Reset settle counter to its initial value.
	/// </summary>
	public void Unsettle()
	{
		settleCounter = 30 * byte.MaxValue;
		settled = false;
	}

	/// <summary>
	/// Decreases the settle counter in order for voxel to reach a settled state.
	/// </summary>
	public void DecreaseSettle()
	{
		if (IsAir)
		{
			Settle();
			return;
		}

		if (settleCounter <= 0)
		{
			Settle();
		}
		else
		{
			if (viscosity == 0)
			{
				settleCounter -= byte.MaxValue;
			}
			else
			{
				settleCounter -= viscosity;
			}
		}
	}

	/// <summary>
	/// Puts voxel to a settled state.
	/// </summary>
	public void Settle()
	{
		if (fluid < Epsilon && solid + fluid < MaxVolume)
		{
			fluid = 0;
			viscosity = 0;
		}

		if (fluid > MaxVolume - solid)
		{
			fluid = (byte)(MaxVolume - solid);
		}

		settleCounter = 0;
		settled = true;

		teleporting = false;
	}

	/// <summary>
	/// Returns true if the neighbour is not null and has a compatible viscosity.
	/// </summary>
	public bool HasCompatibleViscosity(ref Voxel neighbour)
	{
		return neighbour.valid && (viscosity == 0 || neighbour.viscosity == 0 || viscosity == neighbour.viscosity);
	}

	public bool HasFluid
	{
		get
		{
			return fluid > 0;
		}
	}

	public bool IsFull
	{
		get
		{
			return solid + fluid >= MaxVolume;
		}
	}

	public bool IsAir
	{
		get
		{
			return solid == 0 && fluid == 0;
		}
	}

	public bool IsTerrain
	{
		get
		{
			return solid == MaxVolume && fluid == 0;
		}
	}

	public byte CurrentVolume
	{
		get
		{
			return (byte)(solid + fluid);
		}
	}

	public byte ExcessFluid
	{
		get
		{
			return (byte)(solid + fluid <= MaxVolume ? 0 : solid + fluid > byte.MaxValue ? byte.MaxValue - MaxVolume : solid + fluid - MaxVolume);
		}
	}

	public byte FreeVolume
	{
		get
		{
			return (byte)(solid + fluid <= MaxVolume ? MaxVolume - solid - fluid : 0);
		}
	}

	/// <summary>
	/// Render voxel as full of fluid if it's teleporting to hide flickering.
	/// </summary>
	public float RenderFluid
	{
		get
		{
			return /*teleporting ? 1 :*/ (float)fluid / MaxVolume;
		}
	}

	public override string ToString()
	{
		return string.Format("{0}/{1}", fluid.ToString(), solid.ToString());
	}
}
