using System.Collections.Immutable;

namespace PatternCompress.Blocks;

public static class Protocol
{
	public static class OpCodes // not an enum for ease of use --> I know, it sounds counterintuitive
	{
		public const byte DeltaIter = 0; // !deltaiter {startOffset%u8} {count%u32} {delta%u8}
	}

	public enum SectionType : byte
	{
		Data = 0,
		Code = 1,
	}

	public const int HeaderSize = 1+sizeof(ushort);
	

	public static ImmutableArray<byte> GetHeader(byte sectionType, ushort sectionLength)
	{
		return [sectionType, ..BitConverter.GetBytes(sectionLength)];
	}

	public static bool TryMakeSection(SectionType sectionType, ImmutableArray<byte> sectionData, out ImmutableArray<byte> section)
	{
		if (sectionData.Length > ushort.MaxValue)
		{
			section = default;
			return false;
		}

		section = [..GetHeader((byte) sectionType, (ushort) sectionData.Length), ..sectionData];
		return true;
	}

	public static ImmutableArray<byte> MakeSection(SectionType sectionType, ImmutableArray<byte> sectionData)
	{
		if (TryMakeSection(sectionType, sectionData, out var section))
			return section;

		throw new ArgumentException("Section data is too long.");
	}

	public static SectionType GetSectionType(ImmutableArray<byte> sectionHeader)
	{
		return (SectionType) sectionHeader[0];
	}

	public static ushort GetSectionLength(ImmutableArray<byte> sectionHeader)
	{
		return BitConverter.ToUInt16([..sectionHeader], 1);
	}

	public static ImmutableArray<byte> GetSectionData(ImmutableArray<byte> sectionDataAndMore)
	{
		int length = GetSectionLength(sectionDataAndMore);

		return sectionDataAndMore[HeaderSize..(HeaderSize+length)];
	}

	public static SectionType GetSectionType(ImmutableArray<byte> buf, int startIdx)
	{
		return (SectionType) buf[startIdx];
	}

	public static ushort GetSectionLength(ImmutableArray<byte> buf, int startIdx)
	{
		return BitConverter.ToUInt16([..buf], startIdx+1);
	}

	public static ImmutableArray<byte> GetSectionData(ImmutableArray<byte> buf, int startIdx)
	{
		int length = GetSectionLength(buf, startIdx);

		return buf[(startIdx+HeaderSize)..(startIdx+HeaderSize+length)];		
	}

	/// <summary>
	/// Returns the bytes representing one or two sections that were generated from sectionOne and sectionTwo.
	/// </summary>
	/// <param name="sectionOne"></param>
	/// <param name="sectionTwo"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentException"></exception>
	public static (ImmutableArray<byte> Merged, ImmutableArray<byte>? Overflow) MergeSectionsAsMuchAsPossible(ImmutableArray<byte> sectionOne, ImmutableArray<byte> sectionTwo)
	{
		var type = GetSectionType(sectionOne);

		if (type != GetSectionType(sectionTwo))
			throw new ArgumentException("Cannot merge sections of different types.");

		int combinedLength = GetSectionLength(sectionOne) + GetSectionLength(sectionTwo);

		if (combinedLength > ushort.MaxValue)
		{
			var sectionOneData = GetSectionData(sectionOne);
			var sectionTwoData = GetSectionData(sectionTwo);

			var firstPart = MakeSection(type, [..sectionOneData, ..sectionTwoData[..(ushort.MaxValue-sectionOneData.Length)]]);

			var secondPart = MakeSection(type, [..sectionTwoData[(ushort.MaxValue-sectionOneData.Length)..]]);

			return (firstPart, secondPart);
		}

		return (MakeSection(type, [..sectionOne[HeaderSize..], ..sectionTwo[HeaderSize..]]), null);
	}
}