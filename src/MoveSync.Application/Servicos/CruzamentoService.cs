using MoveSync.Application.Abstracoes;
using MoveSync.Application.Contratos;
using MoveSync.Application.Excecoes;
using MoveSync.Domain.Comum;
using MoveSync.Domain.Entidades;

namespace MoveSync.Application.Servicos;

/// <summary>CRUD de cruzamentos e vias (perfil Gestor) e consulta pública da lista.</summary>
public sealed class CruzamentoService(
    ICruzamentoRepository cruzamentos,
    ISensorRepository sensores,
    IOcorrenciaRepository ocorrencias,
    IUnitOfWork unitOfWork,
    PlanoSemaforicoService planoService,
    TimeProvider relogio)
{
    public async Task<PaginaResponse<CruzamentoResponse>> ListarAsync(
        string? busca,
        bool? ativo,
        ParametrosPaginacao paginacao,
        CancellationToken ct)
    {
        var pagina = await cruzamentos.ListarAsync(busca?.Trim(), ativo, paginacao.PaginaAtual, paginacao.ItensPorPagina, ct);
        return PaginaResponse<CruzamentoResponse>.Criar(
            pagina.Itens.Select(CruzamentoResponse.De).ToList(),
            paginacao.PaginaAtual,
            paginacao.ItensPorPagina,
            pagina.Total);
    }

    public async Task<CruzamentoResponse> ObterAsync(Guid id, CancellationToken ct) =>
        CruzamentoResponse.De(await ObterEntidadeAsync(id, ct));

    public async Task<CruzamentoResponse> CriarAsync(CriarCruzamentoRequest requisicao, CancellationToken ct)
    {
        var codigo = Cruzamento.NormalizarCodigo(requisicao.Codigo);
        if (await cruzamentos.CodigoExisteAsync(codigo, ct))
        {
            throw CodigoDuplicado(codigo);
        }

        var agora = Agora();
        var cruzamento = new Cruzamento(
            codigo,
            requisicao.Nome,
            requisicao.Endereco,
            requisicao.Latitude,
            requisicao.Longitude,
            requisicao.LarguraTravessiaMetros,
            agora);

        foreach (var via in requisicao.Vias)
        {
            cruzamento.AdicionarVia(via.Nome, via.Sentido, via.NumeroFaixas, via.VelocidadeMaximaKmh, agora);
        }

        await cruzamentos.AdicionarAsync(cruzamento, ct);
        try
        {
            await unitOfWork.SalvarAsync(ct);
        }
        catch (ConflitoPersistenciaException)
        {
            throw CodigoDuplicado(codigo);
        }

        return CruzamentoResponse.De(cruzamento);
    }

    public async Task<CruzamentoResponse> AtualizarAsync(Guid id, AtualizarCruzamentoRequest requisicao, CancellationToken ct)
    {
        var cruzamento = await ObterEntidadeAsync(id, ct);
        cruzamento.Atualizar(
            requisicao.Nome,
            requisicao.Endereco,
            requisicao.Latitude,
            requisicao.Longitude,
            requisicao.LarguraTravessiaMetros,
            requisicao.Ativo,
            Agora());
        await unitOfWork.SalvarAsync(ct);
        return CruzamentoResponse.De(cruzamento);
    }

    /// <summary>
    /// Exclusão física. Bloqueada se houver ocorrências de cidadãos (o histórico do cidadão é preservado):
    /// nesse caso o gestor deve apenas desativar o cruzamento.
    /// </summary>
    public async Task RemoverAsync(Guid id, CancellationToken ct)
    {
        var cruzamento = await ObterEntidadeAsync(id, ct);
        if (await ocorrencias.ExisteParaCruzamentoAsync(id, ct))
        {
            throw new RegraNegocioException(
                "CRUZAMENTO_COM_OCORRENCIAS",
                "O cruzamento possui ocorrências de cidadãos e não pode ser excluído. Desative-o (Ativo = false).",
                TipoViolacao.Conflito);
        }

        cruzamentos.Remover(cruzamento);
        await unitOfWork.SalvarAsync(ct);
    }

    public async Task<CruzamentoResponse> AlterarModoAsync(Guid id, AlterarModoRequest requisicao, CancellationToken ct)
    {
        var cruzamento = await ObterEntidadeAsync(id, ct);
        cruzamento.DefinirModo(requisicao.Modo!.Value, Agora());
        await unitOfWork.SalvarAsync(ct);

        if (cruzamento.Ativo)
        {
            // A mudança de modo vale imediatamente: o plano é recalculado na hora.
            await planoService.RecalcularAsync(id, ct);
        }

        return CruzamentoResponse.De(cruzamento);
    }

    public async Task<ViaResponse> AdicionarViaAsync(Guid id, CriarViaRequest requisicao, CancellationToken ct)
    {
        var cruzamento = await ObterEntidadeAsync(id, ct);
        var via = cruzamento.AdicionarVia(requisicao.Nome, requisicao.Sentido, requisicao.NumeroFaixas, requisicao.VelocidadeMaximaKmh, Agora());
        await unitOfWork.SalvarAsync(ct);
        return ViaResponse.De(via);
    }

    public async Task<ViaResponse> AtualizarViaAsync(Guid id, Guid viaId, AtualizarViaRequest requisicao, CancellationToken ct)
    {
        var cruzamento = await ObterEntidadeAsync(id, ct);
        GarantirViaExiste(cruzamento, viaId);

        var ativasRestantes = cruzamento.ViasAtivas.Count(v => v.Id != viaId) + (requisicao.Ativa ? 1 : 0);
        if (ativasRestantes < 2)
        {
            throw ViasMinimas();
        }

        var via = cruzamento.AtualizarVia(
            viaId,
            requisicao.Nome,
            requisicao.Sentido,
            requisicao.NumeroFaixas,
            requisicao.VelocidadeMaximaKmh,
            requisicao.Ativa,
            Agora());
        await unitOfWork.SalvarAsync(ct);
        return ViaResponse.De(via);
    }

    public async Task RemoverViaAsync(Guid id, Guid viaId, CancellationToken ct)
    {
        var cruzamento = await ObterEntidadeAsync(id, ct);
        GarantirViaExiste(cruzamento, viaId);

        if (cruzamento.ViasAtivas.Count(v => v.Id != viaId) < 2)
        {
            throw ViasMinimas();
        }

        if (await sensores.ExisteParaViaAsync(viaId, ct))
        {
            throw new RegraNegocioException(
                "VIA_COM_SENSOR",
                "Remova o sensor instalado nesta via antes de excluí-la.",
                TipoViolacao.Conflito);
        }

        cruzamento.RemoverVia(viaId, Agora());
        await unitOfWork.SalvarAsync(ct);
    }

    private static void GarantirViaExiste(Cruzamento cruzamento, Guid viaId)
    {
        if (cruzamento.Vias.All(v => v.Id != viaId))
        {
            throw new RecursoNaoEncontradoException("Via", viaId);
        }
    }

    private async Task<Cruzamento> ObterEntidadeAsync(Guid id, CancellationToken ct) =>
        await cruzamentos.ObterPorIdAsync(id, ct) ?? throw new RecursoNaoEncontradoException("Cruzamento", id);

    private static RegraNegocioException CodigoDuplicado(string codigo) =>
        new("CODIGO_DUPLICADO", $"Já existe um cruzamento com o código '{codigo}'.", TipoViolacao.Conflito);

    private static RegraNegocioException ViasMinimas() =>
        new("VIAS_MINIMAS", "O cruzamento precisa manter ao menos 2 vias ativas.");

    private DateTime Agora() => relogio.GetUtcNow().UtcDateTime;
}
