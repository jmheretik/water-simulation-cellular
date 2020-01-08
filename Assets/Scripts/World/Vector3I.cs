using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// Representation of a 3D point using integers.
/// </summary>
public struct Vector3I : IEquatable<Vector3I>
{
	public int x;
	public int y;
	public int z;

	public Vector3I(int x, int y, int z)
	{
		this.x = x;
		this.y = y;
		this.z = z;
	}

	public bool valid
	{
		get
		{
			return x >= 0;
		}
	}

	public override string ToString()
	{
		return string.Format("({0}, {1}, {2})", x, y, z);
	}

	#region equals

	public bool Equals(Vector3I other)
	{
		return (x == other.x) && (y == other.y) && (z == other.z);
	}

	public override bool Equals(object obj)
	{
		if (obj == null || !GetType().Equals(obj.GetType()))
		{
			return false;
		}
		else
		{
			return Equals((Vector3I)obj);
		}
	}

	public override int GetHashCode()
	{
		return (x ^ y ^ z) + 1 + (x + y + z);
	}

	#endregion
}

