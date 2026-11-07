using System;
using System.Collections.Generic;
using UnityEngine;

namespace Companions;

internal sealed class MHR_PileTaker
{
	public Piece Pile;

	public string Shared;

	public string Why;

	public int Got;

	private readonly string _tag;

	private int _state;

	private float _start;

	private float _arrivedAt;

	private float _downAt;

	private float _nextAct;

	private float _nextStand;

	private bool _hasStand;

	private Vector3 _stand;

	private Vector3 _at;

	private bool _hammerOut;

	private HashSet<int> _before = new HashSet<int>();

	private const float Reach = 4.5f;

	public MHR_PileTaker(Piece pile, string tag)
	{
		Pile = pile;
		Shared = pile.m_resources[0].m_resItem.m_itemData.m_shared.m_name;
		_tag = tag;
		_start = Time.time;
	}

	public static Piece Find(Vector3 center, float radius, string itemPrefab, Vector3 me, Dictionary<int, float> skip)
	{
		List<Piece> list = new List<Piece>();
		Piece.GetAllPiecesInRadius(center, radius, list);
		Piece result = null;
		float num = float.MaxValue;
		foreach (Piece item in list)
		{
			if (UpkeepController.IsPile(item) && !(item.m_resources[0].m_resItem.gameObject.name != itemPrefab) && item.m_canBeRemoved && (skip == null || !skip.TryGetValue(item.GetInstanceID(), out var value) || !(Time.time < value)) && PrivateArea.CheckAccess(item.transform.position, 0f, flash: false))
			{
				float num2 = Vector3.Distance(me, item.transform.position) - HeightAboveGround(item.transform.position) * 20f;
				if (num2 < num)
				{
					num = num2;
					result = item;
				}
			}
		}
		return result;
	}

	private static float HeightAboveGround(Vector3 p)
	{
		if (Physics.Raycast(p + Vector3.down * 0.05f, Vector3.down, out var hitInfo, 10f, LayerMask.GetMask("Default", "static_solid", "piece", "terrain")))
		{
			return p.y - hitInfo.point.y;
		}
		return 0f;
	}

	public static ItemDrop.ItemData Hammer(Humanoid h)
	{
		Inventory inventory = ((h != null) ? h.GetInventory() : null);
		if (inventory == null)
		{
			return null;
		}
		foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
		{
			ItemDrop.ItemData.SharedData sharedData = allItem?.m_shared;
			if (sharedData?.m_buildPieces != null && sharedData.m_buildPieces.name != null && sharedData.m_buildPieces.name.IndexOf("Hammer", StringComparison.OrdinalIgnoreCase) >= 0 && (!sharedData.m_useDurability || allItem.m_durability > 0f))
			{
				return allItem;
			}
		}
		return null;
	}

	public int Tick(CompanionAI ai, Humanoid hum, CompanionSetup cs)
	{
		float time = Time.time;
		if (_state == 1)
		{
			return Collect(ai, hum, time);
		}
		if (Pile == null || Pile.GetComponent<ZNetView>() == null || !Pile.GetComponent<ZNetView>().IsValid())
		{
			Why = "the pile is gone";
			return -1;
		}
		ItemDrop.ItemData itemData = Hammer(hum);
		if (itemData == null)
		{
			Why = "no working hammer";
			return -1;
		}
		Vector3 position = ai.transform.position;
		if (!InReach(position, out var aim))
		{
			if (time - _start > 90f)
			{
				Why = "couldn't get within reach of it";
				return -1;
			}
			if (!_hasStand || time >= _nextStand)
			{
				_nextStand = time + 4f;
				if (MHR_Chests.PickApproachPiece(position, Pile, out var spot))
				{
					_stand = spot;
					_hasStand = true;
				}
				else if (!_hasStand)
				{
					_stand = Pile.transform.position;
					_hasStand = true;
				}
			}
			bool flag = MHR_Nav.Move(ai, Time.deltaTime, _stand, (_stand == Pile.transform.position) ? 1.5f : 0.6f, run: true, _tag);
			if (MHR_Mover.GaveUp(ai))
			{
				Why = "no way to get to it";
				return -1;
			}
			if (!flag)
			{
				_arrivedAt = 0f;
			}
			else if (_arrivedAt == 0f)
			{
				_arrivedAt = time;
			}
			else if (time - _arrivedAt > 3f)
			{
				Why = "can't reach it from where it can stand";
				return -1;
			}
			return 0;
		}
		ai.StopMoving();
		MHR_Mover.Release(ai);
		if (!hum.IsItemEquiped(itemData))
		{
			if (cs != null)
			{
				cs.SuppressAutoEquip = true;
			}
			_hammerOut = true;
			hum.EquipItem(itemData);
			_nextAct = time + 0.6f;
			return 0;
		}
		ai.LookAtPoint(aim);
		if (time < _nextAct || hum.InAttack() || !ai.IsLookingAtPoint(aim, 15f))
		{
			return 0;
		}
		if ((!(ZoneSystem.instance != null) || !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench)) && Pile.m_craftingStation != null && CraftingStation.HaveBuildStationInRange(Pile.m_craftingStation.m_name, position) == null)
		{
			Why = "no " + MHR_List.Local(Pile.m_craftingStation.m_name) + " in range to take it down";
			return -1;
		}
		if (!PrivateArea.CheckAccess(Pile.transform.position, 0f, flash: false) || !Pile.CanBeRemoved())
		{
			Why = "it can't be taken down right now";
			return -1;
		}
		_at = Pile.transform.position;
		_before = MHR_Chests.DropsNear(_at, 4f);
		ZNetView component = Pile.GetComponent<ZNetView>();
		if (!component.IsOwner())
		{
			component.ClaimOwnership();
		}
		Pile.GetComponent<IRemoved>()?.OnRemoved();
		WearNTear component2 = Pile.GetComponent<WearNTear>();
		if (component2 != null)
		{
			component2.Remove();
		}
		else
		{
			Pile.DropResources();
			Pile.m_placeEffect.Create(_at, Pile.transform.rotation, Pile.transform, 1f, -1, ai.GetComponent<Character>().GetZDOID());
			ZNetScene.instance.Destroy(Pile.gameObject);
		}
		string text = itemData.m_shared.m_attack?.m_attackAnimation;
		ai.GetComponent<ZSyncAnimation>()?.SetTrigger((!string.IsNullOrEmpty(text)) ? text : "swing_hammer");
		_state = 1;
		_downAt = time;
		_start = time;
		return 0;
	}

	private bool InReach(Vector3 feet, out Vector3 aim)
	{
		Vector3 vector = feet + Vector3.up * 1.6f;
		aim = Pile.transform.position + Vector3.up * 0.4f;
		float num = float.MaxValue;
		Collider[] componentsInChildren = Pile.GetComponentsInChildren<Collider>();
		foreach (Collider collider in componentsInChildren)
		{
			if (!(collider == null) && collider.enabled && !collider.isTrigger)
			{
				Vector3 vector2 = collider.ClosestPoint(vector);
				float num2 = Vector3.Distance(vector, vector2);
				if (num2 < num)
				{
					num = num2;
					aim = vector2;
				}
			}
		}
		return num <= 4.5f;
	}

	private int Collect(CompanionAI ai, Humanoid hum, float now)
	{
		if (now - _start > 30f)
		{
			return 1;
		}
		if (now - _downAt < 0.6f)
		{
			return 0;
		}
		Inventory inventory = hum.GetInventory();
		Vector3 position = ai.transform.position;
		ItemDrop itemDrop = null;
		float num = float.MaxValue;
		Collider[] array = Physics.OverlapSphere(_at, 4f);
		foreach (Collider collider in array)
		{
			if (collider == null)
			{
				continue;
			}
			ItemDrop componentInParent = collider.GetComponentInParent<ItemDrop>();
			if (componentInParent != null && componentInParent.m_itemData != null && componentInParent.m_itemData.m_shared.m_name == Shared && !_before.Contains(componentInParent.GetInstanceID()) && inventory.CanAddItem(componentInParent.m_itemData, 1))
			{
				float num2 = Vector3.Distance(position, componentInParent.transform.position);
				if (num2 < num)
				{
					num = num2;
					itemDrop = componentInParent;
				}
				continue;
			}
			Container componentInParent2 = collider.GetComponentInParent<Container>();
			if (!(componentInParent2 != null) || !(componentInParent2.GetComponent<Piece>() == null) || !(componentInParent2.GetComponentInParent<Character>() == null) || componentInParent2.GetInventory() == null || componentInParent2.GetInventory().CountItems(Shared) <= 0 || !(Vector3.Distance(position, componentInParent2.transform.position) < 2.5f))
			{
				continue;
			}
			ZNetView component = componentInParent2.GetComponent<ZNetView>();
			if (component != null && !component.IsOwner())
			{
				component.ClaimOwnership();
			}
			ai.GetComponent<ZSyncAnimation>()?.SetTrigger("interact");
			foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(componentInParent2.GetInventory().GetAllItems()))
			{
				if (!(item.m_shared.m_name != Shared))
				{
					int num3 = item.m_stack;
					while (num3 > 0 && !inventory.CanAddItem(item, num3))
					{
						num3--;
					}
					if (num3 <= 0)
					{
						break;
					}
					ItemDrop.ItemData itemData = item.Clone();
					itemData.m_stack = num3;
					if (inventory.AddItem(itemData))
					{
						componentInParent2.GetInventory().RemoveItem(item, num3);
						Got += num3;
					}
				}
			}
			return 0;
		}
		if (itemDrop == null)
		{
			return 1;
		}
		if (num > 1.5f)
		{
			MHR_Nav.Move(ai, Time.deltaTime, itemDrop.transform.position, 1f, run: false, _tag);
			return 0;
		}
		if (!itemDrop.CanPickup())
		{
			itemDrop.RequestOwn();
			ai.StopMoving();
			return 0;
		}
		int stack = itemDrop.m_itemData.m_stack;
		if (hum.Pickup(itemDrop.gameObject, autoequip: false, autoPickupDelay: false))
		{
			Got += stack;
		}
		return 0;
	}

	public void PutAway(Humanoid hum, CompanionSetup cs)
	{
		if (!_hammerOut || hum == null)
		{
			return;
		}
		_hammerOut = false;
		Inventory inventory = hum.GetInventory();
		if (inventory != null)
		{
			foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
			{
				if (allItem?.m_shared?.m_buildPieces != null && hum.IsItemEquiped(allItem))
				{
					hum.UnequipItem(allItem);
				}
			}
		}
		if (cs != null && cs.SuppressAutoEquip)
		{
			cs.SuppressAutoEquip = false;
			cs.SyncEquipmentToInventory();
		}
	}
}
