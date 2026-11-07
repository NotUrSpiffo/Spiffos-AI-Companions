using System.Globalization;
using UnityEngine;

namespace Companions;

public class CompanionAppearance
{
	public int ModelIndex;

	public string HairItem;

	public string BeardItem;

	public Vector3 SkinColor;

	public Vector3 HairColor;

	public static CompanionAppearance Deserialize(string data)
	{
		if (string.IsNullOrEmpty(data))
		{
			return Default();
		}
		try
		{
			string[] array = data.Split(new char[1] { ';' });
			if (array.Length < 9)
			{
				return Default();
			}
			CultureInfo invariantCulture = CultureInfo.InvariantCulture;
			return new CompanionAppearance
			{
				ModelIndex = int.Parse(array[0], invariantCulture),
				HairItem = array[1],
				BeardItem = array[2],
				SkinColor = new Vector3(float.Parse(array[3], invariantCulture), float.Parse(array[4], invariantCulture), float.Parse(array[5], invariantCulture)),
				HairColor = new Vector3(float.Parse(array[6], invariantCulture), float.Parse(array[7], invariantCulture), float.Parse(array[8], invariantCulture))
			};
		}
		catch
		{
			return Default();
		}
	}

	public string Serialize()
	{
		CultureInfo invariantCulture = CultureInfo.InvariantCulture;
		return string.Join(";", ModelIndex.ToString(invariantCulture), HairItem ?? "", BeardItem ?? "", SkinColor.x.ToString("F3", invariantCulture), SkinColor.y.ToString("F3", invariantCulture), SkinColor.z.ToString("F3", invariantCulture), HairColor.x.ToString("F3", invariantCulture), HairColor.y.ToString("F3", invariantCulture), HairColor.z.ToString("F3", invariantCulture));
	}

	public static CompanionAppearance Default()
	{
		return new CompanionAppearance
		{
			ModelIndex = 0,
			HairItem = "Hair1",
			BeardItem = "Beard1",
			SkinColor = Utils.ColorToVec3(new Color(0.95f, 0.82f, 0.68f)),
			HairColor = Utils.ColorToVec3(new Color(0.55f, 0.38f, 0.18f))
		};
	}
}
