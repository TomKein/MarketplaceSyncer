using System;
using System.Collections.Generic;

namespace BusinessRu.ApiClient;

public sealed class PhpKSort : IComparer<string>
{
	private const int OFFSET = 1000000;

	private static int Value(char c)
	{
		if ('0' <= c && c <= '9')
		{
			return 3000000 + c;
		}
		if ('A' <= c && c <= 'Z')
		{
			return 2000000 + c;
		}
		if ('a' <= c && c <= 'z')
		{
			return 1000000 + c;
		}
		switch (c)
		{
		case '_':
			return 1000000 + c + 1000;
		default:
			if (c != ']')
			{
				throw new ArgumentException($"{c} does not have an associated value");
			}
			goto case '[';
		case '[':
			return 1000000 + c + 2000;
		}
	}

	public int Compare(string? x, string? y)
	{
		if (x == y)
		{
			return 0;
		}
		if (x == null)
		{
			return -1;
		}
		if (y == null)
		{
			return 1;
		}
		for (int i = 0; i < x.Length || i < y.Length; i++)
		{
			if (i >= x.Length)
			{
				return -1;
			}
			if (i >= y.Length)
			{
				return 1;
			}
			int num = Value(x[i]);
			int num2 = Value(y[i]);
			if (num < num2)
			{
				return -1;
			}
			if (num > num2)
			{
				return 1;
			}
		}
		return 0;
	}
}
