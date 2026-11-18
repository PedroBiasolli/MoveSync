using System.Globalization;
using MoveSync.Application.Abstracoes;
using MoveSync.Application.Contratos;
using MoveSync.Application.Excecoes;
using MoveSync.Domain.Comum;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;
using MoveSync.Domain.Servicos;

namespace MoveSync.Application.Servicos;

/// <summary>Resultado do recálculo em lote feito pelo worker em segundo plano.</summary>
public sealed record ResultadoRecalculoGeral(int Processados, int NovosPlanos, IReadOnlyList<string> Falhas);

/// <summary>
/// Orquestra o motor semafórico: coleta a demanda recente no banco, chama a calculadora do domínio,
/// persiste o plano quando ele muda e entrega as visões para o cidadão (status) e para o gestor (indicadores).
/// </summary>
public sealed class PlanoSemaforicoService(
    ICruzamentoRepository cruzamentos,
    ILeituraRepository leituras,
    IPlanoRepository planos,
    IEmergenciaRepository emergencias,
    ISolicitacaoTravessiaRepository travessias,
    IUnitOfWork unitOfWork,
    INotificadorNucleo notificador,
    CalculadoraPlanoSemaforico calculadora,
    AnalisadorDesempenho analisador,
    TimeProvider relogio)
{
    private readonly ParametrosSemaforicos _parametros = calculadora.Parametros;

    public async Task<PlanoResponse> RecalcularAsync(Guid cruzamentoId, CancellationToken ct)
    {
        var cruzamento = await ObterCruzamentoAtivoAsync(cruzamentoId, ct);
        var (plano, _) = await RecalcularInternoAsync(cruzamento, ct);
        return PlanoResponse.De(plano);
    }

    /// <summary>Chamado periodicamente pelo worker: mantém todos os cruzamentos ativos com o plano adequado.</summary>
    public async Task<ResultadoRecalculoGeral> RecalcularTodosAsync(CancellationToken ct)
    {
        var ativos = await cruzamentos.ListarAtivosAsync(ct);
        var falhas = new List<string>();
        var novos = 0;
        foreach (var cruzamento in ativos)
        {
            try
            {
                var (_, novo) = await RecalcularInternoAsync(cruzamento, ct);
                if (novo)
                {
                    novos++;
                }
            }
            catch (RegraNegocioException ex)
            {
                falhas.Add($"{cruzamento.Codigo}: {ex.Message}");
                unitOfWork.DescartarAlteracoes();
            }
        }

        return new ResultadoRecalculoGeral(ativos.Count, novos, falhas);
    }

    public async Task<PlanoResponse> ObterVigenteAsync(Guid cruzamentoId, CancellationToken ct)
    {
        await GarantirCruzamentoExisteAsync(cruzamentoId, ct);
        var plano = await planos.ObterVigenteAsync(cruzamentoId, ct)
            ?? throw new RecursoNaoEncontradoException("Plano vigente do cruzamento", cruzamentoId);
        return PlanoResponse.De(plano);
    }

    public async Task<PaginaResponse<PlanoResponse>> ListarHistoricoAsync(Guid cruzamentoId, ParametrosPaginacao paginacao, CancellationToken ct)
    {
        await GarantirCruzamentoExisteAsync(cruzamentoId, ct);
        var pagina = await planos.ListarHistoricoAsync(cruzamentoId, paginacao.PaginaAtual, paginacao.ItensPorPagina, ct);
        return PaginaResponse<PlanoResponse>.Criar(
            pagina.Itens.Select(PlanoResponse.De).ToList(),
            paginacao.PaginaAtual,
            paginacao.ItensPorPagina,
            pagina.Total);
    }

    /// <summary>
    /// Benefício direto ao cidadão: situação do cruzamento, tempo estimado de espera por via e para o pedestre.
    /// É uma consulta (GET) sem efeitos colaterais: se ainda não houver plano gravado, calcula um provisório em memória.
    /// </summary>
    public async Task<StatusCruzamentoResponse> ObterStatusAsync(Guid cruzamentoId, CancellationToken ct)
    {
        var cruzamento = await ObterCruzamentoAtivoAsync(cruzamentoId, ct);
        var agora = Agora();
        var entrada = await MontarEntradaAsync(cruzamento, agora, ct);
        var vigente = await planos.ObterVigenteAsync(cruzamentoId, ct);

        // O plano gravado só é usado se ainda corresponde à situação atual; caso contrário
        // (ex.: emergência acabou de expirar, via nova) mostramos o cálculo atualizado como provisório.
        var calculoAtual = calculadora.Calcular(entrada);
        var usarVigente = vigente is not null
            && vigente.EquivalenteA(calculoAtual, _parametros.ToleranciaMudancaPlanoSegundos);

        ModoPlano modo;
        int ciclo;
        int tempoPedestre;
        bool acessivel;
        DateTime? geradoEm;
        Dictionary<Guid, int> verdes;
        if (usarVigente)
        {
            modo = vigente!.Modo;
            ciclo = vigente.CicloSegundos;
            tempoPedestre = vigente.TempoTravessiaPedestreSegundos;
            acessivel = vigente.TravessiaAcessivel;
            geradoEm = vigente.GeradoEm;
            verdes = vigente.Fases.ToDictionary(f => f.ViaId, f => f.VerdeSegundos);
        }
        else
        {
            modo = calculoAtual.Modo;
            ciclo = calculoAtual.CicloSegundos;
            tempoPedestre = calculoAtual.TempoTravessiaPedestreSegundos;
            acessivel = calculoAtual.TravessiaAcessivel;
            geradoEm = null;
            verdes = calculoAtual.Fases.ToDictionary(f => f.ViaId, f => f.VerdeSegundos);
        }

        var vias = new List<StatusViaResponse>();
        foreach (var demanda in entrada.Vias)
        {
            var via = cruzamento.ObterVia(demanda.ViaId);
            var estimativa = AnalisadorDesempenho.EstimarEspera(
                ciclo,
                verdes[demanda.ViaId],
                calculadora.CalcularDemandaVeiculosHora(demanda),
                calculadora.CalcularFluxoSaturacao(demanda),
                demanda.PossuiDadosRecentes);
            vias.Add(new StatusViaResponse(
                via.Id,
                via.Nome,
                via.Sentido,
                verdes[demanda.ViaId],
                estimativa.EsperaMediaSegundos,
                estimativa.EsperaMaximaSegundos,
                estimativa.Nivel,
                demanda.PossuiDadosRecentes ? demanda.FilaAtual : null));
        }

        var avisos = new List<string>();
        if (modo == ModoPlano.Emergencia)
        {
            avisos.Add("Veículo de emergência em passagem: mantenha a atenção e dê passagem.");
        }

        if (modo == ModoPlano.Contingencia)
        {
            avisos.Add("Sensores sem dados recentes: o semáforo opera em tempo fixo seguro. As estimativas são aproximadas.");
        }

        if (acessivel)
        {
            avisos.Add("Travessia acessível ativa: o tempo para pedestres foi ampliado.");
        }

        return new StatusCruzamentoResponse(
            cruzamento.Id,
            cruzamento.Codigo,
            cruzamento.Nome,
            cruzamento.Endereco,
            modo,
            DescreverSituacao(modo),
            ciclo,
            geradoEm,
            !usarVigente,
            new StatusTravessiaResponse(tempoPedestre, AnalisadorDesempenho.EstimarEsperaPedestre(ciclo, tempoPedestre), acessivel),
            vias,
            avisos);
    }

    /// <summary>Mede o impacto: compara tempo fixo × adaptativo para a demanda real dos últimos minutos.</summary>
    public async Task<IndicadoresResponse> ObterIndicadoresAsync(Guid cruzamentoId, CancellationToken ct)
    {
        var cruzamento = await ObterCruzamentoAtivoAsync(cruzamentoId, ct);
        var entrada = await MontarEntradaAsync(cruzamento, Agora(), ct);

        if (entrada.Vias.Any(v => !v.PossuiDadosRecentes))
        {
            return new IndicadoresResponse(
                cruzamentoId,
                _parametros.JanelaDadosMinutos,
                false,
                $"Sem leituras recentes em todas as vias (últimos {_parametros.JanelaDadosMinutos} min). " +
                "Envie telemetria dos sensores para comparar o tempo fixo com o adaptativo.",
                null);
        }

        var comparacao = analisador.Comparar(entrada);
        var c = CultureInfo.InvariantCulture;
        var interpretacao = comparacao.ReducaoAtrasoPercentual > 0
            ? string.Create(c, $"Com a demanda dos últimos {_parametros.JanelaDadosMinutos} min ({comparacao.DemandaTotalVeiculosHora} veíc/h), o plano adaptativo reduz o atraso médio de {comparacao.TempoFixo.AtrasoMedioSegundosPorVeiculo}s para {comparacao.Adaptativo.AtrasoMedioSegundosPorVeiculo}s por veículo ({comparacao.ReducaoAtrasoPercentual}% menos), economizando cerca de {comparacao.TempoParadoEvitadoHorasPorHora} h de espera e {comparacao.Co2EvitadoKgPorHora} kg de CO2 a cada hora.")
            : "Com a demanda atual o tempo fixo já é adequado; o plano adaptativo mantém o desempenho e passa a ganhar quando a demanda fica desequilibrada.";

        return new IndicadoresResponse(cruzamentoId, _parametros.JanelaDadosMinutos, true, interpretacao, comparacao);
    }

    internal async Task<EntradaCalculoPlano> MontarEntradaAsync(Cruzamento cruzamento, DateTime agora, CancellationToken ct)
    {
        var desde = agora.AddMinutes(-_parametros.JanelaDadosMinutos);
        var recentes = await leituras.ListarRecentesPorCruzamentoAsync(cruzamento.Id, desde, ct);
        var emergencia = await emergencias.ObterAtivaAsync(cruzamento.Id, agora, ct);
        var acessivel = await travessias.ExisteVigenteAsync(cruzamento.Id, agora, ct);

        var demandas = cruzamento.ViasAtivas
            .OrderBy(v => v.Nome, StringComparer.OrdinalIgnoreCase)
            .Select(via =>
            {
                var daVia = recentes.Where(l => l.ViaId == via.Id).ToList();
                var ultima = daVia.OrderByDescending(l => l.ColetadaEm).FirstOrDefault();
                return new DemandaVia(
                    via.Id,
                    via.Nome,
                    via.NumeroFaixas,
                    via.VelocidadeMaximaKmh,
                    daVia.Sum(l => l.VeiculosContados),
                    ultima?.TamanhoFila ?? 0,
                    daVia.Count > 0);
            })
            .ToList();

        var viaEmergencia = emergencia is not null && demandas.Any(d => d.ViaId == emergencia.ViaId)
            ? emergencia.ViaId
            : (Guid?)null;

        return new EntradaCalculoPlano(cruzamento.Modo, cruzamento.LarguraTravessiaMetros, demandas, acessivel, viaEmergencia);
    }

    private async Task<(PlanoSemaforico Plano, bool Novo)> RecalcularInternoAsync(Cruzamento cruzamento, CancellationToken ct)
    {
        var agora = Agora();
        var entrada = await MontarEntradaAsync(cruzamento, agora, ct);
        var calculo = calculadora.Calcular(entrada);
        var vigente = await planos.ObterVigenteAsync(cruzamento.Id, ct);

        if (vigente is not null && vigente.EquivalenteA(calculo, _parametros.ToleranciaMudancaPlanoSegundos))
        {
            vigente.MarcarVerificado(agora);
            await unitOfWork.SalvarAsync(ct);
            return (vigente, false);
        }

        var modoAnterior = vigente?.Modo;
        vigente?.Encerrar(agora);
        var novo = new PlanoSemaforico(cruzamento.Id, calculo, agora);
        await planos.AdicionarAsync(novo, ct);
        await unitOfWork.SalvarAsync(ct);

        if (novo.Modo == ModoPlano.Contingencia && modoAnterior != ModoPlano.Contingencia)
        {
            await notificador.NotificarAsync(
                new EventoIntegracao("CRUZAMENTO_EM_CONTINGENCIA", cruzamento.Id, novo.Justificativa, agora),
                ct);
        }

        return (novo, true);
    }

    private async Task<Cruzamento> ObterCruzamentoAtivoAsync(Guid cruzamentoId, CancellationToken ct)
    {
        var cruzamento = await cruzamentos.ObterPorIdAsync(cruzamentoId, ct)
            ?? throw new RecursoNaoEncontradoException("Cruzamento", cruzamentoId);
        if (!cruzamento.Ativo)
        {
            throw new RegraNegocioException("CRUZAMENTO_INATIVO", "O cruzamento está desativado.");
        }

        return cruzamento;
    }

    private async Task GarantirCruzamentoExisteAsync(Guid cruzamentoId, CancellationToken ct)
    {
        _ = await cruzamentos.ObterPorIdAsync(cruzamentoId, ct)
            ?? throw new RecursoNaoEncontradoException("Cruzamento", cruzamentoId);
    }

    private static string DescreverSituacao(ModoPlano modo) =>
        modo switch
        {
            ModoPlano.Adaptativo => "Semáforo inteligente: tempos ajustados ao fluxo atual.",
            ModoPlano.TempoFixo => "Semáforo em tempo fixo definido pela prefeitura.",
            ModoPlano.Contingencia => "Modo de contingência: tempo fixo seguro por falta de dados dos sensores.",
            ModoPlano.Emergencia => "Prioridade para veículo de emergência.",
            _ => "Situação desconhecida."
        };

    private DateTime Agora() => relogio.GetUtcNow().UtcDateTime;
}
