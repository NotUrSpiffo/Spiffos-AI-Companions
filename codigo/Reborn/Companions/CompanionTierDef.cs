namespace Companions;

public class CompanionTierDef
{
	public string PrefabName;

	public string DisplayName;

	public string NameKey;

	public string SourcePrefab;

	public float Health;

	public float WalkSpeed;

	public float RunSpeed;

	public bool CanWearArmor;

	public bool GiveStarterGear;

	public string LocalName
	{
		get
		{
			if (!string.IsNullOrEmpty(NameKey))
			{
				return ModLocalization.Loc(NameKey);
			}
			return DisplayName;
		}
	}
}
