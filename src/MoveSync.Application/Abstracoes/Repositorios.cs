using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;

namespace MoveSync.Application.Abstracoes;

/// <summary>Resultado paginado vindo do repositório.</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Itens, int Total);

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken ct);

    Task<Usuario?> ObterPorEmailAsync(string emailNormalizado, CancellationToken ct);

    Task<bool> EmailExisteAsync(string emailNormalizado, CancellationToken ct);

    Task AdicionarAsync(Usuario usuario, CancellationToken ct);
}

public interface ICruzamentoRepository
{
    /// <summary>Retorna o cruzamento rastreado (para alteração), já com as vias.</summary>
    Task<Cruzamento?> ObterPorIdAsync(Guid id, CancellationToken ct);

    Task<Pagina<Cruzamento>> ListarAsync(string? busca, bool? ativo, int pagina, int tamanhoPagina, CancellationToken ct);

    Task<IReadOnlyList<Cruzamento>> ListarAtivosAsync(CancellationToken ct);

    Task<bool> CodigoExisteAsync(string codigo, CancellationToken ct);

    Task<int> ContarAsync(bool? ativo, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, string>> ObterNomesAsync(IEnumerable<Guid> ids, CancellationToken ct);

    Task AdicionarAsync(Cruzamento cruzamento, CancellationToken ct);

    void Remover(Cruzamento cruzamento);
}

public interface ISensorRepository
{
    Task<Sensor?> ObterPorIdAsync(Guid id, CancellationToken ct);

    Task<Sensor?> ObterPorCodigoAsync(string codigo, CancellationToken ct);

    Task<IReadOnlyList<Sensor>> ListarAsync(Guid? cruzamentoId, CancellationToken ct);

    Task<bool> CodigoExisteAsync(string codigo, CancellationToken ct);

    Task<bool> ExisteParaViaAsync(Guid viaId, CancellationToken ct);

    Task AdicionarAsync(Sensor sensor, CancellationToken ct);

    void Remover(Sensor sensor);
}

public interface ILeituraRepository
{
    Task<LeituraSensor?> ObterPorChaveExternaAsync(Guid sensorId, string leituraExternaId, CancellationToken ct);

    Task<IReadOnlySet<string>> ObterChavesExistentesAsync(Guid sensorId, IReadOnlyCollection<string> chaves, CancellationToken ct);

    Task<IReadOnlyList<LeituraSensor>> ListarRecentesPorCruzamentoAsync(Guid cruzamentoId, DateTime desdeUtc, CancellationToken ct);

    Task<Pagina<LeituraSensor>> ListarPorViaAsync(Guid viaId, int pagina, int tamanhoPagina, CancellationToken ct);

    Task<int> ContarDesdeAsync(DateTime desdeUtc, CancellationToken ct);

    Task AdicionarAsync(LeituraSensor leitura, CancellationToken ct);
}

public interface IPlanoRepository
{
    Task<PlanoSemaforico?> ObterVigenteAsync(Guid cruzamentoId, CancellationToken ct);

    Task<Pagina<PlanoSemaforico>> ListarHistoricoAsync(Guid cruzamentoId, int pagina, int tamanhoPagina, CancellationToken ct);

    Task<IReadOnlyDictionary<ModoPlano, int>> ContarVigentesPorModoAsync(CancellationToken ct);

    Task AdicionarAsync(PlanoSemaforico plano, CancellationToken ct);
}

public sealed record FiltroOcorrencias(
    Guid? AutorId = null,
    Guid? CruzamentoId = null,
    StatusOcorrencia? Status = null,
    PrioridadeOcorrencia? Prioridade = null);

public interface IOcorrenciaRepository
{
    Task<Ocorrencia?> ObterPorIdAsync(Guid id, CancellationToken ct);

    Task<Pagina<Ocorrencia>> ListarAsync(FiltroOcorrencias filtro, int pagina, int tamanhoPagina, CancellationToken ct);

    Task<bool> ExisteParaCruzamentoAsync(Guid cruzamentoId, CancellationToken ct);

    Task<IReadOnlyDictionary<PrioridadeOcorrencia, int>> ContarPendentesPorPrioridadeAsync(CancellationToken ct);

    Task AdicionarAsync(Ocorrencia ocorrencia, CancellationToken ct);

    void Remover(Ocorrencia ocorrencia);
}

public interface ISolicitacaoTravessiaRepository
{
    Task<SolicitacaoTravessia?> ObterVigenteDoUsuarioAsync(Guid cruzamentoId, Guid usuarioId, DateTime agoraUtc, CancellationToken ct);

    Task<bool> ExisteVigenteAsync(Guid cruzamentoId, DateTime agoraUtc, CancellationToken ct);

    Task AdicionarAsync(SolicitacaoTravessia solicitacao, CancellationToken ct);
}

public interface IEmergenciaRepository
{
    Task<AcionamentoEmergencia?> ObterPorIdAsync(Guid id, CancellationToken ct);

    Task<AcionamentoEmergencia?> ObterAtivaAsync(Guid cruzamentoId, DateTime agoraUtc, CancellationToken ct);

    Task<IReadOnlyList<AcionamentoEmergencia>> ListarAtivasAsync(DateTime agoraUtc, CancellationToken ct);

    Task AdicionarAsync(AcionamentoEmergencia acionamento, CancellationToken ct);
}

/// <summary>Confirma a transação. Traduz violações de unicidade para <see cref="Excecoes.ConflitoPersistenciaException"/>.</summary>
public interface IUnitOfWork
{
    Task<int> SalvarAsync(CancellationToken ct);

    /// <summary>Descarta alterações pendentes (usado após um conflito de concorrência).</summary>
    void DescartarAlteracoes();
}
