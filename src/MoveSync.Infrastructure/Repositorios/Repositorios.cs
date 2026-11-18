using Microsoft.EntityFrameworkCore;
using MoveSync.Application.Abstracoes;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;
using MoveSync.Infrastructure.Persistencia;

namespace MoveSync.Infrastructure.Repositorios;

// Implementações dos repositórios com EF Core.
// Consultas de leitura usam AsNoTracking (mais rápidas); as usadas para alteração ficam rastreadas.

internal sealed class UsuarioRepository(MoveSyncDbContext db) : IUsuarioRepository
{
    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<Usuario?> ObterPorEmailAsync(string emailNormalizado, CancellationToken ct) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Email == emailNormalizado, ct);

    public Task<bool> EmailExisteAsync(string emailNormalizado, CancellationToken ct) =>
        db.Usuarios.AnyAsync(u => u.Email == emailNormalizado, ct);

    public async Task AdicionarAsync(Usuario usuario, CancellationToken ct) =>
        await db.Usuarios.AddAsync(usuario, ct);
}

internal sealed class CruzamentoRepository(MoveSyncDbContext db) : ICruzamentoRepository
{
    public Task<Cruzamento?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        db.Cruzamentos.Include(c => c.Vias).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Pagina<Cruzamento>> ListarAsync(string? busca, bool? ativo, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var consulta = db.Cruzamentos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var termo = busca.ToUpperInvariant();
            consulta = consulta.Where(c =>
                c.Nome.ToUpper().Contains(termo) ||
                c.Codigo.ToUpper().Contains(termo) ||
                c.Endereco.ToUpper().Contains(termo));
        }

        if (ativo is not null)
        {
            consulta = consulta.Where(c => c.Ativo == ativo.Value);
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderBy(c => c.Nome)
            .ThenBy(c => c.Id)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Include(c => c.Vias)
            .ToListAsync(ct);
        return new Pagina<Cruzamento>(itens, total);
    }

    public async Task<IReadOnlyList<Cruzamento>> ListarAtivosAsync(CancellationToken ct) =>
        await db.Cruzamentos.AsNoTracking().Include(c => c.Vias).Where(c => c.Ativo).OrderBy(c => c.Codigo).ToListAsync(ct);

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken ct) =>
        db.Cruzamentos.AnyAsync(c => c.Codigo == codigo, ct);

    public Task<int> ContarAsync(bool? ativo, CancellationToken ct) =>
        ativo is null
            ? db.Cruzamentos.CountAsync(ct)
            : db.Cruzamentos.CountAsync(c => c.Ativo == ativo.Value, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> ObterNomesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.Cruzamentos.AsNoTracking()
            .Where(c => lista.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome, ct);
    }

    public async Task AdicionarAsync(Cruzamento cruzamento, CancellationToken ct) =>
        await db.Cruzamentos.AddAsync(cruzamento, ct);

    public void Remover(Cruzamento cruzamento) => db.Cruzamentos.Remove(cruzamento);
}

internal sealed class SensorRepository(MoveSyncDbContext db) : ISensorRepository
{
    public Task<Sensor?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        db.Sensores.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Sensor?> ObterPorCodigoAsync(string codigo, CancellationToken ct) =>
        db.Sensores.FirstOrDefaultAsync(s => s.Codigo == codigo, ct);

    public async Task<IReadOnlyList<Sensor>> ListarAsync(Guid? cruzamentoId, CancellationToken ct)
    {
        var consulta = db.Sensores.AsNoTracking();
        if (cruzamentoId is not null)
        {
            consulta = consulta.Where(s => s.CruzamentoId == cruzamentoId.Value);
        }

        return await consulta.OrderBy(s => s.Codigo).ToListAsync(ct);
    }

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken ct) =>
        db.Sensores.AnyAsync(s => s.Codigo == codigo, ct);

    public Task<bool> ExisteParaViaAsync(Guid viaId, CancellationToken ct) =>
        db.Sensores.AnyAsync(s => s.ViaId == viaId, ct);

    public async Task AdicionarAsync(Sensor sensor, CancellationToken ct) =>
        await db.Sensores.AddAsync(sensor, ct);

    public void Remover(Sensor sensor) => db.Sensores.Remove(sensor);
}

internal sealed class LeituraRepository(MoveSyncDbContext db) : ILeituraRepository
{
    public Task<LeituraSensor?> ObterPorChaveExternaAsync(Guid sensorId, string leituraExternaId, CancellationToken ct) =>
        db.Leituras.AsNoTracking().FirstOrDefaultAsync(l => l.SensorId == sensorId && l.LeituraExternaId == leituraExternaId, ct);

    public async Task<IReadOnlySet<string>> ObterChavesExistentesAsync(Guid sensorId, IReadOnlyCollection<string> chaves, CancellationToken ct)
    {
        if (chaves.Count == 0)
        {
            return new HashSet<string>();
        }

        var lista = chaves.ToList();
        var existentes = await db.Leituras.AsNoTracking()
            .Where(l => l.SensorId == sensorId && lista.Contains(l.LeituraExternaId))
            .Select(l => l.LeituraExternaId)
            .ToListAsync(ct);
        return existentes.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<LeituraSensor>> ListarRecentesPorCruzamentoAsync(Guid cruzamentoId, DateTime desdeUtc, CancellationToken ct) =>
        await db.Leituras.AsNoTracking()
            .Where(l => l.CruzamentoId == cruzamentoId && l.ColetadaEm >= desdeUtc)
            .ToListAsync(ct);

    public async Task<Pagina<LeituraSensor>> ListarPorViaAsync(Guid viaId, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var consulta = db.Leituras.AsNoTracking().Where(l => l.ViaId == viaId);
        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderByDescending(l => l.ColetadaEm)
            .ThenBy(l => l.Id)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
        return new Pagina<LeituraSensor>(itens, total);
    }

    public Task<int> ContarDesdeAsync(DateTime desdeUtc, CancellationToken ct) =>
        db.Leituras.CountAsync(l => l.ColetadaEm >= desdeUtc, ct);

    public async Task AdicionarAsync(LeituraSensor leitura, CancellationToken ct) =>
        await db.Leituras.AddAsync(leitura, ct);
}

internal sealed class PlanoRepository(MoveSyncDbContext db) : IPlanoRepository
{
    public Task<PlanoSemaforico?> ObterVigenteAsync(Guid cruzamentoId, CancellationToken ct) =>
        db.Planos
            .Include(p => p.Fases)
            .Where(p => p.CruzamentoId == cruzamentoId && p.Vigente)
            .OrderByDescending(p => p.GeradoEm)
            .FirstOrDefaultAsync(ct);

    public async Task<Pagina<PlanoSemaforico>> ListarHistoricoAsync(Guid cruzamentoId, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var consulta = db.Planos.AsNoTracking().Where(p => p.CruzamentoId == cruzamentoId);
        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderByDescending(p => p.GeradoEm)
            .ThenBy(p => p.Id)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .Include(p => p.Fases)
            .ToListAsync(ct);
        return new Pagina<PlanoSemaforico>(itens, total);
    }

    public async Task<IReadOnlyDictionary<ModoPlano, int>> ContarVigentesPorModoAsync(CancellationToken ct)
    {
        var grupos = await db.Planos.AsNoTracking()
            .Where(p => p.Vigente)
            .GroupBy(p => p.Modo)
            .Select(g => new { Modo = g.Key, Total = g.Count() })
            .ToListAsync(ct);
        return grupos.ToDictionary(g => g.Modo, g => g.Total);
    }

    public async Task AdicionarAsync(PlanoSemaforico plano, CancellationToken ct) =>
        await db.Planos.AddAsync(plano, ct);
}

internal sealed class OcorrenciaRepository(MoveSyncDbContext db) : IOcorrenciaRepository
{
    public Task<Ocorrencia?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        db.Ocorrencias.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<Pagina<Ocorrencia>> ListarAsync(FiltroOcorrencias filtro, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var consulta = db.Ocorrencias.AsNoTracking();
        if (filtro.AutorId is { } autor)
        {
            consulta = consulta.Where(o => o.AutorId == autor);
        }

        if (filtro.CruzamentoId is { } cruzamento)
        {
            consulta = consulta.Where(o => o.CruzamentoId == cruzamento);
        }

        if (filtro.Status is { } status)
        {
            consulta = consulta.Where(o => o.Status == status);
        }

        if (filtro.Prioridade is { } prioridade)
        {
            consulta = consulta.Where(o => o.Prioridade == prioridade);
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderByDescending(o => o.CriadaEm)
            .ThenBy(o => o.Id)
            .Skip((pagina - 1) * tamanhoPagina)
            .Take(tamanhoPagina)
            .ToListAsync(ct);
        return new Pagina<Ocorrencia>(itens, total);
    }

    public Task<bool> ExisteParaCruzamentoAsync(Guid cruzamentoId, CancellationToken ct) =>
        db.Ocorrencias.AnyAsync(o => o.CruzamentoId == cruzamentoId, ct);

    public async Task<IReadOnlyDictionary<PrioridadeOcorrencia, int>> ContarPendentesPorPrioridadeAsync(CancellationToken ct)
    {
        var grupos = await db.Ocorrencias.AsNoTracking()
            .Where(o => o.Status == StatusOcorrencia.Aberta || o.Status == StatusOcorrencia.EmAnalise)
            .GroupBy(o => o.Prioridade)
            .Select(g => new { Prioridade = g.Key, Total = g.Count() })
            .ToListAsync(ct);
        return grupos.ToDictionary(g => g.Prioridade, g => g.Total);
    }

    public async Task AdicionarAsync(Ocorrencia ocorrencia, CancellationToken ct) =>
        await db.Ocorrencias.AddAsync(ocorrencia, ct);

    public void Remover(Ocorrencia ocorrencia) => db.Ocorrencias.Remove(ocorrencia);
}

internal sealed class SolicitacaoTravessiaRepository(MoveSyncDbContext db) : ISolicitacaoTravessiaRepository
{
    public Task<SolicitacaoTravessia?> ObterVigenteDoUsuarioAsync(Guid cruzamentoId, Guid usuarioId, DateTime agoraUtc, CancellationToken ct) =>
        db.SolicitacoesTravessia.AsNoTracking()
            .Where(s => s.CruzamentoId == cruzamentoId && s.UsuarioId == usuarioId && s.ExpiraEm > agoraUtc)
            .OrderByDescending(s => s.SolicitadaEm)
            .FirstOrDefaultAsync(ct);

    public Task<bool> ExisteVigenteAsync(Guid cruzamentoId, DateTime agoraUtc, CancellationToken ct) =>
        db.SolicitacoesTravessia.AnyAsync(s => s.CruzamentoId == cruzamentoId && s.ExpiraEm > agoraUtc, ct);

    public async Task AdicionarAsync(SolicitacaoTravessia solicitacao, CancellationToken ct) =>
        await db.SolicitacoesTravessia.AddAsync(solicitacao, ct);
}

internal sealed class EmergenciaRepository(MoveSyncDbContext db) : IEmergenciaRepository
{
    public Task<AcionamentoEmergencia?> ObterPorIdAsync(Guid id, CancellationToken ct) =>
        db.Emergencias.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<AcionamentoEmergencia?> ObterAtivaAsync(Guid cruzamentoId, DateTime agoraUtc, CancellationToken ct) =>
        db.Emergencias
            .Where(a => a.CruzamentoId == cruzamentoId && a.EncerradoEm == null && a.ExpiraEm > agoraUtc)
            .OrderByDescending(a => a.AcionadoEm)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AcionamentoEmergencia>> ListarAtivasAsync(DateTime agoraUtc, CancellationToken ct) =>
        await db.Emergencias.AsNoTracking()
            .Where(a => a.EncerradoEm == null && a.ExpiraEm > agoraUtc)
            .OrderByDescending(a => a.AcionadoEm)
            .ToListAsync(ct);

    public async Task AdicionarAsync(AcionamentoEmergencia acionamento, CancellationToken ct) =>
        await db.Emergencias.AddAsync(acionamento, ct);
}
