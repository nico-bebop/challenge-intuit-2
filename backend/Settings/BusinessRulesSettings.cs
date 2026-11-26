namespace ConsultasPlan.Settings;

/// <summary>Reglas de negocio parametrizables (sección "BusinessRules" de appsettings.json).</summary>
public class BusinessRulesSettings
{
    public const string SectionName = "BusinessRules";

    /// <summary>Anticipación mínima (horas) para poder cancelar una consulta.</summary>
    public int CancellationWindowHours { get; set; } = 24;

    /// <summary>Consultas presenciales de nutrición incluidas en el plan por año calendario.</summary>
    public int NutricionAnnualLimit { get; set; } = 6;

    /// <summary>Máximo de consultas presenciales de nutrición por mes calendario.</summary>
    public int NutricionMonthlyLimit { get; set; } = 1;
}
