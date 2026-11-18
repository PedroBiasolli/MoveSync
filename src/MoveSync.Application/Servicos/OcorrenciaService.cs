using MoveSync.Application.Abstracoes;
using MoveSync.Application.Contratos;
using MoveSync.Application.Excecoes;
using MoveSync.Domain.Comum;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;

namespace MoveSync.Application.Servicos;

/// <summary>
/// Zeladoria colaborativa: o cidadão relata problemas e acompanha a resposta da prefeitura.
/// Privacidade: um cidadão nunca enxerga ocorrências de outro (a API responde 404, sem revelar que existem).
/// </summary>
public sealed class OcorrenciaService(
    IOcorrenciaRepository ocorrencias,
    ICruzamentoRepository cruzamentos,
    IUnitOfWork unitOfWork,
    INotificadorNucleo notificador,
    TimeProvider relogio)
{
    public async Task<OcorrenciaResponse> CriarAsync(Guid autorId, CriarOcorrenciaRequest requisicao, CancellationToken ct)
    {
        var cruzamento = await cruzamentos.ObterPorIdAsync(requisicao.CruzamentoId!.Value, ct)
            ?? throw new RecursoNaoEncontradoException("Cruzamento", requisicao.CruzamentoId!.Value);
        if (!cruzamento.Ativo)
        {
            throw new RegraNegocioException("CRUZAMENTO_INATIVO", "Não é possível registrar ocorrências em um cruzamento desativado.");
        }

        var agora = Agora();
        var ocorrencia = new Ocorrencia(cruzamento.Id, autorId, requisicao.Tipo!.Value, requisicao.Descricao, agora);
        await ocorrencias.AdicionarAsync(ocorrencia, ct);
        await unitOfWork.SalvarAsync(ct);

        if (ocorrencia.Prioridade == PrioridadeOcorrencia.Critica)
        {
            await notificador.NotificarAsync(
                new EventoIntegracao("OCORRENCIA_CRITICA", cruzamento.Id, $"{ocorrencia.Tipo}: {ocorrencia.Descricao}", agora),
                ct);
        }

        return OcorrenciaResponse.De(ocorrencia, cruzamento.Nome);
    }

    public Task<PaginaResponse<OcorrenciaResponse>> ListarDoAutorAsync(
        Guid autorId,
        StatusOcorrencia? status,
        ParametrosPaginacao paginacao,
        CancellationToken ct) =>
        ListarAsync(new FiltroOcorrencias(AutorId: autorId, Status: status), paginacao, ct);

    public async Task<PaginaResponse<OcorrenciaResponse>> ListarAsync(FiltroOcorrencias filtro, ParametrosPaginacao paginacao, CancellationToken ct)
    {
        var pagina = await ocorrencias.ListarAsync(filtro, paginacao.PaginaAtual, paginacao.ItensPorPagina, ct);
        var nomes = await cruzamentos.ObterNomesAsync(pagina.Itens.Select(o => o.CruzamentoId).Distinct(), ct);
        return PaginaResponse<OcorrenciaResponse>.Criar(
            pagina.Itens.Select(o => OcorrenciaResponse.De(o, nomes.GetValueOrDefault(o.CruzamentoId))).ToList(),
            paginacao.PaginaAtual,
            paginacao.ItensPorPagina,
            pagina.Total);
    }

    public async Task<OcorrenciaResponse> ObterAsync(Guid id, Guid usuarioId, bool usuarioEhGestor, CancellationToken ct)
    {
        var ocorrencia = await ObterVisivelAsync(id, usuarioId, usuarioEhGestor, ct);
        return await ParaRespostaAsync(ocorrencia, ct);
    }

    public async Task<OcorrenciaResponse> AtualizarAsync(Guid id, Guid autorId, AtualizarOcorrenciaRequest requisicao, CancellationToken ct)
    {
        var ocorrencia = await ObterVisivelAsync(id, autorId, usuarioEhGestor: false, ct);
        ocorrencia.AtualizarPeloAutor(autorId, requisicao.Tipo!.Value, requisicao.Descricao, Agora());
        await unitOfWork.SalvarAsync(ct);
        return await ParaRespostaAsync(ocorrencia, ct);
    }

    public async Task RemoverAsync(Guid id, Guid autorId, CancellationToken ct)
    {
        var ocorrencia = await ObterVisivelAsync(id, autorId, usuarioEhGestor: false, ct);
        ocorrencia.GarantirExclusaoPeloAutor(autorId);
        ocorrencias.Remover(ocorrencia);
        await unitOfWork.SalvarAsync(ct);
    }

    public async Task<OcorrenciaResponse> AlterarStatusAsync(Guid id, AlterarStatusOcorrenciaRequest requisicao, CancellationToken ct)
    {
        var ocorrencia = await ocorrencias.ObterPorIdAsync(id, ct)
            ?? throw new RecursoNaoEncontradoException("Ocorrência", id);
        ocorrencia.AlterarStatus(requisicao.Status!.Value, requisicao.Resposta, Agora());
        await unitOfWork.SalvarAsync(ct);
        return await ParaRespostaAsync(ocorrencia, ct);
    }

    private async Task<Ocorrencia> ObterVisivelAsync(Guid id, Guid usuarioId, bool usuarioEhGestor, CancellationToken ct)
    {
        var ocorrencia = await ocorrencias.ObterPorIdAsync(id, ct);
        if (ocorrencia is null || (!usuarioEhGestor && ocorrencia.AutorId != usuarioId))
        {
            throw new RecursoNaoEncontradoException("Ocorrência", id);
        }

        return ocorrencia;
    }

    private async Task<OcorrenciaResponse> ParaRespostaAsync(Ocorrencia ocorrencia, CancellationToken ct)
    {
        var nomes = await cruzamentos.ObterNomesAsync([ocorrencia.CruzamentoId], ct);
        return OcorrenciaResponse.De(ocorrencia, nomes.GetValueOrDefault(ocorrencia.CruzamentoId));
    }

    private DateTime Agora() => relogio.GetUtcNow().UtcDateTime;
}
