using UnityEngine;

namespace Companions;

// Valheim solo simula las zonas de 64 m que rodean al jugador (3x3). Según dónde esté el jugador dentro de su zona, el
// borde queda a entre 64 y 128 m: más allá un compañero se queda congelado hasta que el jugador se acerca.
public static class MHR_Loaded
{
	public static bool Safe(Vector3 point, float margin)
	{
		try
		{
			if (ZNetScene.instance == null || ZNet.instance == null)
			{
				return true;
			}
			if (ZNetScene.instance.OutsideActiveArea(point))
			{
				return false;
			}
			if (margin <= 0f)
			{
				return true;
			}
			return !ZNetScene.instance.OutsideActiveArea(point + new Vector3(margin, 0f, 0f)) && !ZNetScene.instance.OutsideActiveArea(point + new Vector3(0f - margin, 0f, 0f)) && !ZNetScene.instance.OutsideActiveArea(point + new Vector3(0f, 0f, margin)) && !ZNetScene.instance.OutsideActiveArea(point + new Vector3(0f, 0f, 0f - margin));
		}
		catch
		{
			return true;
		}
	}
}
