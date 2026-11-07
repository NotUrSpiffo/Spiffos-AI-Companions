using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Companions;

public static class MHR_ToolKeeper
{
	private class Entry
	{
		public int Kind;

		public string Prefab;

		public int Count;

		public Vector3 Chest;
	}

	private class State
	{
		public List<Entry> Borrowed = new List<Entry>();

		public bool Loaded;

		public int Phase;

		public Container Chest;

		public int Kind;

		public string Prefab;

		public Entry ReturnEntry;

		public float TripStart;

		public float NextDecide;

		public float NextSpeak;

		public float BestDist;

		public float LastProgress;

		public float NextTripLog;

		public Vector3 Goal;

		public bool NeedMove;

		public bool WasFighting;

		public bool HasSpot;

		public float NextSpotPick;

		public CraftingStation Station;

		public int WaitKind;

		public bool WaitHome;

		public float NextWaitSpeak;

		public float NextFollowFix;

		public int LastMode = -999;

		public bool UnloadPending;

		public bool UnloadActive;

		public int UnloadTrips;

		public bool TagInit;

		public float NextUnloadSpeak;

		public bool WasAtHome;

		public Dictionary<int, float> KindCooldown = new Dictionary<int, float>();

		public Dictionary<int, float> RepairFailUntil = new Dictionary<int, float>();

		public HashSet<int> Refused = new HashSet<int>();

		public bool PanelOpen;

		public HashSet<int> PanelHad;
	}

	private const int K_AXE = 1;

	private const int K_PICK = 2;

	private const int K_BOW = 3;

	private const int K_ARROW = 4;

	private const int K_CULT = 5;

	private const int K_ROD = 6;

	private const int K_BAIT = 7;

	private const int K_HAMMER = 8;

	private const int K_FOOD = 9;

	private const float SearchRadius = 30f;

	private const float UseDist = 2.5f;

	private const float TripTimeout = 150f;

	private const float NoProgressTimeout = 45f;

	private static readonly int BorrowHash = "HC_Borrowed".GetStableHashCode();

	private static readonly Dictionary<int, State> s_state = new Dictionary<int, State>();

	private const string OwnKey = "HC_Own";

	private static Component s_current;

	internal static string TripText(Component c)
	{
		if (c == null)
		{
			return null;
		}
		CompanionSetup component = c.GetComponent<CompanionSetup>();
		if (component == null || !s_state.TryGetValue(component.GetInstanceID(), out var value))
		{
			return null;
		}
		string text = KindName(value.Kind);
		switch (value.Phase)
		{
		case 1:
			return ModLocalization.LocFmt("hc_status_fetching", text);
		case 2:
			return ModLocalization.LocFmt("hc_status_putting_back", text);
		case 3:
			return ModLocalization.Loc("hc_status_unloading");
		case 4:
			return ModLocalization.Loc("hc_status_repairing_gear");
		default:
			if (value.WaitHome)
			{
				return ModLocalization.LocFmt("hc_status_needs_home", KindName(value.WaitKind));
			}
			return null;
		}
	}

	public static bool IsBusy(Component c)
	{
		if (c == null)
		{
			return false;
		}
		CompanionSetup component = c.GetComponent<CompanionSetup>();
		if (component == null)
		{
			return false;
		}
		if (s_state.TryGetValue(component.GetInstanceID(), out var value))
		{
			if (value.Phase == 0 && !value.UnloadActive)
			{
				return value.WaitHome;
			}
			return true;
		}
		return false;
	}

	public static void RequestUnload(CompanionSetup cs)
	{
		if (cs == null || !cs.HasHomePosition() || !AtHome(cs) || !s_state.TryGetValue(cs.GetInstanceID(), out var value) || value.UnloadPending || value.Phase != 0)
		{
			return;
		}
		Humanoid component = cs.GetComponent<Humanoid>();
		Inventory inventory = ((component != null) ? component.GetInventory() : null);
		ZNetView component2 = cs.GetComponent<ZNetView>();
		int mode = ((component2 != null && component2.GetZDO() != null) ? component2.GetZDO().GetInt(CompanionSetup.ActionModeHash) : 0);
		if (inventory != null && UnloadList(cs, value, inventory, mode).Count != 0)
		{
			value.UnloadPending = true;
			value.UnloadTrips = 0;
			value.NextDecide = 0f;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Settling down near home - emptying bags first");
			}
		}
	}

	public static bool Drive(CompanionAI ai, float dt)
	{
		if (ai == null)
		{
			return false;
		}
		if (MHR_Mover.EscapeFire(ai))
		{
			return true;
		}
		CompanionSetup component = ai.GetComponent<CompanionSetup>();
		if (component == null)
		{
			return false;
		}
		if (!s_state.TryGetValue(component.GetInstanceID(), out var value) || value.Phase == 0)
		{
			return false;
		}
		if (ai.GetFollowTarget() != null)
		{
			ai.SetFollowTarget(null);
		}
		if (value.NeedMove)
		{
			MHR_Mover.Go(ai, dt, value.Goal, 0.6f, allowRun: true, (value.Phase == 4) ? "Repair" : "Tools");
		}
		else
		{
			ai.StopMoving();
		}
		return true;
	}

	public static void Tick()
	{
		IReadOnlyList<CompanionSetup> allCompanions = CompanionSetup.AllCompanions;
		if (allCompanions == null || Player.m_localPlayer == null)
		{
			return;
		}
		float time = Time.time;
		float deltaTime = Time.deltaTime;
		for (int i = 0; i < allCompanions.Count; i++)
		{
			CompanionSetup companionSetup = allCompanions[i];
			if (companionSetup == null)
			{
				continue;
			}
			try
			{
				TickOne(companionSetup, time, deltaTime);
			}
			catch (Exception ex)
			{
				if (s_state.TryGetValue(companionSetup.GetInstanceID(), out var value))
				{
					value.Phase = 0;
				}
				if (CompanionsPlugin.Log != null)
				{
					CompanionsPlugin.Log.LogWarning("[Tools] " + ex.Message);
				}
			}
		}
	}

	private static void TickOne(CompanionSetup cs, float now, float dt)
	{
		ZNetView component = cs.GetComponent<ZNetView>();
		if (component == null || !component.IsValid() || !component.IsOwner())
		{
			return;
		}
		ZDO zDO = component.GetZDO();
		Humanoid component2 = cs.GetComponent<Humanoid>();
		CompanionAI component3 = cs.GetComponent<CompanionAI>();
		if (zDO == null || component2 == null || component3 == null || component2.IsDead())
		{
			return;
		}
		int instanceID = cs.GetInstanceID();
		s_current = cs;
		if (!s_state.TryGetValue(instanceID, out var value))
		{
			value = new State();
			s_state[instanceID] = value;
		}
		if (!value.Loaded)
		{
			Load(value, zDO);
			value.Loaded = true;
		}
		int num = zDO.GetInt(CompanionSetup.ActionModeHash);
		Inventory inventory = component2.GetInventory();
		if (inventory == null)
		{
			return;
		}
		MHR_Crossbow.Tick(component2);
		MHR_Mover.Tick(cs, component3);
		MHR_Follow.Tick(cs, component3);
		MHR_Recover.Tick(cs);
		if (!value.TagInit || CompanionInteractPanel.IsOpenFor(cs))
		{
			value.TagInit = true;
			TagOwn(inventory);
		}
		if (value.LastMode == -999)
		{
			value.LastMode = num;
		}
		if (num != value.LastMode)
		{
			value.UnloadPending = true;
			value.UnloadTrips = 0;
			value.Refused.Clear();
			CompanionStatus.Clear(cs);
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Job changed (" + value.LastMode + " -> " + num + ") - will empty bags at home");
			}
			if (value.Phase == 1 && !NeedsKind(num, value.Kind))
			{
				if (CompanionsPlugin.LogNormal)
				{
					Log("Not fetching " + KindName(value.Kind) + " any more - the job changed");
				}
				EndTrip(cs, value, component3, num);
			}
		}
		value.LastMode = num;
		bool flag = CompanionInteractPanel.IsOpenFor(cs);
		if (flag && !value.PanelOpen)
		{
			value.PanelHad = OwnedKinds(inventory, num);
		}
		else if (!flag && value.PanelOpen && value.PanelHad != null)
		{
			HashSet<int> hashSet = OwnedKinds(inventory, num);
			foreach (int item in value.PanelHad)
			{
				if (!hashSet.Contains(item) && value.Refused.Add(item))
				{
					string text = KindName(item);
					if (text.StartsWith("an "))
					{
						text = text.Substring(3);
					}
					else
					{
						text = (text.StartsWith("a ") ? text.Substring(2) : text);
					}
					if (CompanionsPlugin.LogNormal)
					{
						Log("You took its " + text + " - it won't fetch another for this job");
					}
					cs.GetComponent<CompanionTalk>()?.Speak(ModLocalization.LocFmt("hc_why_tool_taken", text), CompanionTalk.Priority.Event, "taken:" + item);
					if (value.Phase == 1 && value.Kind == item)
					{
						EndTrip(cs, value, component3, num);
					}
				}
			}
			value.PanelHad = null;
		}
		value.PanelOpen = flag;
		bool flag2 = cs.HasHomePosition() && AtHome(cs);
		if (flag2 && !value.WasAtHome && (num == 0 || (MHR_JobRules.KeepsFollow(num) && (num == 7 || cs.GetFollow()))) && !value.UnloadPending && value.Phase == 0 && UnloadList(cs, value, inventory, num).Count > 0)
		{
			value.UnloadPending = true;
			value.UnloadTrips = 0;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Back home - will empty bags");
			}
		}
		value.WasAtHome = flag2;
		if (value.Phase != 0)
		{
			UpdateTrip(cs, value, component3, component2, inventory, zDO, num, now, dt);
		}
		else
		{
			if (now < value.NextDecide || MHR_JobRules.OtherOrder(cs))
			{
				return;
			}
			value.NextDecide = now + 1f;
			CompanionRest component4 = cs.GetComponent<CompanionRest>();
			if ((component4 != null && (component4.IsSleeping || component4.IsNavigating)) || component3.IsInCombat)
			{
				return;
			}
			bool flag3 = NearHome(cs);
			if (value.WaitHome)
			{
				if (flag3)
				{
					value.WaitHome = false;
					if (CompanionsPlugin.LogDetailed)
					{
						LogDebug("Back near home - sorting out the missing tool");
					}
					cs.ApplyFollowMode(num, force: true);
				}
				else
				{
					if (now >= value.NextFollowFix && MHR_Owner.Of(cs) != null && component3.GetFollowTarget() != MHR_Owner.Go(cs))
					{
						value.NextFollowFix = now + 2f;
						component3.SetFollowTarget(MHR_Owner.Go(cs));
					}
					if (NeedsKind(num, value.WaitKind) && BestInInventory(inventory, value.WaitKind, null) == null)
					{
						return;
					}
					value.WaitHome = false;
					cs.ApplyFollowMode(num, force: true);
				}
			}
			if (flag3 && AtHome(cs))
			{
				for (int i = 0; i < value.Borrowed.Count; i++)
				{
					Entry entry = value.Borrowed[i];
					if (NeedsKind(num, entry.Kind))
					{
						continue;
					}
					ItemDrop.ItemData itemData = FindByPrefab(inventory, entry.Prefab);
					float value2;
					if (itemData == null)
					{
						value.Borrowed.RemoveAt(i);
						Save(value, zDO);
						i--;
					}
					else if (!value.KindCooldown.TryGetValue(-entry.Kind, out value2) || !(now < value2))
					{
						Container container = FindChestByPos(entry.Chest, itemData);
						if (container != null && !MHR_Chests.CanReach(cs.transform.position, container))
						{
							container = null;
						}
						if (container == null)
						{
							container = FindChestWithRoom(cs.transform.position, itemData);
						}
						if (!(container == null))
						{
							StartTrip(cs, value, component3, 2, container, entry.Kind, entry.Prefab, entry, now);
							return;
						}
						value.KindCooldown[-entry.Kind] = now + 30f;
						if (CompanionsPlugin.LogNormal)
						{
							Log("Nowhere to put back borrowed \"" + entry.Prefab + "\" - keeping it for now");
						}
					}
				}
				if (value.UnloadPending)
				{
					if (TryStartUnload(cs, value, component3, inventory, num, now))
					{
						return;
					}
				}
				else
				{
					value.UnloadActive = false;
				}
			}
			int[] array = KindsFor(num);
			if (array == null)
			{
				return;
			}
			ItemDrop.ItemData itemData2 = null;
			foreach (int num2 in array)
			{
				if (value.Refused.Contains(num2))
				{
					continue;
				}
				ItemDrop.ItemData itemData3 = BestInInventory(inventory, num2, itemData2);
				if (itemData3 != null)
				{
					if (num2 == 3 || num2 == 6)
					{
						itemData2 = itemData3;
					}
				}
				else
				{
					if (((num2 == 4 || num2 == 7) && itemData2 == null) || (value.KindCooldown.TryGetValue(num2, out var value3) && now < value3))
					{
						continue;
					}
					ItemDrop.ItemData itemData4 = BrokenOfKind(inventory, num2, itemData2);
					if ((!flag3 && num == 7) || (!flag3 && num == 15) || (num2 == 9 && !flag3))
					{
						continue;
					}
					if (!flag3)
					{
						value.WaitHome = true;
						value.WaitKind = num2;
						if (CompanionsPlugin.LogNormal)
						{
							Log(((itemData4 != null) ? ("\"" + itemData4.m_shared.m_name + "\" broke") : ("Needs " + KindName(num2))) + " and home is too far - following until we're back");
						}
						if (now >= value.NextWaitSpeak)
						{
							value.NextWaitSpeak = now + 120f;
							if (itemData4 != null)
							{
								SpeakBroke(cs, num2);
							}
							else
							{
								Speak(cs, num2);
							}
						}
						if (MHR_Owner.Of(cs) != null)
						{
							component3.SetFollowTarget(MHR_Owner.Go(cs));
						}
						break;
					}
					bool flag4 = value.RepairFailUntil.TryGetValue(num2, out var value4) && now < value4;
					if (itemData4 != null && !flag4)
					{
						CraftingStation craftingStation = FindRepairStation(cs, itemData4);
						if (craftingStation != null)
						{
							StartStationTrip(cs, value, component3, craftingStation, num2, itemData4, now);
							break;
						}
						if (CompanionsPlugin.LogDetailed)
						{
							LogDebug("No station near home can repair \"" + itemData4.m_shared.m_name + "\" - looking for another in the chests");
						}
					}
					Container container2 = FindBestInChests(cs.HasHomePosition() ? cs.GetHomePosition() : cs.transform.position, cs.transform.position, num2, itemData2, out var found);
					if (container2 == null)
					{
						value.KindCooldown[num2] = now + 60f;
						if (itemData4 != null)
						{
							if (CompanionsPlugin.LogNormal)
							{
								Log("\"" + itemData4.m_shared.m_name + "\" is broken and there's no other in the chests - following you");
							}
							if (now >= value.NextSpeak)
							{
								SpeakBroke(cs, num2);
								value.NextSpeak = now + 120f;
							}
							if (MHR_Owner.Of(cs) != null && component3.GetFollowTarget() == null)
							{
								component3.SetFollowTarget(MHR_Owner.Go(cs));
							}
						}
						else
						{
							if (CompanionsPlugin.LogNormal)
							{
								Log("Needs " + KindName(num2) + " for this job and couldn't find one in nearby chests");
							}
							if (now >= value.NextSpeak)
							{
								Speak(cs, num2);
								value.NextSpeak = now + 60f;
							}
						}
					}
					else
					{
						if (RoomFor(inventory, found, num2))
						{
							StartTrip(cs, value, component3, 1, container2, num2, (found.m_dropPrefab != null) ? found.m_dropPrefab.name : null, null, now);
							break;
						}
						value.KindCooldown[num2] = now + 30f;
						if (CompanionsPlugin.LogNormal)
						{
							Log("No room to carry " + KindName(num2));
						}
					}
				}
			}
		}
	}

	private static void StartTrip(CompanionSetup cs, State st, CompanionAI ai, int phase, Container chest, int kind, string prefab, Entry entry, float now)
	{
		st.Phase = phase;
		st.Chest = chest;
		st.Kind = kind;
		st.Prefab = prefab;
		st.ReturnEntry = entry;
		st.TripStart = now;
		st.BestDist = float.MaxValue;
		st.LastProgress = now;
		st.NextTripLog = now + 5f;
		PickSpot(cs, st, chest, now);
		st.NeedMove = true;
		st.WasFighting = false;
		ai.SetFollowTarget(null);
		MHR_Mover.Release(ai);
		if (phase == 1 || phase == 2)
		{
			cs.GetComponent<CompanionTalk>()?.Speak(ModLocalization.LocFmt((phase == 1) ? "hc_act_fetch_tool" : "hc_act_return_tool", KindName(kind)), CompanionTalk.Priority.Event, "act:tool" + phase + ":" + kind);
		}
		if (CompanionsPlugin.LogNormal)
		{
			Log(((phase == 3) ? ("Going to put away \"" + prefab + "\" and anything that stacks with it") : (((phase == 1) ? "Going to fetch " : "Going to put back ") + KindName(kind) + " (\"" + prefab + "\")")) + " at a chest " + Vector3.Distance(cs.transform.position, chest.transform.position).ToString("F0") + "m away");
		}
	}

	private static void EndTrip(CompanionSetup cs, State st, CompanionAI ai, int mode)
	{
		st.Phase = 0;
		st.Chest = null;
		st.Station = null;
		st.ReturnEntry = null;
		st.NeedMove = false;
		ai.StopMoving();
		MHR_Mover.Release(ai);
		cs.ApplyFollowMode(mode, force: true);
	}

	private static Vector3 ApproachPoint(Vector3 me, Vector3 cp)
	{
		Vector3 vector = me - cp;
		vector.y = 0f;
		if (!(vector.sqrMagnitude > 0.01f))
		{
			return cp;
		}
		return cp + vector.normalized * 1.2f;
	}

	private static void PickSpot(CompanionSetup cs, State st, Container chest, float now)
	{
		st.NextSpotPick = now + 4f;
		if (MHR_Chests.PickApproach(cs.transform.position, chest, out var spot))
		{
			st.Goal = spot;
			st.HasSpot = true;
		}
		else
		{
			st.Goal = ApproachPoint(cs.transform.position, chest.transform.position);
			st.HasSpot = false;
		}
	}

	private static void UpdateTrip(CompanionSetup cs, State st, CompanionAI ai, Humanoid hum, Inventory inv, ZDO zdo, int mode, float now, float dt)
	{
		if (st.Phase == 4)
		{
			UpdateStationTrip(cs, st, ai, inv, mode, now, dt);
			return;
		}
		if (ai.IsInCombat)
		{
			st.TripStart += dt;
			st.LastProgress = now;
			st.BestDist = float.MaxValue;
			if (!st.WasFighting)
			{
				st.WasFighting = true;
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("Trip paused - fighting");
				}
			}
			return;
		}
		if (st.WasFighting)
		{
			st.WasFighting = false;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Trip resumed after the fight");
			}
		}
		float num = ((st.Chest != null) ? Vector3.Distance(cs.transform.position, st.Chest.transform.position) : 0f);
		if (num < st.BestDist - 0.5f)
		{
			st.BestDist = num;
			st.LastProgress = now;
		}
		bool flag = (st.NeedMove && MHR_Mover.GaveUp(ai)) || (now - st.LastProgress > 45f && !MHR_Mover.Progressing(ai));
		if (((st.Chest == null) | flag) || now - st.TripStart > 150f)
		{
			st.KindCooldown[(st.Phase == 1) ? st.Kind : (-st.Kind)] = now + 60f;
			if (CompanionsPlugin.LogNormal)
			{
				string text;
				if (st.Chest == null)
				{
					text = "chest gone";
				}
				else
				{
					text = (flag ? ("couldn't get any closer, " + num.ToString("F1") + "m away") : "took too long");
				}
				Log("Gave up on the chest trip (" + text + ")");
			}
			EndTrip(cs, st, ai, mode);
			return;
		}
		if (now >= st.NextTripLog)
		{
			st.NextTripLog = now + 5f;
			if (CompanionsPlugin.LogDetailed)
			{
				LogDebug("Walking to chest: " + num.ToString("F1") + "m left");
			}
		}
		Vector3 position = st.Chest.transform.position;
		Vector3 position2 = cs.transform.position;
		bool num2 = st.HasSpot && new Vector2(position2.x - st.Goal.x, position2.z - st.Goal.z).magnitude <= 1f && Mathf.Abs(position2.y - st.Goal.y) <= 1.5f;
		bool flag2 = MHR_Chests.InReach(position2, st.Chest);
		if (!num2 && !flag2)
		{
			if (now >= st.NextSpotPick)
			{
				PickSpot(cs, st, st.Chest, now);
			}
			st.NeedMove = true;
			return;
		}
		if (!flag2 && num > 4.5f)
		{
			st.NeedMove = true;
			return;
		}
		st.NeedMove = false;
		ai.StopMoving();
		ai.LookAtPoint(position);
		if (MHR_Chests.InUse(st.Chest))
		{
			st.KindCooldown[(st.Phase == 1) ? st.Kind : (-st.Kind)] = now + 30f;
			if (CompanionsPlugin.LogNormal)
			{
				Log("Someone has the chest open - trying again later");
			}
			EndTrip(cs, st, ai, mode);
			return;
		}
		MHR_Chests.Claim(st.Chest);
		cs.GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
		Inventory inventory = st.Chest.GetInventory();
		if (inventory == null)
		{
			EndTrip(cs, st, ai, mode);
			return;
		}
		if (st.Phase == 3)
		{
			int num3 = Deposit(cs, st, inv, inventory);
			if (CompanionsPlugin.LogNormal)
			{
				Log("Put away " + num3 + " item stack(s) in a chest");
			}
		}
		else if (st.Phase == 1)
		{
			ItemDrop.ItemData launcher;
			if (st.Kind == 4)
			{
				launcher = BestInInventory(inv, 3, null);
			}
			else
			{
				launcher = ((st.Kind == 7) ? BestInInventory(inv, 6, null) : null);
			}
			ItemDrop.ItemData itemData = BestIn(inventory, st.Kind, launcher);
			if (itemData != null && RoomFor(inv, itemData, st.Kind))
			{
				string text2 = ((itemData.m_dropPrefab != null) ? itemData.m_dropPrefab.name : st.Prefab);
				int count = itemData.m_stack;
				if (st.Kind == 9 && itemData.m_stack > 5)
				{
					ItemDrop.ItemData itemData2 = itemData.Clone();
					itemData2.m_stack = 5;
					count = 5;
					if (inv.AddItem(itemData2))
					{
						inventory.RemoveItem(itemData, 5);
					}
				}
				else
				{
					inv.MoveItemToThis(inventory, itemData);
				}
				if (FindByPrefab(inv, text2) != null)
				{
					st.Borrowed.Add(new Entry
					{
						Kind = st.Kind,
						Prefab = text2,
						Count = count,
						Chest = position
					});
					Save(st, zdo);
					if (CompanionsPlugin.LogNormal)
					{
						Log("Borrowed " + count + "x \"" + text2 + "\" for the job");
					}
				}
			}
			else
			{
				st.KindCooldown[st.Kind] = now + 60f;
				if (CompanionsPlugin.LogNormal)
				{
					Log("The " + KindName(st.Kind) + " was gone from the chest");
				}
			}
		}
		else
		{
			Entry returnEntry = st.ReturnEntry;
			int num4 = returnEntry?.Count ?? 0;
			while (returnEntry != null && num4 > 0)
			{
				ItemDrop.ItemData itemData3 = FindByPrefab(inv, returnEntry.Prefab);
				if (itemData3 == null)
				{
					break;
				}
				if (itemData3.m_stack <= num4)
				{
					int stack = itemData3.m_stack;
					if (!inventory.CanAddItem(itemData3, stack))
					{
						break;
					}
					inventory.MoveItemToThis(inv, itemData3);
					if (FindSame(inv, itemData3))
					{
						break;
					}
					num4 -= stack;
				}
				else
				{
					ItemDrop.ItemData itemData4 = itemData3.Clone();
					itemData4.m_stack = num4;
					if (!inventory.CanAddItem(itemData4, num4))
					{
						break;
					}
					inventory.AddItem(itemData4);
					inv.RemoveItem(itemData3, num4);
					num4 = 0;
				}
			}
			if (returnEntry != null)
			{
				if (num4 <= 0 || FindByPrefab(inv, returnEntry.Prefab) == null)
				{
					st.Borrowed.Remove(returnEntry);
					Save(st, zdo);
					if (CompanionsPlugin.LogNormal)
					{
						Log("Put back \"" + returnEntry.Prefab + "\"");
					}
				}
				else
				{
					returnEntry.Count = num4;
					Save(st, zdo);
					st.KindCooldown[-returnEntry.Kind] = now + 30f;
					if (CompanionsPlugin.LogNormal)
					{
						Log("Chest full - kept " + num4 + "x \"" + returnEntry.Prefab + "\" for now");
					}
				}
			}
		}
		EndTrip(cs, st, ai, mode);
	}

	public static float HomeRange()
	{
		if (ModConfig.HomeTripRange == null)
		{
			return 60f;
		}
		return ModConfig.HomeTripRange.Value;
	}

	public static float AtHomeRange()
	{
		return Mathf.Min(25f, HomeRange());
	}

	public static bool AtHome(CompanionSetup cs)
	{
		if (!(cs == null) && cs.HasHomePosition())
		{
			return Vector3.Distance(cs.transform.position, cs.GetHomePosition()) <= AtHomeRange();
		}
		return true;
	}

	public static bool NearHome(CompanionSetup cs)
	{
		if (!(cs == null) && cs.HasHomePosition())
		{
			return Vector3.Distance(cs.transform.position, cs.GetHomePosition()) <= HomeRange();
		}
		return true;
	}

	private static ItemDrop.ItemData BrokenOfKind(Inventory inv, int kind, ItemDrop.ItemData launcher)
	{
		if (kind == 4 || kind == 7 || kind == 9)
		{
			return null;
		}
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			ItemDrop.ItemData itemData = allItems[i];
			if (itemData != null && itemData.m_shared != null && itemData.m_shared.m_useDurability && !(itemData.m_durability > 0f) && IsKind(itemData, kind, launcher))
			{
				return itemData;
			}
		}
		return null;
	}

	private static CraftingStation FindRepairStation(CompanionSetup cs, ItemDrop.ItemData item)
	{
		Vector3 center = (cs.HasHomePosition() ? cs.GetHomePosition() : cs.transform.position);
		CraftingStation result = null;
		float num = float.MaxValue;
		List<CraftingStation> list = MHR_Chests.Stations(center, 30f);
		for (int i = 0; i < list.Count; i++)
		{
			CraftingStation craftingStation = list[i];
			bool flag;
			try
			{
				flag = RepairController.CanRepairAt(craftingStation, item);
			}
			catch
			{
				flag = false;
			}
			if (flag)
			{
				float num2 = Vector3.Distance(cs.transform.position, craftingStation.transform.position);
				if (num2 < num)
				{
					num = num2;
					result = craftingStation;
				}
			}
		}
		return result;
	}

	private static void StartStationTrip(CompanionSetup cs, State st, CompanionAI ai, CraftingStation stn, int kind, ItemDrop.ItemData item, float now)
	{
		st.Phase = 4;
		st.Station = stn;
		st.Chest = null;
		st.Kind = kind;
		st.Prefab = ((item.m_dropPrefab != null) ? item.m_dropPrefab.name : null);
		st.ReturnEntry = null;
		st.TripStart = now;
		st.BestDist = float.MaxValue;
		st.LastProgress = now;
		st.NextTripLog = now + 5f;
		st.WasFighting = false;
		st.Goal = ApproachPoint(cs.transform.position, stn.transform.position);
		st.NeedMove = true;
		st.HasSpot = false;
		ai.SetFollowTarget(null);
		MHR_Mover.Release(ai);
		if (CompanionsPlugin.LogNormal)
		{
			Log("Going to repair \"" + item.m_shared.m_name + "\" at " + stn.m_name + " " + Vector3.Distance(cs.transform.position, stn.transform.position).ToString("F0") + "m away");
		}
	}

	private static void UpdateStationTrip(CompanionSetup cs, State st, CompanionAI ai, Inventory inv, int mode, float now, float dt)
	{
		if (ai.IsInCombat)
		{
			st.TripStart += dt;
			st.LastProgress = now;
			st.BestDist = float.MaxValue;
			return;
		}
		if (st.Station == null)
		{
			if (CompanionsPlugin.LogNormal)
			{
				Log("The station is gone");
			}
			EndTrip(cs, st, ai, mode);
			return;
		}
		Vector3 position = cs.transform.position;
		Vector3 position2 = st.Station.transform.position;
		float num = MHR_Nav.ReachDist(position, position2);
		if (num < st.BestDist - 0.5f)
		{
			st.BestDist = num;
			st.LastProgress = now;
		}
		if ((st.NeedMove && MHR_Mover.GaveUp(ai)) || (now - st.LastProgress > 45f && !MHR_Mover.Progressing(ai)) || now - st.TripStart > 150f)
		{
			st.RepairFailUntil[st.Kind] = now + 300f;
			st.KindCooldown[st.Kind] = now + 2f;
			if (CompanionsPlugin.LogNormal)
			{
				Log("Gave up on the repair trip (" + num.ToString("F1") + "m away) - looking for another in the chests");
			}
			EndTrip(cs, st, ai, mode);
			return;
		}
		if (num > 2.5f)
		{
			st.NeedMove = true;
			return;
		}
		st.NeedMove = false;
		ai.StopMoving();
		int num2 = 0;
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			ItemDrop.ItemData itemData = allItems[i];
			if (itemData != null && itemData.m_shared != null && itemData.m_shared.m_useDurability && !(itemData.m_durability >= itemData.GetMaxDurability()))
			{
				bool flag;
				try
				{
					flag = RepairController.CanRepairAt(st.Station, itemData);
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					itemData.m_durability = itemData.GetMaxDurability();
					num2++;
				}
			}
		}
		try
		{
			if (num2 > 0 && st.Station.m_repairItemDoneEffects != null)
			{
				st.Station.m_repairItemDoneEffects.Create(position2, Quaternion.identity);
			}
		}
		catch
		{
		}
		if (CompanionsPlugin.LogNormal)
		{
			Log("Repaired " + num2 + " item(s) at " + st.Station.m_name);
		}
		EndTrip(cs, st, ai, mode);
	}

	private static void SpeakBroke(CompanionSetup cs, int kind)
	{
		string key = kind switch
		{
			2 => "hc_speech_broke_pickaxe", 
			1 => "hc_speech_broke_axe", 
			_ => "hc_speech_broke_tool", 
		};
		CompanionTalk component = cs.GetComponent<CompanionTalk>();
		if (component != null)
		{
			component.SayProblem("broke:" + kind, ModLocalization.Loc(key));
		}
	}

	private static bool Keepable(ItemDrop.ItemData it)
	{
		if (it == null || it.m_shared == null)
		{
			return false;
		}
		switch (it.m_shared.m_itemType)
		{
		case ItemDrop.ItemData.ItemType.Consumable:
		case ItemDrop.ItemData.ItemType.OneHandedWeapon:
		case ItemDrop.ItemData.ItemType.Bow:
		case ItemDrop.ItemData.ItemType.Shield:
		case ItemDrop.ItemData.ItemType.Helmet:
		case ItemDrop.ItemData.ItemType.Chest:
		case ItemDrop.ItemData.ItemType.Ammo:
		case ItemDrop.ItemData.ItemType.Legs:
		case ItemDrop.ItemData.ItemType.Hands:
		case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
		case ItemDrop.ItemData.ItemType.Torch:
		case ItemDrop.ItemData.ItemType.Shoulder:
		case ItemDrop.ItemData.ItemType.Utility:
		case ItemDrop.ItemData.ItemType.Tool:
		case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
		case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
		case ItemDrop.ItemData.ItemType.Trinket:
			return true;
		default:
			return false;
		}
	}

	private static void TagOwn(Inventory inv)
	{
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			ItemDrop.ItemData itemData = allItems[i];
			if (Keepable(itemData))
			{
				if (itemData.m_customData == null)
				{
					itemData.m_customData = new Dictionary<string, string>();
				}
				itemData.m_customData["HC_Own"] = "1";
			}
		}
	}

	private static bool IsOwn(ItemDrop.ItemData it)
	{
		if (it.m_customData != null && it.m_customData.TryGetValue("HC_Own", out var value))
		{
			return value == "1";
		}
		return false;
	}

	private static bool ShouldUnload(CompanionSetup cs, State st, ItemDrop.ItemData it, int mode)
	{
		if (it == null || it.m_dropPrefab == null || it.m_equipped)
		{
			return false;
		}
		if (Keepable(it) && IsOwn(it))
		{
			return false;
		}
		string name = it.m_dropPrefab.name;
		for (int i = 0; i < st.Borrowed.Count; i++)
		{
			if (st.Borrowed[i].Prefab == name)
			{
				return false;
			}
		}
		if (mode == 8 && FarmController.s_seedToPlant != null && FarmController.s_seedToPlant.ContainsKey(name))
		{
			return false;
		}
		return true;
	}

	private static List<ItemDrop.ItemData> UnloadList(CompanionSetup cs, State st, Inventory inv, int mode)
	{
		List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>();
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			if (ShouldUnload(cs, st, allItems[i], mode))
			{
				list.Add(allItems[i]);
			}
		}
		return list;
	}

	private static bool TryStartUnload(CompanionSetup cs, State st, CompanionAI ai, Inventory inv, int mode, float now)
	{
		Vector3 position = cs.transform.position;
		bool flag = cs.HasHomePosition();
		Vector3 vector = (flag ? cs.GetHomePosition() : position);
		if (flag && Vector3.Distance(position, vector) > AtHomeRange())
		{
			st.UnloadActive = false;
			return false;
		}
		List<ItemDrop.ItemData> list = UnloadList(cs, st, inv, mode);
		if (list.Count == 0 || st.UnloadTrips >= 10)
		{
			if (list.Count > 0 && CompanionsPlugin.LogNormal)
			{
				Log("Couldn't find room for everything - keeping " + list.Count + " stack(s) for now");
			}
			st.UnloadPending = false;
			st.UnloadActive = false;
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			ItemDrop.ItemData itemData = list[i];
			Container container = ChestFor(vector, position, itemData);
			if (container == null)
			{
				continue;
			}
			st.UnloadActive = true;
			st.UnloadTrips++;
			if (now >= st.NextUnloadSpeak)
			{
				st.NextUnloadSpeak = now + 120f;
				CompanionTalk component = cs.GetComponent<CompanionTalk>();
				if (component != null)
				{
					component.Say(ModLocalization.Loc("hc_speech_unload"));
				}
			}
			StartTrip(cs, st, ai, 3, container, 0, itemData.m_dropPrefab.name, null, now);
			return true;
		}
		if (CompanionsPlugin.LogNormal)
		{
			Log("No chest near home has room for what I'm carrying - keeping " + list.Count + " stack(s)");
		}
		CompanionStatus.Why(cs, "hc_why_chests_full", true);
		st.UnloadPending = false;
		st.UnloadActive = false;
		return false;
	}

	private static Container ChestFor(Vector3 home, Vector3 me, ItemDrop.ItemData it)
	{
		string name = it.m_dropPrefab.name;
		Container container = null;
		float num = float.MaxValue;
		List<Container> list = MHR_Chests.Near(home, 30f);
		for (int i = 0; i < list.Count; i++)
		{
			Container container2 = list[i];
			if (UsableChest(container2) && FindByPrefab(container2.GetInventory(), name) != null && container2.GetInventory().CanAddItem(it, it.m_stack))
			{
				float num2 = Vector3.Distance(me, container2.transform.position);
				if (num2 < num && MHR_Chests.CanReach(me, container2))
				{
					num = num2;
					container = container2;
				}
			}
		}
		if (container != null)
		{
			return container;
		}
		for (int j = 0; j < list.Count; j++)
		{
			Container container3 = list[j];
			if (UsableChest(container3) && container3.GetInventory().CanAddItem(it, it.m_stack))
			{
				float num3 = Vector3.Distance(me, container3.transform.position);
				if (num3 < num && MHR_Chests.CanReach(me, container3))
				{
					num = num3;
					container = container3;
				}
			}
		}
		return container;
	}

	private static int Deposit(CompanionSetup cs, State st, Inventory inv, Inventory cinv)
	{
		int mode = ((cs.GetComponent<ZNetView>() != null && cs.GetComponent<ZNetView>().GetZDO() != null) ? cs.GetComponent<ZNetView>().GetZDO().GetInt(CompanionSetup.ActionModeHash) : 0);
		List<ItemDrop.ItemData> list = UnloadList(cs, st, inv, mode);
		int num = 0;
		for (int i = 0; i < list.Count; i++)
		{
			ItemDrop.ItemData itemData = list[i];
			string name = itemData.m_dropPrefab.name;
			if ((name != st.Prefab && FindByPrefab(cinv, name) == null) || !cinv.CanAddItem(itemData, itemData.m_stack))
			{
				continue;
			}
			cinv.MoveItemToThis(inv, itemData);
			if (!FindSame(inv, itemData))
			{
				num++;
				if (CompanionsPlugin.LogDetailed)
				{
					LogDebug("  Put away \"" + name + "\" x" + itemData.m_stack);
				}
			}
		}
		return num;
	}

	private static int[] KindsFor(int mode)
	{
		switch (mode)
		{
		case 1:
			return new int[1] { 1 };
		case 2:
		case 3:
			return new int[1] { 2 };
		case 7:
			return new int[2] { 3, 4 };
		case 8:
			return new int[1] { 5 };
		case 9:
			return new int[3] { 6, 7, 9 };
		case 15:
			return new int[1] { 8 };
		default:
			return null;
		}
	}

	private static HashSet<int> OwnedKinds(Inventory inv, int mode)
	{
		HashSet<int> hashSet = new HashSet<int>();
		int[] array = KindsFor(mode);
		if (array == null || inv == null)
		{
			return hashSet;
		}
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] != 4 && array[i] != 7 && array[i] != 9 && BestInInventory(inv, array[i], null) != null)
			{
				hashSet.Add(array[i]);
			}
		}
		return hashSet;
	}

	private static bool NeedsKind(int mode, int kind)
	{
		int[] array = KindsFor(mode);
		if (array == null)
		{
			return false;
		}
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] == kind)
			{
				return true;
			}
		}
		return false;
	}

	private static string KindName(int kind)
	{
		return kind switch
		{
			1 => "an axe", 
			2 => "a pickaxe", 
			3 => "a bow", 
			4 => "arrows", 
			5 => "a cultivator", 
			6 => "a fishing rod", 
			7 => "bait", 
			8 => "a hammer", 
			9 => "stamina food", 
			_ => "a tool", 
		};
	}

	private static void Speak(CompanionSetup cs, int kind)
	{
		string text = null;
		switch (kind)
		{
		case 1:
			text = "hc_speech_need_axe";
			break;
		case 2:
			text = "hc_speech_need_pickaxe";
			break;
		case 4:
			text = "hc_speech_need_arrows";
			break;
		default:
		{
			string text2 = ModLocalization.LocFmt("hc_why_need_tool", KindName(kind));
			CompanionStatus.Why(cs, "hc_why_need_tool", false, KindName(kind));
			cs.GetComponent<CompanionTalk>()?.SayProblem("tool:" + kind, text2);
			return;
		}
		}
		CompanionTalk component = cs.GetComponent<CompanionTalk>();
		if (component != null)
		{
			component.SayProblem("tool:" + kind, ModLocalization.Loc(text));
		}
	}

	private static bool IsKind(ItemDrop.ItemData it, int kind, ItemDrop.ItemData launcher)
	{
		if (it == null || it.m_shared == null)
		{
			return false;
		}
		ItemDrop.ItemData.SharedData shared = it.m_shared;
		switch (kind)
		{
		case 1:
			if (shared.m_damages.m_chop > 0f && IsWeapon(shared))
			{
				return shared.m_buildPieces == null;
			}
			return false;
		case 2:
			if (shared.m_damages.m_pickaxe > 0f && IsWeapon(shared))
			{
				return shared.m_buildPieces == null;
			}
			return false;
		case 3:
			if (shared.m_itemType == ItemDrop.ItemData.ItemType.Bow)
			{
				return shared.m_animationState != ItemDrop.ItemData.AnimationState.FishingRod;
			}
			return false;
		case 6:
			return shared.m_animationState == ItemDrop.ItemData.AnimationState.FishingRod;
		case 4:
		case 7:
			if (shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo && launcher != null && !string.IsNullOrEmpty(shared.m_ammoType))
			{
				return shared.m_ammoType == launcher.m_shared.m_ammoType;
			}
			return false;
		case 5:
			return HasPlantPiece(shared.m_buildPieces);
		case 8:
			if (shared.m_buildPieces != null && shared.m_buildPieces.name != null)
			{
				return shared.m_buildPieces.name.IndexOf("Hammer", StringComparison.OrdinalIgnoreCase) >= 0;
			}
			return false;
		case 9:
			if (shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable && shared.m_foodStamina > 0f)
			{
				return shared.m_foodStamina >= shared.m_food;
			}
			return false;
		default:
			return false;
		}
	}

	private static bool IsWeapon(ItemDrop.ItemData.SharedData s)
	{
		if (s.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon && s.m_itemType != ItemDrop.ItemData.ItemType.TwoHandedWeapon && s.m_itemType != ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft)
		{
			return s.m_itemType == ItemDrop.ItemData.ItemType.Tool;
		}
		return true;
	}

	private static bool HasPlantPiece(PieceTable pt)
	{
		if (pt == null || pt.m_pieces == null)
		{
			return false;
		}
		for (int i = 0; i < pt.m_pieces.Count; i++)
		{
			if (pt.m_pieces[i] != null && pt.m_pieces[i].GetComponent<Plant>() != null)
			{
				return true;
			}
		}
		return false;
	}

	private static float Score(ItemDrop.ItemData it, int kind)
	{
		switch (kind)
		{
		case 4:
		case 7:
			return it.m_stack;
		case 9:
			return it.m_shared.m_foodStamina;
		default:
		{
			ItemDrop.ItemData.SharedData shared = it.m_shared;
			float num = kind switch
			{
				2 => shared.m_damages.m_pickaxe, 
				1 => shared.m_damages.m_chop, 
				_ => 0f, 
			};
			return (float)shared.m_toolTier * 10000f + (float)it.m_quality * 1000f + num + it.m_durability / Mathf.Max(1f, shared.m_maxDurability);
		}
		}
	}

	private static ItemDrop.ItemData BestIn(Inventory inv, int kind, ItemDrop.ItemData launcher)
	{
		ItemDrop.ItemData result = null;
		float num = float.MinValue;
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			ItemDrop.ItemData itemData = allItems[i];
			if (IsKind(itemData, kind, launcher) && (!itemData.m_shared.m_useDurability || !(itemData.m_durability <= 0f)))
			{
				float num2 = Score(itemData, kind);
				if (num2 > num)
				{
					num = num2;
					result = itemData;
				}
			}
		}
		return result;
	}

	private static ItemDrop.ItemData BestInInventory(Inventory inv, int kind, ItemDrop.ItemData launcher)
	{
		return BestIn(inv, kind, launcher);
	}

	internal static string StaminaFoodText(Inventory inv)
	{
		ItemDrop.ItemData itemData = ((inv != null) ? BestIn(inv, 9, null) : null);
		if (itemData != null)
		{
			return (MHR_List.Local(itemData.m_shared.m_name) ?? itemData.m_shared.m_name) + " x" + itemData.m_stack + " (" + itemData.m_shared.m_foodStamina.ToString("F0") + " stamina)";
		}
		return null;
	}

	private static ItemDrop.ItemData FindByPrefab(Inventory inv, string prefab)
	{
		if (string.IsNullOrEmpty(prefab))
		{
			return null;
		}
		List<ItemDrop.ItemData> allItems = inv.GetAllItems();
		for (int i = 0; i < allItems.Count; i++)
		{
			if (allItems[i] != null && allItems[i].m_dropPrefab != null && allItems[i].m_dropPrefab.name == prefab)
			{
				return allItems[i];
			}
		}
		return null;
	}

	private static bool FindSame(Inventory inv, ItemDrop.ItemData it)
	{
		return inv.GetAllItems().Contains(it);
	}

	private static bool RoomFor(Inventory inv, ItemDrop.ItemData it, int kind)
	{
		switch (kind)
		{
		case 9:
			return inv.CanAddItem(it, 1);
		default:
			return inv.HaveEmptySlot();
		case 4:
		case 7:
			return inv.CanAddItem(it, it.m_stack);
		}
	}

	internal static bool UsableChest(Container c, Component who = null)
	{
		return MHR_Chests.Usable(c, who ?? s_current);
	}

	private static Container FindBestInChests(Vector3 center, Vector3 me, int kind, ItemDrop.ItemData launcher, out ItemDrop.ItemData found)
	{
		found = null;
		List<KeyValuePair<float, Container>> list = new List<KeyValuePair<float, Container>>();
		List<Container> list2 = MHR_Chests.Near(center, 30f);
		for (int i = 0; i < list2.Count; i++)
		{
			Container container = list2[i];
			if (UsableChest(container))
			{
				ItemDrop.ItemData itemData = BestIn(container.GetInventory(), kind, launcher);
				if (itemData != null)
				{
					list.Add(new KeyValuePair<float, Container>(Score(itemData, kind) - Vector3.Distance(center, container.transform.position) * 0.01f, container));
				}
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		list.Sort((KeyValuePair<float, Container> x, KeyValuePair<float, Container> y) => y.Key.CompareTo(x.Key));
		for (int num = 0; num < list.Count; num++)
		{
			Container value = list[num].Value;
			if (MHR_Chests.CanReach(me, value))
			{
				found = BestIn(value.GetInventory(), kind, launcher);
				return value;
			}
			if (CompanionsPlugin.LogNormal)
			{
				Log("Can't reach the chest at " + MHR_Chests.Fmt(value.transform.position) + " (no clear spot next to it) - trying another");
			}
		}
		Container value2 = list[0].Value;
		found = BestIn(value2.GetInventory(), kind, launcher);
		if (CompanionsPlugin.LogNormal)
		{
			Log("No chest with " + KindName(kind) + " looks reachable - trying the best one anyway");
		}
		return value2;
	}

	private static Container FindChestByPos(Vector3 pos, ItemDrop.ItemData it)
	{
		List<Container> list = MHR_Chests.Near(pos, 1.5f);
		for (int i = 0; i < list.Count; i++)
		{
			Container container = list[i];
			if (UsableChest(container) && container.GetInventory().CanAddItem(it, it.m_stack))
			{
				return container;
			}
		}
		return null;
	}

	private static Container FindChestWithRoom(Vector3 center, ItemDrop.ItemData it)
	{
		Container container = null;
		Container result = null;
		float num = float.MaxValue;
		float num2 = float.MaxValue;
		List<Container> list = MHR_Chests.Near(center, 30f);
		for (int i = 0; i < list.Count; i++)
		{
			Container container2 = list[i];
			if (UsableChest(container2) && container2.GetInventory().CanAddItem(it, it.m_stack))
			{
				float num3 = Vector3.Distance(center, container2.transform.position);
				if (num3 < num2)
				{
					num2 = num3;
					result = container2;
				}
				if (num3 < num && MHR_Chests.CanReach(center, container2))
				{
					num = num3;
					container = container2;
				}
			}
		}
		if (!(container != null))
		{
			return result;
		}
		return container;
	}

	private static void Load(State st, ZDO zdo)
	{
		st.Borrowed.Clear();
		string text = zdo.GetString(BorrowHash);
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		string[] array = text.Split(new char[1] { ';' });
		for (int i = 0; i < array.Length; i++)
		{
			string[] array2 = array[i].Split(new char[1] { '|' });
			if (array2.Length >= 6)
			{
				try
				{
					st.Borrowed.Add(new Entry
					{
						Kind = int.Parse(array2[0]),
						Prefab = array2[1],
						Count = int.Parse(array2[2]),
						Chest = new Vector3(float.Parse(array2[3], CultureInfo.InvariantCulture), float.Parse(array2[4], CultureInfo.InvariantCulture), float.Parse(array2[5], CultureInfo.InvariantCulture))
					});
				}
				catch
				{
				}
			}
		}
	}

	private static void Save(State st, ZDO zdo)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < st.Borrowed.Count; i++)
		{
			Entry entry = st.Borrowed[i];
			if (stringBuilder.Length > 0)
			{
				stringBuilder.Append(';');
			}
			stringBuilder.Append(entry.Kind).Append('|').Append(entry.Prefab)
				.Append('|')
				.Append(entry.Count)
				.Append('|')
				.Append(entry.Chest.x.ToString("F2", CultureInfo.InvariantCulture))
				.Append('|')
				.Append(entry.Chest.y.ToString("F2", CultureInfo.InvariantCulture))
				.Append('|')
				.Append(entry.Chest.z.ToString("F2", CultureInfo.InvariantCulture));
		}
		zdo.Set(BorrowHash, stringBuilder.ToString());
	}

	private static void LogDebug(string msg)
	{
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Tools] " + msg);
		}
	}

	private static void Log(string msg)
	{
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Tools] " + msg);
		}
	}
}
