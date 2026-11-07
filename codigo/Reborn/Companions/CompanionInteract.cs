using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Companions;

public class CompanionInteract : MonoBehaviour
{
	private class DistComparer : IComparer<RaycastHit>
	{
		public int Compare(RaycastHit a, RaycastHit b)
		{
			return a.distance.CompareTo(b.distance);
		}
	}

	[HarmonyPatch(typeof(Container), "Interact")]
	private static class ContainerInteract_Patch
	{
		private static bool Prefix(Container __instance, Humanoid character, bool hold, ref bool __result)
		{
			CompanionSetup component = __instance.GetComponent<CompanionSetup>();
			if (component == null)
			{
				return true;
			}
			if (Time.time - CompanionTombstoneResurrect.LastResurrectTime < 1f)
			{
				__result = false;
				return false;
			}
			ZNetView component2 = __instance.GetComponent<ZNetView>();
			if (component2?.GetZDO() == null)
			{
				return true;
			}
			Player player = character as Player;
			if (player == null)
			{
				__result = false;
				return false;
			}
			string text = component2.GetZDO().GetString(CompanionSetup.OwnerHash);
			string text2 = player.GetPlayerID().ToString();
			if (text.Length > 0 && text != text2)
			{
				player.Message(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_not_yours"));
				__result = false;
				return false;
			}
			if (ZInput.IsGamepadActive())
			{
				__result = false;
				return false;
			}
			if (!IsRadialKeyUse)
			{
				if (hold)
				{
					__result = false;
					return false;
				}
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Interact] Prefix — separate key mode, opening inventory");
				}
				OpenCompanionInventory(component, __instance, player);
				__result = true;
				return false;
			}
			if (hold)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogInfo("[Interact] Prefix — hold=true arrived, opening radial");
				}
				_pendingTapContainer = null;
				_pendingTapPlayer = null;
				_loggedPendingStart = false;
				_gamepadReleaseDetected = false;
				CompanionRadialMenu.EnsureInstance();
				if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
				{
					CompanionRadialMenu.Instance.Show(component);
				}
				__result = true;
				return false;
			}
			if (_pendingTapContainer == null)
			{
				_pendingTapContainer = __instance;
				_pendingTapPlayer = player;
				_pendingTapTime = Time.time;
				_loggedPendingStart = false;
				_pendingIsGamepad = false;
				_gamepadReleaseDetected = false;
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogInfo("[Interact] Prefix — tap deferred, " + $"InputKey={Input.GetKey(ModConfig.RadialMenuKey.Value)} " + $"time={Time.time:F3}");
				}
			}
			else if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Interact] Prefix — already pending, keeping existing timer " + $"(elapsed={Time.time - _pendingTapTime:F3}s)");
			}
			__result = false;
			return false;
		}
	}

	[HarmonyPatch(typeof(Character), "GetHoverText")]
	private static class CharacterGetHoverText_Patch
	{
		private static bool Prefix(Character __instance, ref string __result)
		{
			CompanionInteract component = __instance.GetComponent<CompanionInteract>();
			if (component == null)
			{
				return true;
			}
			__result = component.GetHoverText();
			return false;
		}
	}

	[HarmonyPatch(typeof(Character), "GetHoverName")]
	private static class CharacterGetHoverName_Patch
	{
		private static bool Prefix(Character __instance, ref string __result)
		{
			CompanionInteract component = __instance.GetComponent<CompanionInteract>();
			if (component == null)
			{
				return true;
			}
			__result = component.GetHoverName();
			return false;
		}
	}

	private ZNetView _nview;

	private Character _character;

	private static Container _pendingTapContainer;

	private static Player _pendingTapPlayer;

	private static float _pendingTapTime;

	private static bool _pendingIsGamepad;

	private static bool _gamepadReleaseDetected;

	private static bool _gpHoldActive;

	private static float _gpHoldStart;

	private static CompanionSetup _gpHoldTarget;

	private const float HoldThreshold = 0.2f;

	private static readonly FieldInfo _playerHoveringField = AccessTools.Field(typeof(Player), "m_hovering");

	private static int _directCommandFrame = -1;

	private static float _cmdCooldown;

	private static bool _holdActive;

	private static float _holdTimer;

	private static bool _holdFired;

	private static bool _mhrSuperFired;

	private static bool _mhrSuperRecall;

	private const float CmdHoldThreshold = 0.4f;

	private static readonly RaycastHit[] _cmdHitBuffer = new RaycastHit[32];

	private static readonly DistComparer _distCompare = new DistComparer();

	private static bool _loggedPendingStart;

	private static string[] _comeHereLines;

	private static string[] _attackLines;

	private static string[] _cartPullLines;

	private static string[] _cartReleasedLines;

	private static string[] _doorLines;

	private static string[] _sitLines;

	private static string[] _sleepLines;

	private static string[] _wakeLines;

	private static string[] _notMyBedLines;

	private static string[] _depositLines;

	private static string[] _depositEmptyLines;

	private static string[] _harvestLines;

	private static string[] _cancelLines;

	private static string[] _moveLines;

	private static string[] _repairLines;

	private static string[] _repairNothingLines;

	private static string[] _boardLines;

	private static string[] _tombstoneLines;

	private static string[] _smeltLines;

	private static readonly MethodInfo s_takeInput = AccessTools.Method(typeof(Player), "TakeInput");

	private static bool IsRadialKeyUse => ModConfig.RadialMenuKey.Value == KeyCode.E;

	private static string[] ComeHereLines
	{
		get
		{
			object obj = _comeHereLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_comehere_1"),
					ModLocalization.Loc("hc_cmd_comehere_2"),
					ModLocalization.Loc("hc_cmd_comehere_3")
				};
				_comeHereLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] AttackLines
	{
		get
		{
			object obj = _attackLines;
			if (obj == null)
			{
				obj = new string[4]
				{
					ModLocalization.Loc("hc_cmd_attack_1"),
					ModLocalization.Loc("hc_cmd_attack_2"),
					ModLocalization.Loc("hc_cmd_attack_3"),
					ModLocalization.Loc("hc_cmd_attack_4")
				};
				_attackLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] CartPullLines
	{
		get
		{
			object obj = _cartPullLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_cart_pull_1"),
					ModLocalization.Loc("hc_cmd_cart_pull_2"),
					ModLocalization.Loc("hc_cmd_cart_pull_3")
				};
				_cartPullLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] CartReleasedLines
	{
		get
		{
			object obj = _cartReleasedLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_cart_release_1"),
					ModLocalization.Loc("hc_cmd_cart_release_2"),
					ModLocalization.Loc("hc_cmd_cart_release_3")
				};
				_cartReleasedLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] DoorLines
	{
		get
		{
			object obj = _doorLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_door_1"),
					ModLocalization.Loc("hc_cmd_door_2"),
					ModLocalization.Loc("hc_cmd_door_3")
				};
				_doorLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] DoorCloseLines => new string[3]
	{
		ModLocalization.Loc("hc_cmd_doorclose_1"),
		ModLocalization.Loc("hc_cmd_doorclose_2"),
		ModLocalization.Loc("hc_cmd_doorclose_3")
	};

	private static string[] SitLines
	{
		get
		{
			object obj = _sitLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_sit_1"),
					ModLocalization.Loc("hc_cmd_sit_2"),
					ModLocalization.Loc("hc_cmd_sit_3")
				};
				_sitLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] SleepLines
	{
		get
		{
			object obj = _sleepLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_sleep_1"),
					ModLocalization.Loc("hc_cmd_sleep_2"),
					ModLocalization.Loc("hc_cmd_sleep_3")
				};
				_sleepLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] WakeLines
	{
		get
		{
			object obj = _wakeLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_wake_1"),
					ModLocalization.Loc("hc_cmd_wake_2"),
					ModLocalization.Loc("hc_cmd_wake_3")
				};
				_wakeLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] DepositLines
	{
		get
		{
			object obj = _depositLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_deposit_1"),
					ModLocalization.Loc("hc_cmd_deposit_2"),
					ModLocalization.Loc("hc_cmd_deposit_3")
				};
				_depositLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] DepositEmptyLines
	{
		get
		{
			object obj = _depositEmptyLines;
			if (obj == null)
			{
				obj = new string[2]
				{
					ModLocalization.Loc("hc_cmd_deposit_empty_1"),
					ModLocalization.Loc("hc_cmd_deposit_empty_2")
				};
				_depositEmptyLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] HarvestLines
	{
		get
		{
			object obj = _harvestLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_harvest_1"),
					ModLocalization.Loc("hc_cmd_harvest_2"),
					ModLocalization.Loc("hc_cmd_harvest_3")
				};
				_harvestLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] CancelLines
	{
		get
		{
			object obj = _cancelLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_cancel_1"),
					ModLocalization.Loc("hc_cmd_cancel_2"),
					ModLocalization.Loc("hc_cmd_cancel_3")
				};
				_cancelLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] MoveLines
	{
		get
		{
			object obj = _moveLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_move_1"),
					ModLocalization.Loc("hc_cmd_move_2"),
					ModLocalization.Loc("hc_cmd_move_3")
				};
				_moveLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] RepairLines
	{
		get
		{
			object obj = _repairLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_repair_1"),
					ModLocalization.Loc("hc_cmd_repair_2"),
					ModLocalization.Loc("hc_cmd_repair_3")
				};
				_repairLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] RepairNothingLines
	{
		get
		{
			object obj = _repairNothingLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_repair_nothing_1"),
					ModLocalization.Loc("hc_cmd_repair_nothing_2"),
					ModLocalization.Loc("hc_cmd_repair_nothing_3")
				};
				_repairNothingLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] BoardLines
	{
		get
		{
			object obj = _boardLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_board_1"),
					ModLocalization.Loc("hc_cmd_board_2"),
					ModLocalization.Loc("hc_cmd_board_3")
				};
				_boardLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] TombstoneLines
	{
		get
		{
			object obj = _tombstoneLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_tombstone_1"),
					ModLocalization.Loc("hc_cmd_tombstone_2"),
					ModLocalization.Loc("hc_cmd_tombstone_3")
				};
				_tombstoneLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	private static string[] SmeltLines
	{
		get
		{
			object obj = _smeltLines;
			if (obj == null)
			{
				obj = new string[3]
				{
					ModLocalization.Loc("hc_cmd_smelt_1"),
					ModLocalization.Loc("hc_cmd_smelt_2"),
					ModLocalization.Loc("hc_cmd_smelt_3")
				};
				_smeltLines = (string[])obj;
			}
			return (string[])obj;
		}
	}

	internal static bool ShouldKeep(ItemDrop.ItemData item, Humanoid humanoid)
	{
		if (item == null || item.m_shared == null)
		{
			return true;
		}
		if (humanoid.IsItemEquiped(item))
		{
			return true;
		}
		ItemDrop.ItemData.ItemType itemType = item.m_shared.m_itemType;
		if (itemType == ItemDrop.ItemData.ItemType.Consumable && (item.m_shared.m_food > 0f || item.m_shared.m_foodStamina > 0f || item.m_shared.m_foodEitr > 0f))
		{
			return true;
		}
		switch (itemType)
		{
		case ItemDrop.ItemData.ItemType.OneHandedWeapon:
		case ItemDrop.ItemData.ItemType.Bow:
		case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
		case ItemDrop.ItemData.ItemType.Torch:
		case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
			return true;
		case ItemDrop.ItemData.ItemType.Shield:
		case ItemDrop.ItemData.ItemType.Helmet:
		case ItemDrop.ItemData.ItemType.Chest:
		case ItemDrop.ItemData.ItemType.Legs:
		case ItemDrop.ItemData.ItemType.Hands:
		case ItemDrop.ItemData.ItemType.Shoulder:
		case ItemDrop.ItemData.ItemType.Utility:
			return true;
		default:
			return false;
		}
	}

	private void Awake()
	{
		_nview = GetComponent<ZNetView>();
		_character = GetComponent<Character>();
	}

	private void Update()
	{
		HandleDirectCommand();
		if (ZInput.IsGamepadActive())
		{
			HandleGamepadHold();
		}
		if (!IsRadialKeyUse)
		{
			HandleSeparateKeyRadial();
			if (!_gpHoldActive)
			{
				ProcessPendingTap();
			}
		}
		else if (!_gpHoldActive)
		{
			ProcessPendingTap();
		}
	}

	private static void HandleDirectCommand()
	{
		if (Time.frameCount == _directCommandFrame)
		{
			return;
		}
		_directCommandFrame = Time.frameCount;
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			_holdActive = false;
			return;
		}
		if (InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible())
		{
			_holdActive = false;
			return;
		}
		if (TextInput.IsVisible() || Console.IsVisible() || StoreGui.IsVisible())
		{
			_holdActive = false;
			return;
		}
		if (Chat.instance != null && Chat.instance.HasFocus())
		{
			_holdActive = false;
			return;
		}
		if (Hud.IsPieceSelectionVisible())
		{
			_holdActive = false;
			return;
		}
		MHR_StanceKeys.Tick(localPlayer);
		if (CompanionRadialMenu.Instance != null && CompanionRadialMenu.Instance.IsVisible)
		{
			_holdActive = false;
			return;
		}
		if (!ZInput.IsGamepadActive() && ((FarmZonePlacer.Instance != null && FarmZonePlacer.Instance.IsVisible) || Input.GetKey(ModConfig.FarmZoneModifier.Value)))
		{
			_holdActive = false;
			_holdTimer = 0f;
			return;
		}
		float deltaTime = Time.deltaTime;
		_cmdCooldown -= deltaTime;
		bool flag = ZInput.IsGamepadActive();
		bool flag2 = (flag ? MHR_Pad.Down(ModConfig.PadCommand, ModConfig.PadCommandModifier) : Input.GetKeyDown(ModConfig.DirectTargetKey.Value));
		bool flag3 = (flag ? MHR_Pad.Held(ModConfig.PadCommand, ModConfig.PadCommandModifier) : Input.GetKey(ModConfig.DirectTargetKey.Value));
		if (flag && MHR_Pad.Plain(ModConfig.PadCommand, ModConfig.PadCommandModifier, "Use") && (flag2 | flag3) && (localPlayer.GetHoverObject() != null || Hud.InRadial()))
		{
			_holdActive = false;
			_holdTimer = 0f;
			return;
		}
		if (flag2 && _cmdCooldown <= 0f)
		{
			_holdActive = true;
			_holdTimer = 0f;
			_holdFired = false;
			_mhrSuperFired = false;
		}
		if (_holdActive & flag3)
		{
			_holdTimer += deltaTime;
			if (_holdTimer >= 0.4f)
			{
				if (!_holdFired)
				{
					_holdFired = true;
					_cmdCooldown = 0.5f;
					FireComeToMe(localPlayer);
				}
				else if (!_mhrSuperFired && _holdTimer >= ModConfig.SuperRecallHoldTime.Value)
				{
					_mhrSuperFired = true;
					_cmdCooldown = 0.5f;
					MHR_FireSuperRecall(localPlayer);
				}
			}
		}
		else
		{
			if (!_holdActive)
			{
				return;
			}
			_holdActive = false;
			if (_holdFired || _cmdCooldown > 0f)
			{
				return;
			}
			_cmdCooldown = 0.5f;
			Camera mainCamera = Utils.GetMainCamera();
			if (mainCamera == null)
			{
				return;
			}
			Ray ray = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
			int mask = LayerMask.GetMask("character", "character_net", "character_ghost", "character_noenv", "Default", "Default_small", "piece", "piece_nonsolid", "static_solid", "terrain", "vehicle", "item");
			int num = Physics.RaycastNonAlloc(ray, _cmdHitBuffer, 50f, mask);
			if (num > 1)
			{
				Array.Sort(_cmdHitBuffer, 0, num, _distCompare);
			}
			IReadOnlyList<CompanionSetup> allCompanions = CompanionSetup.AllCompanions;
			string localId = localPlayer.GetPlayerID().ToString();
			for (int i = 0; i < num; i++)
			{
				Collider collider = _cmdHitBuffer[i].collider;
				if (collider == null || collider.GetComponentInParent<Player>() != null || collider.GetComponentInParent<CompanionSetup>() != null)
				{
					continue;
				}
				Character componentInParent = collider.GetComponentInParent<Character>();
				if (componentInParent != null)
				{
					if (componentInParent.GetHealth() > 0f && BaseAI.IsEnemy(localPlayer, componentInParent))
					{
						DirectAttack(allCompanions, localId, componentInParent);
						return;
					}
					continue;
				}
				Vagon componentInParent2 = collider.GetComponentInParent<Vagon>();
				if (componentInParent2 != null)
				{
					DirectCart(allCompanions, localId, componentInParent2);
					return;
				}
				Door componentInParent3 = collider.GetComponentInParent<Door>();
				if (componentInParent3 != null)
				{
					DirectDoor(allCompanions, localId, componentInParent3);
					return;
				}
				Fireplace componentInParent4 = collider.GetComponentInParent<Fireplace>();
				if (componentInParent4 != null)
				{
					DirectSit(allCompanions, localId, componentInParent4);
					return;
				}
				Bed componentInParent5 = collider.GetComponentInParent<Bed>();
				if (componentInParent5 != null)
				{
					DirectSleep(allCompanions, localId, componentInParent5);
					return;
				}
				CraftingStation componentInParent6 = collider.GetComponentInParent<CraftingStation>();
				if (componentInParent6 != null)
				{
					DirectRepair(allCompanions, localId, componentInParent6);
					return;
				}
				if (collider.GetComponentInParent<Smelter>() != null)
				{
					DirectSmelt(allCompanions, localId);
					return;
				}
				Beehive componentInParent7 = collider.GetComponentInParent<Beehive>();
				if (componentInParent7 != null)
				{
					DirectHive(allCompanions, localId, componentInParent7);
					return;
				}
				Ship componentInParent8 = collider.GetComponentInParent<Ship>();
				if (componentInParent8 != null)
				{
					Chair componentInParent9 = collider.GetComponentInParent<Chair>();
					DirectBoard(allCompanions, localId, componentInParent8, (componentInParent9 != null && componentInParent9.m_inShip && !MHR_Board.IsMast(componentInParent9)) ? componentInParent9 : null);
					return;
				}
				TombStone componentInParent10 = collider.GetComponentInParent<TombStone>();
				if (componentInParent10 != null)
				{
					DirectTombstoneRecovery(allCompanions, localId, componentInParent10);
					return;
				}
				Container componentInParent11 = collider.GetComponentInParent<Container>();
				if (componentInParent11 != null && componentInParent11.GetComponent<CompanionSetup>() == null)
				{
					DirectDeposit(allCompanions, localId, componentInParent11);
					return;
				}
				GameObject harvestable = GetHarvestable(collider);
				if (harvestable != null)
				{
					if (harvestable.GetComponent<MineRock5>() == null)
					{
						for (int j = i + 1; j < num; j++)
						{
							Collider collider2 = _cmdHitBuffer[j].collider;
							if (!(collider2 == null))
							{
								MineRock5 componentInParent12 = collider2.GetComponentInParent<MineRock5>();
								if (componentInParent12 != null)
								{
									harvestable = componentInParent12.gameObject;
									break;
								}
							}
						}
					}
					DirectGatherMode(allCompanions, localId, harvestable);
					return;
				}
				int layer = collider.gameObject.layer;
				if (layer == LayerMask.NameToLayer("terrain") || layer == LayerMask.NameToLayer("Default") || layer == LayerMask.NameToLayer("static_solid") || layer == LayerMask.NameToLayer("piece") || layer == LayerMask.NameToLayer("piece_nonsolid"))
				{
					DirectGround(allCompanions, localId, _cmdHitBuffer[i].point);
					return;
				}
			}
			CancelAll(allCompanions, localId);
		}
	}

	private void HandleGamepadCombo(Player localPlayer)
	{
		if (!GameTakesInput(localPlayer))
		{
			return;
		}
		GameObject gameObject = _playerHoveringField?.GetValue(localPlayer) as GameObject;
		CompanionSetup companionSetup = ((gameObject != null) ? gameObject.GetComponentInParent<CompanionSetup>() : null);
		CompanionSetup component = GetComponent<CompanionSetup>();
		if (companionSetup == null || companionSetup != component || (CompanionRadialMenu.Instance != null && CompanionRadialMenu.Instance.IsVisible) || InventoryGui.IsVisible())
		{
			return;
		}
		bool flag = MHR_Pad.Down(ModConfig.PadWheel, ModConfig.PadWheelModifier);
		bool flag2 = !flag && ZInput.GetButtonDown("JoyUse");
		if (!flag && !flag2)
		{
			return;
		}
		string text = companionSetup.GetComponent<ZNetView>()?.GetZDO()?.GetString(CompanionSetup.OwnerHash) ?? "";
		if (text.Length > 0 && text != localPlayer.GetPlayerID().ToString())
		{
			localPlayer.Message(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_not_yours"));
			return;
		}
		if (flag)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo("[Interact] Controller wheel bind on \"" + companionSetup.name + "\" - opening the command wheel");
			}
			CompanionRadialMenu.EnsureInstance();
			if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
			{
				CompanionRadialMenu.Instance.Show(companionSetup);
			}
			return;
		}
		Container component2 = companionSetup.GetComponent<Container>();
		if (component2 != null)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo("[Interact] Controller Use on \"" + companionSetup.name + "\" - opening its inventory");
			}
			OpenCompanionInventory(companionSetup, component2, localPlayer);
		}
	}

	private static bool GameTakesInput(Player p)
	{
		try
		{
			return p != null && (s_takeInput == null || (bool)s_takeInput.Invoke(p, null));
		}
		catch
		{
			return true;
		}
	}

	private void HandleGamepadHold()
	{
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null || !GameTakesInput(localPlayer))
		{
			ResetGamepadHold();
			return;
		}
		if (!MHR_Pad.Plain(ModConfig.PadWheel, ModConfig.PadWheelModifier, "Use"))
		{
			ResetGamepadHold();
			HandleGamepadCombo(localPlayer);
			return;
		}
		GameObject gameObject = _playerHoveringField?.GetValue(localPlayer) as GameObject;
		CompanionSetup companionSetup = ((gameObject != null) ? gameObject.GetComponentInParent<CompanionSetup>() : null);
		CompanionSetup component = GetComponent<CompanionSetup>();
		if (companionSetup == null || companionSetup != component)
		{
			if (_gpHoldActive && _gpHoldTarget == component)
			{
				ResetGamepadHold();
			}
		}
		else if (CompanionRadialMenu.Instance != null && CompanionRadialMenu.Instance.IsVisible)
		{
			ResetGamepadHold();
		}
		else if (ZInput.GetButton("JoyUse"))
		{
			if (!_gpHoldActive)
			{
				_gpHoldActive = true;
				_gpHoldStart = Time.time;
				_gpHoldTarget = companionSetup;
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Interact] Gamepad independent hold START on \"" + companionSetup.name + "\"");
				}
			}
			else if (_gpHoldTarget == companionSetup && Time.time - _gpHoldStart >= 0.2f)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogInfo("[Interact] Gamepad independent hold → RADIAL " + $"(held {Time.time - _gpHoldStart:F3}s)");
				}
				ClearPendingTap();
				ResetGamepadHold();
				CompanionRadialMenu.EnsureInstance();
				if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
				{
					CompanionRadialMenu.Instance.Show(companionSetup);
				}
			}
		}
		else
		{
			if (!_gpHoldActive || !(_gpHoldTarget == companionSetup))
			{
				return;
			}
			float num = Time.time - _gpHoldStart;
			ClearPendingTap();
			ResetGamepadHold();
			if (num >= 0.2f)
			{
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogInfo("[Interact] Gamepad hold → RADIAL on release " + $"(held {num:F3}s, threshold={0.2f}s)");
				}
				CompanionRadialMenu.EnsureInstance();
				if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
				{
					CompanionRadialMenu.Instance.Show(companionSetup);
				}
			}
			else
			{
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogInfo("[Interact] Gamepad hold → TAP " + $"(released after {num:F3}s), opening inventory");
				}
				Container component2 = companionSetup.GetComponent<Container>();
				if (component2 != null)
				{
					OpenCompanionInventory(companionSetup, component2, localPlayer);
				}
			}
		}
	}

	private void HandleSeparateKeyRadial()
	{
		if (!Input.GetKeyDown(ModConfig.RadialMenuKey.Value))
		{
			return;
		}
		Player localPlayer = Player.m_localPlayer;
		if (localPlayer == null)
		{
			return;
		}
		GameObject gameObject = _playerHoveringField?.GetValue(localPlayer) as GameObject;
		if (gameObject == null)
		{
			return;
		}
		CompanionSetup componentInParent = gameObject.GetComponentInParent<CompanionSetup>();
		if (componentInParent == null)
		{
			return;
		}
		ZNetView component = componentInParent.GetComponent<ZNetView>();
		if (component?.GetZDO() == null)
		{
			return;
		}
		string text = component.GetZDO().GetString(CompanionSetup.OwnerHash);
		if (text.Length > 0 && text != localPlayer.GetPlayerID().ToString())
		{
			localPlayer.Message(MessageHud.MessageType.Center, ModLocalization.Loc("hc_msg_not_yours"));
			return;
		}
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogInfo($"[Interact] Separate key ({ModConfig.RadialMenuKey.Value}) — opening radial");
		}
		CompanionRadialMenu.EnsureInstance();
		if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
		{
			CompanionRadialMenu.Instance.Show(componentInParent);
		}
	}

	private void ProcessPendingTap()
	{
		if (_pendingTapContainer == null)
		{
			return;
		}
		float num = Time.time - _pendingTapTime;
		if (_pendingIsGamepad && !_gamepadReleaseDetected && ZInput.GetButtonUp("JoyUse"))
		{
			_gamepadReleaseDetected = true;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo($"[Interact] Gamepad ButtonUp detected at {num:F3}s");
			}
		}
		bool flag = ((!_pendingIsGamepad) ? Input.GetKey(ModConfig.RadialMenuKey.Value) : (!_gamepadReleaseDetected));
		if (!_loggedPendingStart)
		{
			_loggedPendingStart = true;
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo("[Interact] Update — processing pending tap: " + $"useHeld={flag} gamepad={_pendingIsGamepad} " + $"key={ModConfig.RadialMenuKey.Value} " + $"elapsed={num:F3}s");
			}
		}
		if (!flag)
		{
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo($"[Interact] Update — RELEASED after {num:F3}s " + $"(gamepad={_pendingIsGamepad}), opening inventory");
			}
			Container pendingTapContainer = _pendingTapContainer;
			Player pendingTapPlayer = _pendingTapPlayer;
			CompanionSetup companionSetup = pendingTapContainer?.GetComponent<CompanionSetup>();
			_pendingTapContainer = null;
			_pendingTapPlayer = null;
			_loggedPendingStart = false;
			_gamepadReleaseDetected = false;
			if (pendingTapContainer != null && pendingTapPlayer != null && companionSetup != null)
			{
				OpenCompanionInventory(companionSetup, pendingTapContainer, pendingTapPlayer);
			}
		}
		else
		{
			if (!(num >= 0.2f))
			{
				return;
			}
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogInfo($"[Interact] Update — hold threshold reached ({num:F3}s), " + $"gamepad={_pendingIsGamepad}, opening radial");
			}
			CompanionSetup component = _pendingTapContainer.GetComponent<CompanionSetup>();
			_pendingTapContainer = null;
			_pendingTapPlayer = null;
			_loggedPendingStart = false;
			_gamepadReleaseDetected = false;
			if (!(component != null))
			{
				return;
			}
			CompanionRadialMenu.EnsureInstance();
			if (CompanionRadialMenu.Instance != null && !CompanionRadialMenu.Instance.IsVisible)
			{
				CompanionRadialMenu.Instance.Show(component);
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogInfo("[Interact] Radial menu Show() called");
				}
			}
		}
	}

	[SpecialName]
	private static string[] get_NotMyBedLines()
	{
		object obj = _notMyBedLines;
		if (obj == null)
		{
			obj = new string[3]
			{
				ModLocalization.Loc("hc_cmd_notmybed_1"),
				ModLocalization.Loc("hc_cmd_notmybed_2"),
				ModLocalization.Loc("hc_cmd_notmybed_3")
			};
			_notMyBedLines = (string[])obj;
		}
		return (string[])obj;
	}

	internal static void ResetCachedLines()
	{
		_comeHereLines = null;
		_attackLines = null;
		_cartPullLines = null;
		_cartReleasedLines = null;
		_doorLines = null;
		_sitLines = null;
		_sleepLines = null;
		_wakeLines = null;
		_depositLines = null;
		_depositEmptyLines = null;
		_harvestLines = null;
		_cancelLines = null;
		_moveLines = null;
		_repairLines = null;
		_repairNothingLines = null;
		_boardLines = null;
		_tombstoneLines = null;
		_smeltLines = null;
	}

	private static void FireComeToMe(Player player)
	{
		IReadOnlyList<CompanionSetup> allCompanions = CompanionSetup.AllCompanions;
		string localId = player.GetPlayerID().ToString();
		CancelAll(allCompanions, localId);
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup item in allCompanions)
		{
			if (!IsOwned(item, localId) || !MHR_InScope(item))
			{
				continue;
			}
			MHR_Owner.TakeControl(item);
			CompanionRest.MHR_StayAwakeForRecall(item);
			MHR_JobRules.FollowSetByPlayer(item);
			if (!item.GetFollow())
			{
				item.SetFollow(v: true);
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Direct] Come-to-me: forced Follow ON for \"" + (item.GetComponent<Character>()?.m_name ?? "?") + "\"");
				}
			}
			CompanionAI component = item.GetComponent<CompanionAI>();
			if (component != null && Player.m_localPlayer != null)
			{
				component.SetFollowTarget(Player.m_localPlayer.gameObject);
			}
			item.GetComponent<CompanionCombatAI>()?.HoldForOrder("Recalled");
			ReturnLogic.Trigger(item, player.gameObject);
			if (companionTalk == null)
			{
				companionTalk = item.GetComponent<CompanionTalk>();
			}
		}
		SayRandom(companionTalk, ComeHereLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Direct] Come-to-me triggered");
		}
	}

	internal static void MHR_FireSuperRecall(Player player)
	{
		_mhrSuperRecall = true;
		try
		{
			FireComeToMe(player);
		}
		finally
		{
			_mhrSuperRecall = false;
		}
		MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, "Recalling all companions");
		if (CompanionsPlugin.LogNormal)
		{
			CompanionsPlugin.Log.LogInfo("[Direct] Super recall: all companions, any distance, Command toggle ignored");
		}
	}

	private static void DirectAttack(IReadOnlyList<CompanionSetup> setups, string localId, Character enemy)
	{
		int num = 0;
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CompanionAI component = setup.GetComponent<CompanionAI>();
			if (!(component == null))
			{
				CancelExistingActions(setup);
				component.DirectEngage(enemy);
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, AttackLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → attack \"{enemy.m_name}\"");
		}
	}

	private static void DirectCart(IReadOnlyList<CompanionSetup> setups, string localId, Vagon vagon)
	{
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId))
			{
				continue;
			}
			Character component = setup.GetComponent<Character>();
			if (!(component != null) || !vagon.IsAttached(component))
			{
				continue;
			}
			Humanoid component2 = setup.GetComponent<Humanoid>();
			if (component2 != null)
			{
				vagon.Interact(component2, hold: false, alt: false);
				CompanionAI component3 = setup.GetComponent<CompanionAI>();
				if (component3 != null && Player.m_localPlayer != null)
				{
					component3.SetFollowTarget(Player.m_localPlayer.gameObject);
				}
				SayRandom(setup.GetComponent<CompanionTalk>(), CartReleasedLines);
				if (CompanionsPlugin.LogDetailed)
				{
					CompanionsPlugin.Log.LogDebug("[Direct] Companion → detach from cart");
				}
			}
			return;
		}
		CompanionSetup companionSetup = null;
		float num = float.MaxValue;
		foreach (CompanionSetup setup2 in setups)
		{
			if (IsOwned(setup2, localId) && MHR_InScope(setup2))
			{
				float num2 = Vector3.Distance(setup2.transform.position, vagon.transform.position);
				if (num2 < num)
				{
					num = num2;
					companionSetup = setup2;
				}
			}
		}
		if (companionSetup == null)
		{
			return;
		}
		Humanoid component4 = companionSetup.GetComponent<Humanoid>();
		CompanionAI component5 = companionSetup.GetComponent<CompanionAI>();
		if (component4 == null || component5 == null)
		{
			return;
		}
		CancelExistingActions(companionSetup);
		Vector3 vector = vagon.m_attachPoint.position - vagon.m_attachOffset;
		vector.y = companionSetup.transform.position.y;
		if (Vector3.Distance(companionSetup.transform.position, vector) < 3f)
		{
			companionSetup.transform.position = vector;
			Rigidbody component6 = companionSetup.GetComponent<Rigidbody>();
			if (component6 != null)
			{
				component6.position = vector;
				component6.linearVelocity = Vector3.zero;
			}
			Vector3 vector2 = vagon.transform.position - companionSetup.transform.position;
			vector2.y = 0f;
			if (vector2.sqrMagnitude > 0.01f)
			{
				companionSetup.transform.rotation = Quaternion.LookRotation(vector2.normalized);
			}
			component5.FreezeTimer = 1f;
			component5.SetFollowTarget(vagon.gameObject);
			vagon.Interact(component4, hold: false, alt: false);
		}
		else
		{
			component5.SetPendingCart(vagon, component4);
		}
		SayRandom(companionSetup.GetComponent<CompanionTalk>(), CartPullLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] Companion → cart attach (dist={num:F1}m)");
		}
	}

	private static void DirectDoor(IReadOnlyList<CompanionSetup> setups, string localId, Door door)
	{
		bool flag = DoorHandler.IsOpen(door);
		CompanionSetup companionSetup = null;
		float num = float.MaxValue;
		foreach (CompanionSetup setup in setups)
		{
			if (IsOwned(setup, localId) && MHR_InScope(setup) && !(setup.GetComponent<DoorHandler>() == null))
			{
				float num2 = Vector3.Distance(setup.transform.position, door.transform.position);
				if (num2 < num)
				{
					num = num2;
					companionSetup = setup;
				}
			}
		}
		if (companionSetup == null)
		{
			return;
		}
		CompanionTalk component = companionSetup.GetComponent<CompanionTalk>();
		if (!DoorHandler.IsUsableDoor(door))
		{
			SayRandom(component, new string[1] { ModLocalization.Loc("hc_cmd_door_locked") });
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Direct] door \"" + door.m_name + "\" is locked - not going");
			}
			return;
		}
		CancelExistingActions(companionSetup);
		companionSetup.GetComponent<DoorHandler>().DirectUseDoor(door, flag);
		SayRandom(component, flag ? DoorCloseLines : DoorLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug(string.Format("[Direct] \"{0}\" → {1} door \"{2}\" ({3:F1} m away)", companionSetup.GetComponent<Character>()?.m_name, flag ? "close" : "open", door.m_name, num));
		}
	}

	private static void DirectSit(IReadOnlyList<CompanionSetup> setups, string localId, Fireplace fire)
	{
		if (!fire.IsBurning())
		{
			return;
		}
		int num = 0;
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CancelExistingActions(setup, cancelRest: false);
			CompanionRest component = setup.GetComponent<CompanionRest>();
			if (!(component == null))
			{
				component.DirectSit(fire.gameObject);
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, SitLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → sit near fire");
		}
	}

	private static void DirectSleep(IReadOnlyList<CompanionSetup> setups, string localId, Bed bed)
	{
		int num = 0;
		int num2 = 0;
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CancelExistingActions(setup, cancelRest: false);
			CompanionRest component = setup.GetComponent<CompanionRest>();
			if (component == null)
			{
				continue;
			}
			bool flag = component.IsResting || component.IsNavigating;
			if (!component.DirectSleep(bed))
			{
				SayRandom(setup.GetComponent<CompanionTalk>(), get_NotMyBedLines());
				continue;
			}
			bool flag2 = component.IsResting || component.IsNavigating;
			if (!flag & flag2)
			{
				num++;
			}
			else if (flag && !flag2)
			{
				num2++;
			}
			if (companionTalk == null)
			{
				companionTalk = setup.GetComponent<CompanionTalk>();
			}
		}
		if (num2 > 0)
		{
			SayRandom(companionTalk, WakeLines);
		}
		else if (num > 0)
		{
			SayRandom(companionTalk, SleepLines);
		}
		else
		{
			SayRandom(companionTalk, CancelLines);
		}
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] Sleep — started={num}, wokeUp={num2}");
		}
	}

	private static void DirectRepair(IReadOnlyList<CompanionSetup> setups, string localId, CraftingStation station)
	{
		CompanionTalk companionTalk = null;
		int num = 0;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			RepairController component = setup.GetComponent<RepairController>();
			if (!(component == null))
			{
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				CancelExistingActions(setup);
				if (component.DirectRepairAt(station))
				{
					num++;
				}
			}
		}
		if (num > 0)
		{
			SayRandom(companionTalk, RepairLines);
		}
		else
		{
			SayRandom(companionTalk, RepairNothingLines);
		}
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → repair at \"{station.m_name}\"");
		}
	}

	private static void DirectHive(IReadOnlyList<CompanionSetup> setups, string localId, Beehive hive)
	{
		CompanionSetup companionSetup = null;
		float num = float.MaxValue;
		foreach (CompanionSetup setup in setups)
		{
			if (IsOwned(setup, localId) && MHR_InScope(setup) && !(setup.GetComponent<CompanionAI>() == null))
			{
				CompanionRest component = setup.GetComponent<CompanionRest>();
				bool flag = component != null && (component.IsResting || component.IsNavigating || component.IsSleeping);
				float num2 = Vector3.Distance(setup.transform.position, hive.transform.position) + (flag ? 1000f : 0f);
				if (num2 < num)
				{
					num = num2;
					companionSetup = setup;
				}
			}
		}
		CompanionSetup[] array = ((!(companionSetup != null)) ? new CompanionSetup[0] : new CompanionSetup[1] { companionSetup });
		int num3 = 0;
		if (num3 >= array.Length)
		{
			return;
		}
		CompanionSetup companionSetup2 = array[num3];
		CompanionAI component2 = companionSetup2.GetComponent<CompanionAI>();
		CompanionTalk component3 = companionSetup2.GetComponent<CompanionTalk>();
		if (hive.m_honeyItem == null || MHR_Hive.HoneyIn(hive) <= 0)
		{
			SayRandom(component3, new string[1] { ModLocalization.Loc("hc_cmd_hive_empty") });
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Direct] beehive is empty - nothing to collect");
			}
		}
		else if (PrivateArea.CheckAccess(hive.transform.position))
		{
			CancelExistingActions(companionSetup2);
			component2.SetHiveOrder(hive);
			SayRandom(component3, new string[1] { ModLocalization.Loc("hc_cmd_hive") });
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Direct] 1 companion(s) → collect honey");
			}
		}
	}

	private static void DirectSmelt(IReadOnlyList<CompanionSetup> setups, string localId)
	{
		CompanionTalk companionTalk = null;
		int num = 0;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CancelExistingActions(setup);
			ZNetView component = setup.GetComponent<ZNetView>();
			if (component?.GetZDO() != null)
			{
				component.GetZDO().Set(CompanionSetup.ActionModeHash, 6);
				MHR_JobRules.OnJobSet(setup, 6);
				setup.GetComponent<SmeltController>()?.NotifyActionModeChanged();
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, SmeltLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → smelt mode");
		}
	}

	private static void DirectBoard(IReadOnlyList<CompanionSetup> setups, string localId, Ship ship, Chair pointed = null)
	{
		Chair[] componentsInChildren = ship.GetComponentsInChildren<Chair>();
		List<Chair> list = new List<Chair>();
		List<Character> allCharacters = Character.GetAllCharacters();
		Chair[] array = componentsInChildren;
		foreach (Chair chair in array)
		{
			if (!chair.m_inShip || chair.m_attachPoint == null || MHR_Board.IsMast(chair))
			{
				continue;
			}
			Vector3 position = chair.m_attachPoint.position;
			bool flag = false;
			foreach (Character item in allCharacters)
			{
				if (Vector3.Distance(item.transform.position, position) < 0.5f)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				list.Add(chair);
			}
		}
		CompanionTalk companionTalk = null;
		int num = 0;
		HashSet<Chair> hashSet = new HashSet<Chair>();
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CompanionAI component = setup.GetComponent<CompanionAI>();
			if (component == null)
			{
				continue;
			}
			if (pointed != null && !hashSet.Contains(pointed) && MHR_Board.Free(pointed))
			{
				hashSet.Add(pointed);
				if (component.IsOnShip)
				{
					component.DetachFromShip();
					if (CompanionsPlugin.LogNormal)
					{
						CompanionsPlugin.Log.LogInfo("[Board] " + setup.GetComponent<Character>()?.m_name + " getting up for the seat you pointed at");
					}
				}
				else
				{
					CancelExistingActions(setup);
				}
				component.SetPendingShipBoard(ship, pointed);
				num++;
			}
			else
			{
				if (component.IsOnShip)
				{
					continue;
				}
				CancelExistingActions(setup);
				Chair chair2 = null;
				float num2 = float.MaxValue;
				foreach (Chair item2 in list)
				{
					if (!hashSet.Contains(item2))
					{
						float num3 = Vector3.Distance(setup.transform.position, item2.m_attachPoint.position);
						if (num3 < num2)
						{
							num2 = num3;
							chair2 = item2;
						}
					}
				}
				if (chair2 != null)
				{
					hashSet.Add(chair2);
				}
				component.SetPendingShipBoard(ship, chair2);
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, BoardLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → board ship");
		}
	}

	private static void DirectTombstoneRecovery(IReadOnlyList<CompanionSetup> setups, string localId, TombStone tombstone)
	{
		CompanionSetup companionSetup = null;
		float num = float.MaxValue;
		foreach (CompanionSetup setup in setups)
		{
			if (IsOwned(setup, localId) && MHR_InScope(setup))
			{
				float num2 = Vector3.Distance(setup.transform.position, tombstone.transform.position);
				if (num2 < num)
				{
					num = num2;
					companionSetup = setup;
				}
			}
		}
		if (companionSetup == null)
		{
			return;
		}
		CancelExistingActions(companionSetup);
		CompanionAI component = companionSetup.GetComponent<CompanionAI>();
		if (!(component == null))
		{
			component.SetDirectedTombstoneRecovery(tombstone);
			SayRandom(companionSetup.GetComponent<CompanionTalk>(), TombstoneLines);
			if (CompanionsPlugin.LogDetailed)
			{
				CompanionsPlugin.Log.LogDebug("[Direct] Companion → recover tombstone");
			}
		}
	}

	private static void DirectDeposit(IReadOnlyList<CompanionSetup> setups, string localId, Container chest)
	{
		if (chest.GetInventory() == null)
		{
			return;
		}
		int num = 0;
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			Humanoid component = setup.GetComponent<Humanoid>();
			if (component == null)
			{
				continue;
			}
			Inventory inventory = component.GetInventory();
			if (inventory == null)
			{
				continue;
			}
			bool flag = false;
			foreach (ItemDrop.ItemData allItem in inventory.GetAllItems())
			{
				if (!ShouldKeep(allItem, component))
				{
					flag = true;
					break;
				}
			}
			if (companionTalk == null)
			{
				companionTalk = setup.GetComponent<CompanionTalk>();
			}
			if (flag)
			{
				CancelExistingActions(setup);
				CompanionAI component2 = setup.GetComponent<CompanionAI>();
				if (!(component2 == null))
				{
					component2.SetPendingDeposit(chest, component);
					num++;
				}
			}
		}
		if (num > 0)
		{
			SayRandom(companionTalk, DepositLines);
		}
		else
		{
			SayRandom(companionTalk, DepositEmptyLines);
		}
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → deposit");
		}
	}

	private static GameObject GetHarvestable(Collider col)
	{
		TreeBase componentInParent = col.GetComponentInParent<TreeBase>();
		if (componentInParent != null)
		{
			return componentInParent.gameObject;
		}
		TreeLog componentInParent2 = col.GetComponentInParent<TreeLog>();
		if (componentInParent2 != null)
		{
			return componentInParent2.gameObject;
		}
		MineRock5 componentInParent3 = col.GetComponentInParent<MineRock5>();
		if (componentInParent3 != null)
		{
			return componentInParent3.gameObject;
		}
		MineRock componentInParent4 = col.GetComponentInParent<MineRock>();
		if (componentInParent4 != null)
		{
			return componentInParent4.gameObject;
		}
		Destructible componentInParent5 = col.GetComponentInParent<Destructible>();
		if (componentInParent5 != null)
		{
			if (componentInParent5.m_damages.m_chop != HitData.DamageModifier.Immune && componentInParent5.gameObject.name.IndexOf("stub", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return componentInParent5.gameObject;
			}
			if (componentInParent5.m_damages.m_pickaxe != HitData.DamageModifier.Immune && componentInParent5.m_damages.m_chop == HitData.DamageModifier.Immune)
			{
				return componentInParent5.gameObject;
			}
		}
		return null;
	}

	private static void DirectGatherMode(IReadOnlyList<CompanionSetup> setups, string localId, GameObject target)
	{
		int num = HarvestController.DetermineHarvestModeStatic(target);
		if (num < 0)
		{
			return;
		}
		CompanionTalk companionTalk = null;
		int num2 = 0;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			CancelExistingActions(setup);
			ZNetView component = setup.GetComponent<ZNetView>();
			if (component?.GetZDO() != null)
			{
				component.GetZDO().Set(CompanionSetup.ActionModeHash, num);
				MHR_JobRules.OnJobSet(setup, num);
				HarvestController component2 = setup.GetComponent<HarvestController>();
				if (component2 != null && num2 == 0)
				{
					component2.SetDirectedTarget(target);
				}
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num2++;
			}
		}
		SayRandom(companionTalk, HarvestLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] {num2} companion(s) → gather mode {num}");
		}
	}

	private static void DirectGround(IReadOnlyList<CompanionSetup> setups, string localId, Vector3 point)
	{
		foreach (CompanionSetup setup in setups)
		{
			if (IsOwned(setup, localId) && MHR_InScope(setup))
			{
				HarvestController component = setup.GetComponent<HarvestController>();
				if (component != null && component.IsInGatherMode)
				{
					ExitGatherMode(setups, localId);
					return;
				}
			}
		}
		CompanionTalk companionTalk = null;
		int num = 0;
		foreach (CompanionSetup setup2 in setups)
		{
			if (!IsOwned(setup2, localId) || !MHR_InScope(setup2))
			{
				continue;
			}
			CancelExistingActions(setup2);
			CompanionAI component2 = setup2.GetComponent<CompanionAI>();
			if (!(component2 == null))
			{
				setup2.GetComponent<CompanionCombatAI>()?.HoldForOrder("Ordered to move");
				component2.SetMoveTarget(point);
				if (companionTalk == null)
				{
					companionTalk = setup2.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, MoveLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) → move to {point:F1}");
		}
		if (num == 0)
		{
			MHR_WhyNotCommanded(setups, localId);
		}
	}

	private static void CancelAll(IReadOnlyList<CompanionSetup> setups, string localId)
	{
		CompanionTalk companionTalk = null;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			MHR_Owner.TakeControl(setup);
			if (companionTalk == null)
			{
				companionTalk = setup.GetComponent<CompanionTalk>();
			}
			CompanionAI component = setup.GetComponent<CompanionAI>();
			if (component != null)
			{
				component.CancelPendingCart();
				component.CancelMoveTarget();
				component.CancelPendingDeposit();
				component.CancelPendingShipBoard();
				component.CancelHiveOrder();
				component.CancelTombstoneRecovery();
				component.ClearTargets();
				component.StopMoving();
				if (component.IsOnShip)
				{
					component.DetachFromShip();
				}
				if (Player.m_localPlayer != null && setup.GetFollow())
				{
					component.SetFollowTarget(Player.m_localPlayer.gameObject);
				}
				else if (!setup.GetFollow())
				{
					component.SetFollowTarget(null);
				}
			}
			ZNetView component2 = setup.GetComponent<ZNetView>();
			if (component2?.GetZDO() != null && component2.GetZDO().GetInt(CompanionSetup.ActionModeHash) != 0)
			{
				component2.GetZDO().Set(CompanionSetup.ActionModeHash, 0);
				MHR_JobRules.OnJobSet(setup, 0);
			}
			setup.GetComponent<DoorHandler>()?.Cancel();
			setup.GetComponent<HarvestController>()?.NotifyActionModeChanged();
			CompanionRest component3 = setup.GetComponent<CompanionRest>();
			if (component3 != null)
			{
				component3.CancelDirected();
			}
			RepairController component4 = setup.GetComponent<RepairController>();
			if (component4 != null && component4.IsActive)
			{
				component4.CancelDirected();
			}
			SmeltController component5 = setup.GetComponent<SmeltController>();
			if (component5 != null)
			{
				component5.NotifyActionModeChanged();
			}
			setup.GetComponent<UpkeepController>()?.NotifyActionModeChanged();
		}
		SayRandom(companionTalk, CancelLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Direct] Cancelled all directed commands");
		}
	}

	private static bool IsOwned(CompanionSetup setup, string localId)
	{
		ZNetView component = setup.GetComponent<ZNetView>();
		if (component == null || component.GetZDO() == null)
		{
			return false;
		}
		return component.GetZDO().GetString(CompanionSetup.OwnerHash) == localId;
	}

	private static void MHR_WhyNotCommanded(IReadOnlyList<CompanionSetup> setups, string localId)
	{
		Player localPlayer = Player.m_localPlayer;
		foreach (CompanionSetup setup in setups)
		{
			if (setup == null)
			{
				continue;
			}
			string text = setup.GetComponent<Character>()?.m_name ?? "?";
			string text2 = null;
			string text3;
			if (!IsOwned(setup, localId))
			{
				text3 = "not yours";
			}
			else if (!setup.GetIsCommandable())
			{
				text3 = "Command is switched off for it on the wheel";
				text2 = ModLocalization.LocFmt("hc_cmd_refused_off", text);
			}
			else if (localPlayer != null && ModConfig.RecallRange != null && ModConfig.RecallRange.Value > 0f)
			{
				float num = Vector3.Distance(setup.transform.position, localPlayer.transform.position);
				text3 = $"it's {num:F0} m away (RecallRange is {ModConfig.RecallRange.Value:F0} m - hold the command key {ModConfig.SuperRecallHoldTime?.Value ?? 2f:F0}s to call everyone)";
				text2 = ModLocalization.LocFmt("hc_cmd_refused_far", text, num.ToString("F0"), (ModConfig.SuperRecallHoldTime?.Value ?? 2f).ToString("F0"));
			}
			else
			{
				text3 = "unknown";
			}
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Direct] \"" + text + "\" didn't take the command: " + text3);
			}
			if (text2 != null)
			{
				ConfigEntry<bool> speechFarMessages = ModConfig.SpeechFarMessages;
				if ((speechFarMessages == null || speechFarMessages.Value) && MessageHud.instance != null)
				{
					MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, text2);
				}
			}
		}
	}

	internal static bool MHR_InScope(CompanionSetup s)
	{
		bool flag = MHR_InScopeCheck(s);
		if (flag)
		{
			MHR_Owner.TakeControl(s);
		}
		return flag;
	}

	private static bool MHR_InScopeCheck(CompanionSetup s)
	{
		if (_mhrSuperRecall)
		{
			return true;
		}
		if (!s.GetIsCommandable())
		{
			return false;
		}
		if (ModConfig.RecallRange != null)
		{
			float value = ModConfig.RecallRange.Value;
			if (value > 0f)
			{
				Player localPlayer = Player.m_localPlayer;
				if (!(localPlayer == null))
				{
					return Vector3.Distance(s.transform.position, localPlayer.transform.position) <= value;
				}
			}
		}
		return true;
	}

	private static void SayRandom(CompanionTalk talk, string[] pool)
	{
		if (!(talk == null) && pool != null && pool.Length != 0)
		{
			talk.SayOrder(pool[UnityEngine.Random.Range(0, pool.Length)]);
		}
	}

	private static void CancelExistingActions(CompanionSetup setup, bool cancelRest = true)
	{
		MHR_Owner.TakeControl(setup);
		if (cancelRest)
		{
			CompanionRest component = setup.GetComponent<CompanionRest>();
			if (component != null && (component.IsResting || component.IsNavigating))
			{
				component.CancelDirected();
			}
		}
		CompanionAI component2 = setup.GetComponent<CompanionAI>();
		if (component2 != null)
		{
			component2.CancelPendingCart();
			component2.CancelMoveTarget();
			component2.CancelPendingDeposit();
			component2.CancelPendingShipBoard();
			component2.CancelHiveOrder();
			component2.CancelTombstoneRecovery();
			if (component2.IsOnShip)
			{
				component2.DetachFromShip();
			}
		}
		setup.GetComponent<HarvestController>()?.CancelDirectedTarget();
		setup.GetComponent<DoorHandler>()?.Cancel();
		RepairController component3 = setup.GetComponent<RepairController>();
		if (component3 != null && component3.IsActive)
		{
			component3.CancelDirected();
		}
		SmeltController component4 = setup.GetComponent<SmeltController>();
		if (component4 != null && component4.IsActive)
		{
			component4.CancelDirected();
		}
		setup.GetComponent<UpkeepController>()?.NotifyActionModeChanged();
	}

	private static void ExitGatherMode(IReadOnlyList<CompanionSetup> setups, string localId)
	{
		CompanionTalk companionTalk = null;
		int num = 0;
		foreach (CompanionSetup setup in setups)
		{
			if (!IsOwned(setup, localId) || !MHR_InScope(setup))
			{
				continue;
			}
			ZNetView component = setup.GetComponent<ZNetView>();
			if (component?.GetZDO() != null)
			{
				component.GetZDO().Set(CompanionSetup.ActionModeHash, 0);
				MHR_JobRules.OnJobSet(setup, 0);
				if (companionTalk == null)
				{
					companionTalk = setup.GetComponent<CompanionTalk>();
				}
				num++;
			}
		}
		SayRandom(companionTalk, CancelLines);
		if (CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug($"[Direct] {num} companion(s) exited gather mode → follow");
		}
	}

	private static void ResetGamepadHold()
	{
		_gpHoldActive = false;
		_gpHoldTarget = null;
	}

	internal static void ClearPendingTap()
	{
		if (_pendingTapContainer != null && CompanionsPlugin.LogDetailed)
		{
			CompanionsPlugin.Log.LogDebug("[Interact] ClearPendingTap called while pending");
		}
		_pendingTapContainer = null;
		_pendingTapPlayer = null;
		_loggedPendingStart = false;
		_gamepadReleaseDetected = false;
	}

	internal static void OpenCompanionInventory(CompanionSetup setup, Container container, Player player)
	{
		if (InventoryGui.instance == null)
		{
			return;
		}
		ZNetView component = container.GetComponent<ZNetView>();
		if (component != null && !component.IsOwner())
		{
			component.ClaimOwnership();
			if (CompanionsPlugin.LogNormal)
			{
				CompanionsPlugin.Log.LogInfo("[Companion] Requesting container access - interact again in a moment");
			}
		}
		else
		{
			InventoryGui.instance.Show(container);
			CompanionInteractPanel.EnsureInstance();
			CompanionInteractPanel.Instance?.Show(setup);
		}
	}

	internal string GetHoverText()
	{
		string text = GetCompanionName();
		if (ModConfig.ShowStatus == null || ModConfig.ShowStatus.Value)
		{
			string text2 = CompanionStatus.Describe(GetComponent<CompanionSetup>());
			if (!string.IsNullOrEmpty(text2))
			{
				text = text + "\n<color=#c8c8c8>" + text2 + "</color>";
			}
		}
		string text3 = ModLocalization.Loc("hc_hover_inventory");
		string text4 = ModLocalization.Loc("hc_hover_commands");
		string text5 = ((!ZInput.IsGamepadActive()) ? ZInput.instance.GetBoundKeyString("Use") : ZInput.instance.GetBoundKeyString("JoyUse"));
		if (ZInput.IsGamepadActive() && !MHR_Pad.Plain(ModConfig.PadWheel, ModConfig.PadWheelModifier, "Use"))
		{
			string text6 = MHR_Pad.Show(ModConfig.PadWheel, ModConfig.PadWheelModifier);
			return text + "\n[<color=yellow><b>" + text5 + "</b></color>] " + text3 + ((text6 != null) ? ("\n[<color=yellow><b>" + text6 + "</b></color>] " + text4) : "");
		}
		if (IsRadialKeyUse)
		{
			return text + "\n[<color=yellow><b>" + text5 + "</b></color>] " + text3 + "\n[<color=yellow>" + ModLocalization.Loc("hc_hover_hold") + " <b>" + text5 + "</b></color>] " + text4;
		}
		string text7 = ((!ZInput.IsGamepadActive()) ? ModConfig.RadialMenuKey.Value.ToString() : text5);
		return text + "\n[<color=yellow><b>" + text5 + "</b></color>] " + text3 + "\n[<color=yellow><b>" + text7 + "</b></color>] " + text4;
	}

	internal string GetCompanionName()
	{
		if (_nview != null && _nview.GetZDO() != null)
		{
			string text = _nview.GetZDO().GetString(CompanionSetup.NameHash);
			if (!string.IsNullOrEmpty(text))
			{
				return text;
			}
		}
		if (!(_character != null))
		{
			return ModLocalization.Loc("hc_msg_name_default");
		}
		return _character.m_name;
	}

	internal string GetHoverName()
	{
		return GetCompanionName();
	}
}
