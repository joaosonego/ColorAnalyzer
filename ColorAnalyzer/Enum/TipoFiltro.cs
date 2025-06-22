using System.ComponentModel;

namespace ColorAnalyzer.Enum
{
    public enum TipoFiltro
    {
        [Description("Tons de cinza")]
        Grayscale,

        [Description("Sépia")]
        Sepia,

        [Description("Negativo")]
        Negative
    }
}
