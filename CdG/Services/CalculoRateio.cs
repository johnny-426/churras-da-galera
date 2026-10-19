namespace CdG.Services;
public static class CalculoRateio
{
    // Ordem estável distribui os centavos restantes sem perder dinheiro.
    public static Dictionary<string, long> Dividir(long total, IEnumerable<string> usuarios)
    {
        var ids = usuarios.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (total < 0 || ids.Length == 0) throw new ArgumentException("Rateio inválido.");
        return ids.Select((id, i) => new { id, valor = total / ids.Length + (i < total % ids.Length ? 1 : 0) })
            .ToDictionary(x => x.id, x => x.valor);
    }
}
