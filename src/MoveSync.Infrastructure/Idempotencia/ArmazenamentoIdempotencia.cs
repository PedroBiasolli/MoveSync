using Microsoft.EntityFrameworkCore;
using MoveSync.Infrastructure.Persistencia;

namespace MoveSync.Infrastructure.Idempotencia;

public enum SituacaoReserva
{
    /// <summary>Primeira vez que a chave aparece: pode executar a operação.</summary>
    Reservada,

    /// <summary>Chave já concluída com o mesmo conteúdo: devolver a resposta gravada.</summary>
    Repetida,

    /// <summary>A primeira requisição com esta chave ainda está em andamento.</summary>
    EmProcessamento,

    /// <summary>Mesma chave reutilizada com outro conteúdo: erro do cliente.</summary>
    ConteudoDiferente
}

public sealed record ReservaIdempotencia(SituacaoReserva Situacao, RegistroIdempotencia? Registro);

public interface IArmazenamentoIdempotencia
{
    Task<ReservaIdempotencia> ReservarAsync(string escopo, string chave, string hashRequisicao, CancellationToken ct);

    Task ConcluirAsync(Guid registroId, int statusCode, string? contentType, string? location, string? corpo, CancellationToken ct);

    Task LiberarAsync(Guid registroId, CancellationToken ct);

    Task<int> RemoverExpiradosAsync(CancellationToken ct);
}

/// <summary>
/// Persiste as chaves de idempotência no banco (sobrevive a reinícios e funciona com várias instâncias da API).
/// Usa um DbContext próprio (via factory), independente do DbContext da requisição.
/// </summary>
public sealed class ArmazenamentoIdempotenciaEf(
    IDbContextFactory<MoveSyncDbContext> fabrica,
    TimeProvider relogio) : IArmazenamentoIdempotencia
{
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    /// <summary>Se uma requisição "em processamento" travar (ex.: queda do servidor), a chave é liberada após este tempo.</summary>
    public static readonly TimeSpan TempoMaximoProcessamento = TimeSpan.FromMinutes(2);

    public async Task<ReservaIdempotencia> ReservarAsync(string escopo, string chave, string hashRequisicao, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        await using var db = await fabrica.CreateDbContextAsync(ct);

        var existente = await db.RegistrosIdempotencia.FirstOrDefaultAsync(r => r.Escopo == escopo && r.Chave == chave, ct);
        if (existente is not null)
        {
            var abandonado = !existente.Concluido && agora - existente.CriadoEm > TempoMaximoProcessamento;
            if (existente.ExpiraEm > agora && !abandonado)
            {
                if (!string.Equals(existente.HashRequisicao, hashRequisicao, StringComparison.Ordinal))
                {
                    return new ReservaIdempotencia(SituacaoReserva.ConteudoDiferente, existente);
                }

                return existente.Concluido
                    ? new ReservaIdempotencia(SituacaoReserva.Repetida, existente)
                    : new ReservaIdempotencia(SituacaoReserva.EmProcessamento, existente);
            }

            db.RegistrosIdempotencia.Remove(existente);
            await db.SaveChangesAsync(ct);
        }

        var registro = new RegistroIdempotencia
        {
            Escopo = escopo,
            Chave = chave,
            HashRequisicao = hashRequisicao,
            Concluido = false,
            CriadoEm = agora,
            ExpiraEm = agora + Validade
        };
        db.RegistrosIdempotencia.Add(registro);

        try
        {
            await db.SaveChangesAsync(ct);
            return new ReservaIdempotencia(SituacaoReserva.Reservada, registro);
        }
        catch (DbUpdateException ex) when (MoveSyncDbContext.EhViolacaoDeUnicidade(ex))
        {
            // Duas requisições idênticas chegaram ao mesmo tempo: a outra venceu a corrida.
            return new ReservaIdempotencia(SituacaoReserva.EmProcessamento, null);
        }
    }

    public async Task ConcluirAsync(Guid registroId, int statusCode, string? contentType, string? location, string? corpo, CancellationToken ct)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var registro = await db.RegistrosIdempotencia.FirstOrDefaultAsync(r => r.Id == registroId, ct);
        if (registro is null)
        {
            return;
        }

        registro.Concluido = true;
        registro.StatusCode = statusCode;
        registro.ContentType = contentType;
        registro.Location = location;
        registro.CorpoResposta = corpo;
        await db.SaveChangesAsync(ct);
    }

    public async Task LiberarAsync(Guid registroId, CancellationToken ct)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var registro = await db.RegistrosIdempotencia.FirstOrDefaultAsync(r => r.Id == registroId, ct);
        if (registro is not null)
        {
            db.RegistrosIdempotencia.Remove(registro);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> RemoverExpiradosAsync(CancellationToken ct)
    {
        var agora = relogio.GetUtcNow().UtcDateTime;
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await db.RegistrosIdempotencia.Where(r => r.ExpiraEm <= agora).ExecuteDeleteAsync(ct);
    }
}
