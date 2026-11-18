namespace MoveSync.Domain.Comum;

/// <summary>
/// Cláusulas de guarda reutilizadas pelas entidades para proteger suas invariantes.
/// Mesmo que a API já valide a entrada, o domínio nunca confia no chamador.
/// </summary>
public static class Guarda
{
    public static string Texto(string? valor, string campo, int minimo, int maximo)
    {
        var texto = valor?.Trim() ?? string.Empty;
        if (texto.Length < minimo || texto.Length > maximo)
        {
            throw new RegraNegocioException(
                "CAMPO_INVALIDO",
                $"O campo '{campo}' deve ter entre {minimo} e {maximo} caracteres.");
        }

        return texto;
    }

    public static int Intervalo(int valor, string campo, int minimo, int maximo)
    {
        if (valor < minimo || valor > maximo)
        {
            throw new RegraNegocioException(
                "CAMPO_INVALIDO",
                $"O campo '{campo}' deve estar entre {minimo} e {maximo}.");
        }

        return valor;
    }

    public static double Intervalo(double valor, string campo, double minimo, double maximo)
    {
        if (double.IsNaN(valor) || valor < minimo || valor > maximo)
        {
            throw new RegraNegocioException(
                "CAMPO_INVALIDO",
                $"O campo '{campo}' deve estar entre {minimo} e {maximo}.");
        }

        return valor;
    }

    public static DateTime Utc(DateTime valor) =>
        valor.Kind switch
        {
            DateTimeKind.Utc => valor,
            DateTimeKind.Local => valor.ToUniversalTime(),
            _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc)
        };
}
