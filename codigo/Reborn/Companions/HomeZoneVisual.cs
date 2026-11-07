using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Companions;

public class HomeZoneVisual : MonoBehaviour
{
	private LineRenderer _lr;

	private Material _ringMaterial;

	private const int Points = 64;

	private const float Width = 0.08f;

	private static readonly Color RingColor = new Color(1f, 0.9f, 0.2f, 0.8f);

	private void Awake()
	{
		_lr = gameObject.AddComponent<LineRenderer>();
		_lr.useWorldSpace = true;
		_lr.loop = true;
		_lr.positionCount = 64;
		_lr.startWidth = 0.08f;
		_lr.endWidth = 0.08f;
		_lr.startColor = RingColor;
		_lr.endColor = RingColor;
		_lr.shadowCastingMode = ShadowCastingMode.Off;
		_lr.receiveShadows = false;
		_lr.enabled = false;
		_ringMaterial = new Material(Shader.Find("Sprites/Default"));
		_lr.material = _ringMaterial;
	}

	private void OnDestroy()
	{
		if (_ringMaterial != null)
		{
			UnityEngine.Object.Destroy(_ringMaterial);
		}
	}

	public void Show(Vector3 center, float radius)
	{
		if (!(_lr == null))
		{
			float y = center.y + 0.12f;
			for (int i = 0; i < 64; i++)
			{
				float f = (float)i * 5.625f * (Mathf.PI / 180f);
				_lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(f) * radius, y, center.z + Mathf.Sin(f) * radius));
			}
			_lr.enabled = true;
		}
	}

	public void Hide()
	{
		if (_lr != null)
		{
			_lr.enabled = false;
		}
	}
}
