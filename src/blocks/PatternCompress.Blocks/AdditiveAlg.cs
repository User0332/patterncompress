using System.Collections.Immutable;
using System.IO.Compression;

namespace PatternCompress.Blocks;

static class AdditiveAlg
{
	static readonly ImmutableArray<byte> data = [..Enumerable.Range(0, 1000).ToMinimumBytes()];
	const int GenerateCodeIfMoreBytesThanThis = 10;

	public static void Run()
	{
		ImmutableArray<byte> data = [..File.ReadAllBytes("bin/Debug/net8.0/PatternCompress.Blocks.dll")];

		Console.WriteLine($"Data Size: {data.Length/1000d} kb");

		ImmutableArray<byte> compressed = Compress(data);
		ImmutableArray<byte> gzipped = CompressionBenchmark(data);

		Console.WriteLine($"PatternCompressed Size: {compressed.Length/1000d} kb");
		Console.WriteLine($"gzipped Size: {gzipped.Length/1000d} kb");

		double ratio = compressed.Length / (double) gzipped.Length;

		Console.WriteLine($"[!]    PatternCompress is {ratio} times the size of gzip");

		ImmutableArray<byte> decompressed = Decompress(compressed);

		Console.WriteLine($"Decompressed Size: {decompressed.Length/1000d} kb");

		if (decompressed.Length != data.Length)
		{
			Console.WriteLine($"[!]    Decompressed size does not match original size: {decompressed.Length} != {data.Length}");
		}

		for (int i = 0; i < decompressed.Length; i++)
		{
			if (decompressed[i] != data[i])
			{
				Console.WriteLine($"[!]    Decompressed data does not match original data at index {i}: {decompressed[i]} != {data[i]}");
			}
		}

		Console.WriteLine(decompressed.SequenceEqual(data));
	}

	static ImmutableArray<byte> Compress(ImmutableArray<byte> data)
	{
		if (data.Length < 2)
			return [..Protocol.MakeSection(Protocol.SectionType.Data, data)];

		List<ImmutableArray<byte>> sections = [];
		List<byte> compressed = [];

		for (int i = 1; i < data.Length; i++)
		{
			// try searching for simple additive pattern
			byte searchDelta = (byte) (data[i] - data[i-1]);

			int j;

			for (j = i+1; j < data.Length; j++)
			{
				byte thisDelta = (byte) (data[j] - data[j-1]);

				if (thisDelta != searchDelta)
				{
					break;
				}
			}

			int numberOfCompressableBytes = j-i;

			if (numberOfCompressableBytes > GenerateCodeIfMoreBytesThanThis)
			{
				int start = i-1;

				ImmutableArray<byte> addIterCode = [Protocol.OpCodes.DeltaIter, data[start],  ..BitConverter.GetBytes((ushort) numberOfCompressableBytes), searchDelta];

				sections.Add(Protocol.MakeSection(Protocol.SectionType.Code, addIterCode));
				if (j != data.Length) sections.Add(Protocol.MakeSection(Protocol.SectionType.Data, [data[j]]));
			}
			else
			{
				sections.Add(Protocol.MakeSection(Protocol.SectionType.Data, data[(i-1)..j]));
			}

			i+=numberOfCompressableBytes;
		}

		for (int i = 1; i < sections.Count; i++)
		{
			if (Protocol.GetSectionType(sections[i]) == Protocol.GetSectionType(sections[i-1]))
			{
				var (Merged, Overflow) = Protocol.MergeSectionsAsMuchAsPossible(sections[i-1], sections[i]);

				sections[i-1] = Merged;

				if (Overflow != null)
				{
					sections[i] = Overflow.Value;
					continue;
				}

				sections.RemoveAt(i);
				i--;
			}
		}

		foreach (var section in sections)
		{
			Console.WriteLine($"Section: {section.Length} bytes, {Protocol.GetSectionType(section)}");
			compressed.AddRange(section);
		}

		return [..compressed];
	}

	static ImmutableArray<byte> CompressionBenchmark(ImmutableArray<byte> data)
	{
		using var compressedStream = new MemoryStream();
		using var zipStream = new GZipStream(compressedStream, CompressionMode.Compress);

		zipStream.Write([..data], 0, data.Length);
		zipStream.Close();
		return [..compressedStream.ToArray()];
	}

	static ImmutableArray<byte> Decompress(ImmutableArray<byte> compressed)
	{
		List<byte> decompressed = [];

		int i = 0;

		while (i < compressed.Length)
		{
			var sectionType = Protocol.GetSectionType(compressed, i);
			ushort sectionLength = Protocol.GetSectionLength(compressed, i);

			i+=Protocol.HeaderSize;

			Console.WriteLine($"Section: {Protocol.HeaderSize+sectionLength} bytes, {sectionType}");

			if (sectionType == Protocol.SectionType.Data)
			{
				decompressed.AddRange(compressed[i..(i+sectionLength)]);
				i += sectionLength;
			}
			else if (sectionType == Protocol.SectionType.Code)
			{
				int j = i;

				while (j < i+sectionLength)
				{
					byte opCode = compressed[j];

					j++;

					switch (opCode)
					{
						case Protocol.OpCodes.DeltaIter:
							byte startOffset = compressed[j++];
							ushort count = BitConverter.ToUInt16([..compressed], j);
							j += 2;

							byte delta = compressed[j++];

							for (int k = 0; k < count; k++)
							{unchecked {
								// Console.WriteLine($"DeltaIter: {startOffset} + {k} * {delta} = {startOffset + (k * delta)}");
								
								decompressed.Add((byte) (startOffset + (k * delta)));
							}}

							break;

						default:
							throw new Exception($"Unknown opcode: {opCode}");
					}
				}

				i+=sectionLength;
			}
			else
			{
				throw new Exception($"Unknown section type: {sectionType}");
			}
		}

		return [..decompressed];
	}
}