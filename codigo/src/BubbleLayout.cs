using System;

namespace OfflineCompanionsAI
{
    /// <summary>Cuánto subir la burbuja de un compañero para que no tape su nombre.</summary>
    public static class BubbleLayout
    {
        /// <summary>Si no cabe entera sobre el nombre, se prefiere que no se salga de la pantalla, porque el texto empieza arriba.</summary>
        public static float Lift(float textBottom, float textTop, float labelTop, float gap, float screenTop)
        {
            float needed = labelTop + gap - textBottom;
            if (needed <= 0f) return 0f;
            float room = screenTop - textTop;
            return Math.Max(0f, Math.Min(needed, room));
        }
    }
}
