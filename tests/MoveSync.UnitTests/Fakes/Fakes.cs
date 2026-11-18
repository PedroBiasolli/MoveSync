using MoveSync.Application.Abstracoes;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;

namespace MoveSync.UnitTests.Fakes;

// Implementações em memória das abstrações. Permitem testar os casos de uso
// (camada Application) de forma rápida e determinística, sem banco de dados.

public sealed class RelogioFixo(DateTime agoraUtc) : TimeProvider
{
    public DateTime Agora { get; private set; } = DateTime.SpecifyKind(agoraUtc, DateTimeKind.Utc);

    public void Avancar(TimeSpan tempo) => Agora += tempo;

    public override DateTimeOffset GetUtcNow() => new(Agora);
}

public sealed class HashFalso : IHashSenha
{
    public string Gerar(string senha) => "hash:" + senha;

    public bool Verificar(string hash, string senha) => hash == "hash:" + senha;
}

public sealed class GeradorTokenFalso : IGeradorToken
{
    public TokenGerado GerarParaUsuario(Usuario usuario) => new($"token-{usuario.Perfil}-{usuario.Id}", DateTime.UtcNow.AddHours(2));

    public TokenGerado GerarParaSensor(Sensor sensor) => new($"token-sensor-{sensor.Id}", DateTime.UtcNow.AddHours(24));
}

public sealed class NotificadorFalso : INotificadorNucleo
{
    public List<EventoIntegracao> Eventos { get; } = [];

    public Task NotificarAsync(EventoIntegracao evento, CancellationToken ct)
    {
        Eventos.Add(evento);
        return Task.CompletedTask;
    }
}

public sealed class UnidadeTrabalhoFalsa : IUnitOfWork
{
    public int Salvamentos { get; private set; }

    public Task<int> SalvarAsync(CancellationToken ct)
    {
        Salvamentos++;
        return Task.FromResult(1);
    }

    public void DescartarAlteracoes()
    {
    }
}

/// <summary>"Banco" em memória compartilhado pelos repositórios falsos.</summary>
public sealed class BancoEmMemoria
{
    public List<Usuario> Usuarios { get; } = [];

    public List<Cruzamento> Cruzamentos { get; } = [];

    public List<Sensor> Sensores { get; } = [];

    public List<LeituraSensor> Leituras { get; } = [];

    public List<PlanoSemaforico> Planos { get; } = [];

    public List<Ocorrencia> Ocorrencias { get; } = [];

    public List<SolicitacaoTravessia> Travessias { get; } = [];

    public List<AcionamentoEmergencia> Emergencias { get; } = [];
}

public sealed class UsuarioRepositoryFalso(BancoEmMemoria db) : IUsuarioRepository
{
    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(db.Usuarios.FirstOrDefault(u => u.Id == id));

    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct) => Task.FromResult(db.Usuarios.FirstOrDefault(u => u.Email == email));

    public Task<bool> EmailExisteAsync(string email, CancellationToken ct) => Task.FromResult(db.Usuarios.Any(u => u.Email == email));

    public Task AdicionarAsync(Usuario usuario, CancellationToken ct)
    {
        db.Usuarios.Add(usuario);
        return Task.CompletedTask;
    }
}

public sealed class CruzamentoRepositoryFalso(BancoEmMemoria db) : ICruzamentoRepository
{
    public Task<Cruzamento?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(db.Cruzamentos.FirstOrDefault(c => c.Id == id));

    public Task<Pagina<Cruzamento>> ListarAsync(string? busca, bool? ativo, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var itens = db.Cruzamentos
            .Where(c => busca is null || c.Nome.Contains(busca, StringComparison.OrdinalIgnoreCase))
            .Where(c => ativo is null || c.Ativo == ativo)
            .ToList();
        return Task.FromResult(new Pagina<Cruzamento>(itens.Skip((pagina - 1) * tamanhoPagina).Take(tamanhoPagina).ToList(), itens.Count));
    }

    public Task<IReadOnlyList<Cruzamento>> ListarAtivosAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Cruzamento>>(db.Cruzamentos.Where(c => c.Ativo).ToList());

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken ct) => Task.FromResult(db.Cruzamentos.Any(c => c.Codigo == codigo));

    public Task<int> ContarAsync(bool? ativo, CancellationToken ct) => Task.FromResult(db.Cruzamentos.Count(c => ativo is null || c.Ativo == ativo));

    public Task<IReadOnlyDictionary<Guid, string>> ObterNomesAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(db.Cruzamentos.Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Nome));

    public Task AdicionarAsync(Cruzamento cruzamento, CancellationToken ct)
    {
        db.Cruzamentos.Add(cruzamento);
        return Task.CompletedTask;
    }

    public void Remover(Cruzamento cruzamento) => db.Cruzamentos.Remove(cruzamento);
}

public sealed class SensorRepositoryFalso(BancoEmMemoria db) : ISensorRepository
{
    public Task<Sensor?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(db.Sensores.FirstOrDefault(s => s.Id == id));

    public Task<Sensor?> ObterPorCodigoAsync(string codigo, CancellationToken ct) => Task.FromResult(db.Sensores.FirstOrDefault(s => s.Codigo == codigo));

    public Task<IReadOnlyList<Sensor>> ListarAsync(Guid? cruzamentoId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Sensor>>(db.Sensores.Where(s => cruzamentoId is null || s.CruzamentoId == cruzamentoId).ToList());

    public Task<bool> CodigoExisteAsync(string codigo, CancellationToken ct) => Task.FromResult(db.Sensores.Any(s => s.Codigo == codigo));

    public Task<bool> ExisteParaViaAsync(Guid viaId, CancellationToken ct) => Task.FromResult(db.Sensores.Any(s => s.ViaId == viaId));

    public Task AdicionarAsync(Sensor sensor, CancellationToken ct)
    {
        db.Sensores.Add(sensor);
        return Task.CompletedTask;
    }

    public void Remover(Sensor sensor) => db.Sensores.Remove(sensor);
}

public sealed class LeituraRepositoryFalso(BancoEmMemoria db) : ILeituraRepository
{
    public Task<LeituraSensor?> ObterPorChaveExternaAsync(Guid sensorId, string chave, CancellationToken ct) =>
        Task.FromResult(db.Leituras.FirstOrDefault(l => l.SensorId == sensorId && l.LeituraExternaId == chave));

    public Task<IReadOnlySet<string>> ObterChavesExistentesAsync(Guid sensorId, IReadOnlyCollection<string> chaves, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<string>>(db.Leituras.Where(l => l.SensorId == sensorId && chaves.Contains(l.LeituraExternaId)).Select(l => l.LeituraExternaId).ToHashSet());

    public Task<IReadOnlyList<LeituraSensor>> ListarRecentesPorCruzamentoAsync(Guid cruzamentoId, DateTime desde, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LeituraSensor>>(db.Leituras.Where(l => l.CruzamentoId == cruzamentoId && l.ColetadaEm >= desde).ToList());

    public Task<Pagina<LeituraSensor>> ListarPorViaAsync(Guid viaId, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var itens = db.Leituras.Where(l => l.ViaId == viaId).OrderByDescending(l => l.ColetadaEm).ToList();
        return Task.FromResult(new Pagina<LeituraSensor>(itens.Skip((pagina - 1) * tamanhoPagina).Take(tamanhoPagina).ToList(), itens.Count));
    }

    public Task<int> ContarDesdeAsync(DateTime desde, CancellationToken ct) => Task.FromResult(db.Leituras.Count(l => l.ColetadaEm >= desde));

    public Task AdicionarAsync(LeituraSensor leitura, CancellationToken ct)
    {
        db.Leituras.Add(leitura);
        return Task.CompletedTask;
    }
}

public sealed class PlanoRepositoryFalso(BancoEmMemoria db) : IPlanoRepository
{
    public Task<PlanoSemaforico?> ObterVigenteAsync(Guid cruzamentoId, CancellationToken ct) =>
        Task.FromResult(db.Planos.Where(p => p.CruzamentoId == cruzamentoId && p.Vigente).OrderByDescending(p => p.GeradoEm).FirstOrDefault());

    public Task<Pagina<PlanoSemaforico>> ListarHistoricoAsync(Guid cruzamentoId, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var itens = db.Planos.Where(p => p.CruzamentoId == cruzamentoId).OrderByDescending(p => p.GeradoEm).ToList();
        return Task.FromResult(new Pagina<PlanoSemaforico>(itens.Skip((pagina - 1) * tamanhoPagina).Take(tamanhoPagina).ToList(), itens.Count));
    }

    public Task<IReadOnlyDictionary<ModoPlano, int>> ContarVigentesPorModoAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<ModoPlano, int>>(db.Planos.Where(p => p.Vigente).GroupBy(p => p.Modo).ToDictionary(g => g.Key, g => g.Count()));

    public Task AdicionarAsync(PlanoSemaforico plano, CancellationToken ct)
    {
        db.Planos.Add(plano);
        return Task.CompletedTask;
    }
}

public sealed class OcorrenciaRepositoryFalso(BancoEmMemoria db) : IOcorrenciaRepository
{
    public Task<Ocorrencia?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(db.Ocorrencias.FirstOrDefault(o => o.Id == id));

    public Task<Pagina<Ocorrencia>> ListarAsync(FiltroOcorrencias filtro, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var itens = db.Ocorrencias
            .Where(o => filtro.AutorId is null || o.AutorId == filtro.AutorId)
            .Where(o => filtro.CruzamentoId is null || o.CruzamentoId == filtro.CruzamentoId)
            .Where(o => filtro.Status is null || o.Status == filtro.Status)
            .Where(o => filtro.Prioridade is null || o.Prioridade == filtro.Prioridade)
            .ToList();
        return Task.FromResult(new Pagina<Ocorrencia>(itens.Skip((pagina - 1) * tamanhoPagina).Take(tamanhoPagina).ToList(), itens.Count));
    }

    public Task<bool> ExisteParaCruzamentoAsync(Guid cruzamentoId, CancellationToken ct) => Task.FromResult(db.Ocorrencias.Any(o => o.CruzamentoId == cruzamentoId));

    public Task<IReadOnlyDictionary<PrioridadeOcorrencia, int>> ContarPendentesPorPrioridadeAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<PrioridadeOcorrencia, int>>(db.Ocorrencias.Where(o => !o.EstaFinalizada).GroupBy(o => o.Prioridade).ToDictionary(g => g.Key, g => g.Count()));

    public Task AdicionarAsync(Ocorrencia ocorrencia, CancellationToken ct)
    {
        db.Ocorrencias.Add(ocorrencia);
        return Task.CompletedTask;
    }

    public void Remover(Ocorrencia ocorrencia) => db.Ocorrencias.Remove(ocorrencia);
}

public sealed class TravessiaRepositoryFalso(BancoEmMemoria db) : ISolicitacaoTravessiaRepository
{
    public Task<SolicitacaoTravessia?> ObterVigenteDoUsuarioAsync(Guid cruzamentoId, Guid usuarioId, DateTime agora, CancellationToken ct) =>
        Task.FromResult(db.Travessias.FirstOrDefault(s => s.CruzamentoId == cruzamentoId && s.UsuarioId == usuarioId && s.EstaVigente(agora)));

    public Task<bool> ExisteVigenteAsync(Guid cruzamentoId, DateTime agora, CancellationToken ct) =>
        Task.FromResult(db.Travessias.Any(s => s.CruzamentoId == cruzamentoId && s.EstaVigente(agora)));

    public Task AdicionarAsync(SolicitacaoTravessia solicitacao, CancellationToken ct)
    {
        db.Travessias.Add(solicitacao);
        return Task.CompletedTask;
    }
}

public sealed class EmergenciaRepositoryFalso(BancoEmMemoria db) : IEmergenciaRepository
{
    public Task<AcionamentoEmergencia?> ObterPorIdAsync(Guid id, CancellationToken ct) => Task.FromResult(db.Emergencias.FirstOrDefault(a => a.Id == id));

    public Task<AcionamentoEmergencia?> ObterAtivaAsync(Guid cruzamentoId, DateTime agora, CancellationToken ct) =>
        Task.FromResult(db.Emergencias.FirstOrDefault(a => a.CruzamentoId == cruzamentoId && a.EstaAtivo(agora)));

    public Task<IReadOnlyList<AcionamentoEmergencia>> ListarAtivasAsync(DateTime agora, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AcionamentoEmergencia>>(db.Emergencias.Where(a => a.EstaAtivo(agora)).ToList());

    public Task AdicionarAsync(AcionamentoEmergencia acionamento, CancellationToken ct)
    {
        db.Emergencias.Add(acionamento);
        return Task.CompletedTask;
    }
}
