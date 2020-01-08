using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class VectorI3Comparer : IEqualityComparer<VectorI3>
{
	public int GetHashCode(VectorI3 a)
	{
		return a.GetHashCode();
	}

	public bool Equals(VectorI3 a, VectorI3 b)
	{
		return a == b;
	}
	public static VectorI3Comparer Instance = new VectorI3Comparer();
}

public class VectorI3Comparison : Comparer<VectorI3>
{
	public override int Compare(VectorI3 x, VectorI3 y)
	{
		return x.x.CompareTo(y.x) * 100 + x.y.CompareTo(y.y) * 10 + x.z.CompareTo(y.z);
	}

	public static int CompareStatic(VectorI3 x, VectorI3 y)
	{
		return x.x.CompareTo(y.x) * 100 + x.y.CompareTo(y.y) * 10 + x.z.CompareTo(y.z);
	}

	public static readonly VectorI3Comparison Instance = new VectorI3Comparison();
}


[System.Serializable]
public struct VectorI4 : IEquatable<VectorI4>
{
	public int x, y, z, w;
	public int this[int index]
	{
		get
		{
			if (index == 0) { return x; }
			if (index == 1) { return y; }
			if (index == 2) { return z; }
			if (index == 3) { return w; }

			return 0;
		}
		set
		{
			if (index == 0) { x = value; }
			if (index == 1) { y = value; }
			if (index == 2) { z = value; }
			if (index == 3) { w = value; }
		}
	}

	public bool Equals(VectorI4 i)
	{
		return x == i.x && y == i.y && z == i.z && w == i.w;
	}

	public VectorI4(int x, int y, int z, int w)
	{
		this.x = x;
		this.y = y;
		this.z = z;
		this.w = w;
	}
	public VectorI4(float x, float y, float z, float w)
	{
		this.x = (int)x;
		this.y = (int)y;
		this.z = (int)z;
		this.w = (int)w;
	}
	public VectorI4(VectorI4 vector)
	{
		this.x = vector.x;
		this.y = vector.y;
		this.z = vector.z;
		this.w = vector.w;
	}
	public VectorI4(Vector4 vector)
	{
		this.x = (int)vector.x;
		this.y = (int)vector.y;
		this.z = (int)vector.z;
		this.w = (int)vector.w;
	}
	public VectorI4(VectorI3 vector)
	{
		this.x = vector.x;
		this.y = vector.y;
		this.z = vector.z;
		w = 0;
	}
}

[System.Serializable]
public struct VectorI3 : IEquatable<VectorI3>
{
	#region Constants
	public static readonly VectorI3 one = new VectorI3(1, 1, 1);
	public static readonly VectorI3 negativeOne = new VectorI3(-1, -1, -1);
	public static readonly VectorI3 zero = new VectorI3(0, 0, 0);
	public static readonly VectorI3 left = new VectorI3(-1, 0, 0);
	public static readonly VectorI3 right = new VectorI3(1, 0, 0);
	public static readonly VectorI3 up = new VectorI3(0, 1, 0);
	public static readonly VectorI3 top = new VectorI3(0, 1, 0);
	public static readonly VectorI3 down = new VectorI3(0, -1, 0);
	public static readonly VectorI3 bottom = new VectorI3(0, -1, 0);
	public static readonly VectorI3 forward = new VectorI3(0, 0, 1);
	public static readonly VectorI3 back = new VectorI3(0, 0, -1);

	public int x, y, z;
	public int this[int index]
	{
		get
		{
			if (index == 0) { return x; }
			if (index == 1) { return y; }
			if (index == 2) { return z; }

			return 0;
		}
		set
		{
			if (index == 0) { x = value; }
			if (index == 1) { y = value; }
			if (index == 2) { z = value; }
		}
	}

	public VectorI3(int x, int y, int z)
	{
		this.x = x;
		this.y = y;
		this.z = z;
	}

	public VectorI3(int value)
	{ 
		x = y = z = value;
	}

	public VectorI3(float x, float y, float z)
	{
		this.x = (int)x;
		this.y = (int)y;
		this.z = (int)z;
	}
	public VectorI3(VectorI3 vector)
	{
		this.x = vector.x;
		this.y = vector.y;
		this.z = vector.z;
	}
	public VectorI3(Vector3 vector)
	{
		this.x = (int)vector.x;
		this.y = (int)vector.y;
		this.z = (int)vector.z;
	}
	public VectorI3(VectorI2 vector)
	{
		this.x = vector.x;
		this.y = vector.y;
		z = 0;
	}

	public Vector3 ToVector3()
	{ 
		return new Vector3(x, y, z);
	}

	#region VectorI3 / Vector3 Methods

	public int GetDistanceSquared(VectorI3 b)
	{
		int dx = x - b.x;
		int dy = y - b.y;
		int dz = z - b.z;
		return (dx * dx) + (dy * dy) + (dz * dz);
	}

	public float GetPathDistance(VectorI3 b)
	{
		int dx = x - b.x;
		int dy = y - b.y;
		int dz = z - b.z;
		return Mathf.Abs(dx) + Mathf.Abs(dy) + Mathf.Abs(dz);
	}

	public float GetDistance(VectorI3 b)
	{
		int dx = x - b.x;
		int dy = y - b.y;
		int dz = z - b.z;
		return Mathf.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
	}

	public static VectorI3 GridCeil(Vector3 v, Vector3 roundBy)
	{
		return Round(new Vector3(
				Mathf.Ceil(v.x / roundBy.x) * roundBy.x,
				Mathf.Ceil(v.y / roundBy.y) * roundBy.y,
				Mathf.Ceil(v.z / roundBy.z) * roundBy.z
				));
	}
	public static VectorI3 GridFloor(Vector3 v, Vector3 roundBy)
	{
		return Round(new Vector3(
				Mathf.Floor(v.x / roundBy.x) * roundBy.x,
				Mathf.Floor(v.y / roundBy.y) * roundBy.y,
				Mathf.Floor(v.z / roundBy.z) * roundBy.z
				));
	}
	public static VectorI3 GridRound(Vector3 v, Vector3 roundBy)
	{
		return Round(new Vector3(
				Mathf.Round(v.x / roundBy.x) * roundBy.x,
				Mathf.Round(v.y / roundBy.y) * roundBy.y,
				Mathf.Round(v.z / roundBy.z) * roundBy.z
				));
	}

	public static VectorI3 Ceil(Vector3 v)
	{
		return new VectorI3(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y), Mathf.CeilToInt(v.z));
	}
	public static VectorI3 Floor(Vector3 v)
	{
		return new VectorI3(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));
	}
	public static VectorI3 Round(Vector3 v)
	{
		//if(v.x < 0) v.x--;
		//if(v.y < 0) v.y--;
		//if(v.z < 0) v.z--;

		return new VectorI3(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));

		//return new VectorI3(v.x, v.y, v.z);
	}

	public int size
	{
		get { return Size(this); }
	}
	public static int Size(VectorI3 v)
	{
		return v.x * v.y * v.z;
	}

	public static VectorI3 Wrap3DIndex(VectorI3 positionIndex, VectorI3 direction, VectorI3 arraySize)
	{
		VectorI3 newDirection = new VectorI3(
				((positionIndex.x + direction.x) % (arraySize.x)),
				((positionIndex.y + direction.y) % (arraySize.y)),
				((positionIndex.z + direction.z) % (arraySize.z))
				);

		if (newDirection.x < 0) { newDirection.x = arraySize.x + newDirection.x; }
		if (newDirection.y < 0) { newDirection.y = arraySize.y + newDirection.y; }
		if (newDirection.z < 0) { newDirection.z = arraySize.z + newDirection.z; }

		return newDirection;
	}

	public static bool AnyGreater(VectorI3 a, VectorI3 b)
	{
		return a.x > b.x || a.y > b.y || a.z > b.z;
	}
	public static bool AllGreater(VectorI3 a, VectorI3 b)
	{
		return a.x > b.x && a.y > b.y && a.z > b.z;
	}
	public static bool AnyLower(VectorI3 a, VectorI3 b)
	{
		return a.x < b.x || a.y < b.y || a.z < b.z;
	}
	public static bool AllLower(VectorI3 a, VectorI3 b)
	{
		return a.x < b.x && a.y < b.y && a.z < b.z;
	}
	public static bool AnyGreaterAllEqual(VectorI3 a, VectorI3 b)
	{
		return a == b || AnyGreater(a, b);
	}
	public static bool AllGreaterEqual(VectorI3 a, VectorI3 b)
	{
		return a == b || AllGreater(a, b);
	}
	public static bool AnyLowerEqual(VectorI3 a, VectorI3 b)
	{
		return a == b || AnyLower(a, b);
	}
	public static bool AllLowerEqual(VectorI3 a, VectorI3 b)
	{
		return a == b || AllLower(a, b);
	}

	#endregion

	#region Advanced
	public static VectorI3 operator -(VectorI3 a)
	{
		return new VectorI3(-a.x, -a.y, -a.z);
	}
	public static VectorI3 operator -(VectorI3 a, VectorI3 b)
	{
		return new VectorI3(a.x - b.x, a.y - b.y, a.z - b.z);
	}
	//public static Vector3 operator -(Vector3 a, VectorI3 b)
	//{
	//	return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
	//}
	//public static Vector3 operator -(VectorI3 a, Vector3 b)
	//{
	//	return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
	//}

	public static VectorI3 operator %(VectorI3 a, VectorI3 b)
	{
		return new VectorI3(a.x % b.x, a.y % b.y, a.z % b.z);
	}

	public static bool operator !=(VectorI3 lhs, VectorI3 rhs)
	{
		return !(lhs == rhs);
	}
	//public static bool operator !=(Vector3 lhs, VectorI3 rhs)
	//{
	//	return lhs.x != rhs.x || lhs.y != rhs.y || lhs.z != rhs.z;
	//}
	//public static bool operator !=(VectorI3 lhs, Vector3 rhs)
	//{
	//	return lhs.x != rhs.x || lhs.y != rhs.y || lhs.z != rhs.z;
	//}

	public static Vector3 operator *(float d, VectorI3 a)
	{
		return new Vector3(d * a.x, d * a.y, d * a.z);
	}

	public static Vector3 operator *(VectorI3 a, float d)
	{
		return new Vector3(a.x * d, a.y * d, a.z * d);
	}

	public static VectorI3 operator *(int d, VectorI3 a)
	{
		return new VectorI3(d * a.x, d * a.y, d * a.z);
	}

	public static VectorI3 operator *(VectorI3 a, int d)
	{
		return new VectorI3(a.x * d, a.y * d, a.z * d);
	}

	public static Vector3 operator /(VectorI3 a, float d)
	{
		return new Vector3(a.x / d, a.y / d, a.z / d);
	}

	public static float operator /(float d, VectorI3 a)
	{
		d /= a.x; d /= a.y; d /= a.z;
		return d;
	}

	public static VectorI3 operator *(VectorI3 a, VectorI3 b)
	{
		return new VectorI3(a.x * b.x, a.y * b.y, a.z * b.z);
	}

	public static Vector3 operator *(Vector3 a, VectorI3 b)
	{
		return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
	}

	public static Vector3 operator *(VectorI3 a, Vector3 b)
	{
		return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
	}

	public static VectorI3 operator /(VectorI3 a, VectorI3 b)
	{
		return new VectorI3(a.x / b.x, a.y / b.y, a.z / b.z);
	}

	public static Vector3 operator /(Vector3 a, VectorI3 b)
	{
		return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
	}

	public static Vector3 operator /(VectorI3 a, Vector3 b)
	{
		return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
	}

	public static VectorI3 operator +(VectorI3 a, VectorI3 b)
	{
		return new VectorI3(a.x + b.x, a.y + b.y, a.z + b.z);
	}

	public static Vector3 operator +(Vector3 a, VectorI3 b)
	{
		return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
	}

	public static Vector3 operator +(VectorI3 a, Vector3 b)
	{
		return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
	}

	public static bool operator ==(VectorI3 lhs, VectorI3 rhs)
	{
		return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
	}
	//public static bool operator ==(Vector3 lhs, VectorI3 rhs)
	//{
	//	return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
	//}
	//public static bool operator ==(VectorI3 lhs, Vector3 rhs)
	//{
	//	return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z;
	//}

	public override bool Equals(object obj)
	{
		return obj is VectorI3 && this.Equals((VectorI3)obj);
	}

	public bool Equals(VectorI3 v)
	{
		return x == v.x && y == v.y && z == v.z;
	}

	public override string ToString()
	{
		return "(" + x + ", " + y + ", " + z + ")";
	}

	#endregion

	public VectorI3 Abs()
	{
		return new VectorI3(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z));
	}

	public override int GetHashCode()
	{
		return (x ^ y ^ z) + 1 + (x + y + z);
	}

	public static VectorI3 Parse(string _s)
	{
		string[] parts = _s.Split(',');
		if (parts.Length != 3)
		{
			return VectorI3.zero;
		}
		return new VectorI3(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
	}
}

[System.Serializable]
public struct VectorI2 : IEquatable<VectorI2>
{
	#region Constants
	public static readonly VectorI2 one = new VectorI2(1, 1);
	public static readonly VectorI2 negativeOne = new VectorI2(-1, -1);
	public static readonly VectorI2 zero = new VectorI2(0, 0);
	public static readonly VectorI2 left = new VectorI2(-1, 0);
	public static readonly VectorI2 right = new VectorI2(1, 0);
	public static readonly VectorI2 up = new VectorI2(0, 1);
	public static readonly VectorI2 top = new VectorI2(0, 1);
	public static readonly VectorI2 down = new VectorI2(0, -1);
	public static readonly VectorI2 bottom = new VectorI2(0, -1);
	#endregion

	public int x, y;
	public int this[int index]
	{
		get
		{
			if (index == 0) { return x; }
			if (index == 1) { return y; }

			return 0;
		}
		set
		{
			if (index == 0) { x = value; }
			if (index == 1) { y = value; }
		}
	}

	public VectorI2(int x, int y)
	{
		this.x = x;
		this.y = y;
	}
	public VectorI2(float x, float y)
	{
		this.x = (int)x;
		this.y = (int)y;
	}

	public VectorI2(Vector2 vector)
	{
		this.x = (int)vector.x;
		this.y = (int)vector.y;
	}

	public override int GetHashCode()
	{
		return x ^ y;
	}

	#region VectorI2 / Vector3 Methods

	public static VectorI2 GridCeil(Vector2 v, Vector2 roundBy)
	{
		return Round(new Vector2(
				Mathf.Ceil(v.x / roundBy.x) * roundBy.x,
				Mathf.Ceil(v.y / roundBy.y) * roundBy.y
				));
	}
	public static VectorI2 GridFloor(Vector2 v, Vector2 roundBy)
	{
		return Round(new Vector2(
				Mathf.Floor(v.x / roundBy.x) * roundBy.x,
				Mathf.Floor(v.y / roundBy.y) * roundBy.y
				));
	}
	public static VectorI2 GridRound(Vector2 v, Vector2 roundBy)
	{
		return Round(new Vector2(
				Mathf.Round(v.x / roundBy.x) * roundBy.x,
				Mathf.Round(v.y / roundBy.y) * roundBy.y
				));
	}

	public static VectorI2 Ceil(Vector2 v)
	{
		return new VectorI2(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));
	}
	public static VectorI2 Floor(Vector2 v)
	{
		return new VectorI2(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
	}
	public static VectorI2 Round(Vector2 v)
	{
		return new VectorI2(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));
	}

	public int size
	{
		get { return Size(this); }
	}
	public static int Size(VectorI2 v)
	{
		return v.x * v.y;
	}

	public static VectorI2 Wrap2DIndex(VectorI2 positionIndex, VectorI2 direction, VectorI2 arraySize)
	{
		VectorI2 newDirection = new VectorI2(
				((positionIndex.x + direction.x) % (arraySize.x)),
				((positionIndex.y + direction.y) % (arraySize.y))
				);

		if (newDirection.x < 0) { newDirection.x = arraySize.x + newDirection.x; }
		if (newDirection.y < 0) { newDirection.y = arraySize.y + newDirection.y; }

		return newDirection;
	}

	public static bool AnyGreater(VectorI2 a, VectorI2 b)
	{
		return a.x > b.x || a.y > b.y;
	}
	public static bool AllGreater(VectorI2 a, VectorI2 b)
	{
		return a.x > b.x && a.y > b.y;
	}
	public static bool AnyLower(VectorI2 a, VectorI2 b)
	{
		return a.x < b.x || a.y < b.y;
	}
	public static bool AllLower(VectorI2 a, VectorI2 b)
	{
		return a.x < b.x && a.y < b.y;
	}
	public static bool AnyGreaterAllEqual(VectorI2 a, VectorI2 b)
	{
		return a == b || AnyGreater(a, b);
	}
	public static bool AllGreaterEqual(VectorI2 a, VectorI2 b)
	{
		return a == b || AllGreater(a, b);
	}
	public static bool AnyLowerEqual(VectorI2 a, VectorI2 b)
	{
		return a == b || AnyLower(a, b);
	}
	public static bool AllLowerEqual(VectorI2 a, VectorI2 b)
	{
		return a == b || AllLower(a, b);
	}
	#endregion

	#region Advanced
	public static VectorI2 operator -(VectorI2 a)
	{
		return new VectorI2(-a.x, -a.y);
	}
	public static VectorI2 operator -(VectorI2 a, VectorI2 b)
	{
		return new VectorI2(a.x - b.x, a.y - b.y);
	}
	public static Vector2 operator -(Vector3 a, VectorI2 b)
	{
		return new Vector2(a.x - b.x, a.y - b.y);
	}
	public static Vector2 operator -(VectorI2 a, Vector2 b)
	{
		return new Vector2(a.x - b.x, a.y - b.y);
	}

	public static bool operator !=(VectorI2 lhs, VectorI2 rhs)
	{
		return !(lhs == rhs);
	}
	public static bool operator !=(Vector2 lhs, VectorI2 rhs)
	{
		return !(lhs == rhs);
	}
	public static bool operator !=(VectorI2 lhs, Vector2 rhs)
	{
		return !(lhs == rhs);
	}

	public static Vector2 operator *(float d, VectorI2 a)
	{
		return new Vector2(d * a.x, d * a.y);
	}
	public static Vector2 operator *(VectorI2 a, float d)
	{
		return new Vector2(a.x * d, a.y * d);
	}
	public static Vector2 operator /(VectorI2 a, float d)
	{
		return new Vector2(a.x / d, a.y / d);
	}
	public static float operator /(float d, VectorI2 a)
	{
		d /= a.x; d /= a.y;
		return d;
	}

	public static VectorI2 operator *(VectorI2 a, int d)
	{
		return new VectorI2(a.x * d, a.y * d);
	}

	public static VectorI2 operator *(int d, VectorI2 a)
	{
		return new VectorI2(a.x * d, a.y * d);
	}


	public static VectorI2 operator *(VectorI2 a, VectorI2 b)
	{
		return new VectorI2(a.x * b.x, a.y * b.y);
	}
	public static Vector2 operator *(Vector2 a, VectorI2 b)
	{
		return new Vector2(a.x * b.x, a.y * b.y);
	}
	public static Vector2 operator *(VectorI2 a, Vector2 b)
	{
		return new Vector2(a.x * b.x, a.y * b.y);
	}

	public static VectorI2 operator /(VectorI2 a, VectorI2 b)
	{
		return new VectorI2(a.x / b.x, a.y / b.y);
	}
	public static Vector2 operator /(Vector2 a, VectorI2 b)
	{
		return new Vector2(a.x / b.x, a.y / b.y);
	}
	public static Vector2 operator /(VectorI2 a, Vector2 b)
	{
		return new Vector2(a.x / b.x, a.y / b.y);
	}

	public static VectorI2 operator +(VectorI2 a, VectorI2 b)
	{
		return new VectorI2(a.x + b.x, a.y + b.y);
	}
	public static Vector2 operator +(Vector2 a, VectorI2 b)
	{
		return new Vector2(a.x + b.x, a.y + b.y);
	}
	public static Vector2 operator +(VectorI2 a, Vector2 b)
	{
		return new Vector2(a.x + b.x, a.y + b.y);
	}

	public static Vector2 operator +(VectorI2 a, float d)
	{
		return new Vector2(a.x + d, a.y + d);
	}

	public static bool operator ==(VectorI2 lhs, VectorI2 rhs)
	{
		return lhs.x == rhs.x && lhs.y == rhs.y;
	}
	public static bool operator ==(Vector2 lhs, VectorI2 rhs)
	{
		return lhs.x == rhs.x && lhs.y == rhs.y;
	}
	public static bool operator ==(VectorI2 lhs, Vector2 rhs)
	{
		return lhs.x == rhs.x && lhs.y == rhs.y;
	}

	public bool Equals(VectorI2 v)
	{
		return x == v.x && y == v.y;
	}

	public override bool Equals(object obj)
	{
		return obj is VectorI2 && this.Equals((VectorI2)obj);
	}

	public override string ToString()
	{
		return "(" + x + ", " + y + ")";
	}

	#endregion

	#endregion
}