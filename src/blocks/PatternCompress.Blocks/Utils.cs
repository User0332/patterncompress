namespace PatternCompress.Blocks;

public static class Utils
{
	public static IEnumerable<byte> ToMinimumBytes(this IEnumerable<int> numbers)
	{
		foreach (var num in numbers)
		{
			var value = num;
		
			while (value > 255)
			{
				yield return (byte) (value & 0xFF);
				value >>= 8;
			}

			yield return (byte) value;
		}
	}
}