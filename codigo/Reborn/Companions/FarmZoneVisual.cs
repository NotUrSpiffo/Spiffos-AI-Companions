using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Companions;

public class FarmZoneVisual : MonoBehaviour
{
	private readonly List<LineRenderer> _borders = new List<LineRenderer>();

	private Material _borderMaterial;

	private const float Width = 0.06f;

	private static readonly Color ZoneColor = new Color(0.3f, 0.85f, 0.3f, 0.7f);

	private void Awake()
	{
		_borderMaterial = new Material(Shader.Find("Sprites/Default"));
	}

	private void OnDestroy()
	{
		if (_borderMaterial != null)
		{
			Object.Destroy(_borderMaterial);
		}
	}

	private LineRenderer CreateBorder(int index)
	{
		GameObject gameObject = new GameObject($"FarmZoneBorder_{index}");
		gameObject.transform.SetParent(transform, worldPositionStays: false);
		LineRenderer lineRenderer = gameObject.AddComponent<LineRenderer>();
		lineRenderer.useWorldSpace = true;
		lineRenderer.loop = true;
		lineRenderer.positionCount = 4;
		lineRenderer.startWidth = 0.06f;
		lineRenderer.endWidth = 0.06f;
		lineRenderer.material = _borderMaterial;
		lineRenderer.startColor = ZoneColor;
		lineRenderer.endColor = ZoneColor;
		lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
		lineRenderer.receiveShadows = false;
		lineRenderer.enabled = false;
		return lineRenderer;
	}

	private void EnsureCapacity(int count)
	{
		while (_borders.Count < count)
		{
			_borders.Add(CreateBorder(_borders.Count));
		}
	}

	internal void ShowZones(List<FarmZone> zones)
	{
		int count = zones?.Count ?? 0;
		EnsureCapacity(count);
		for (int i = 0; i < _borders.Count; i++)
		{
			if (zones != null && i < zones.Count && zones[i].IsValid)
			{
				FarmZone farmZone = zones[i];
				float y = farmZone.Center.y + 0.12f;
				farmZone.GetCorners(out var c, out var c2, out var c3, out var c4);
				c.y = y;
				c2.y = y;
				c3.y = y;
				c4.y = y;
				_borders[i].SetPosition(0, c);
				_borders[i].SetPosition(1, c2);
				_borders[i].SetPosition(2, c3);
				_borders[i].SetPosition(3, c4);
				_borders[i].enabled = true;
			}
			else
			{
				_borders[i].enabled = false;
			}
		}
	}

	internal void HideAll()
	{
		for (int i = 0; i < _borders.Count; i++)
		{
			if (_borders[i] != null)
			{
				_borders[i].enabled = false;
			}
		}
	}
}
