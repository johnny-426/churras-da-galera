using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CdG.Models;

namespace CdG.Services;

public static class VersaoEvento
{
    public static string Obter(Evento evento)
    {
        var dados = JsonSerializer.Serialize(new {
            evento.Id, evento.Nome, evento.Descricao, evento.Local,
            evento.LimiteParticipantes, evento.Status,
            Data = evento.DataHora?.Ticks, Atualizacao = evento.AtualizadoEm.Ticks
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dados)));
    }
}
