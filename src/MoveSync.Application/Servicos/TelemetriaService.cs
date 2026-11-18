using MoveSync.Application.Abstracoes;
using MoveSync.Application.Contratos;
using MoveSync.Application.Excecoes;
using MoveSync.Domain.Comum;
using MoveSync.Domain.Entidades;

namespace MoveSync.Application.Servicos;

/// <summary>
/// Ingestão de telemetria dos sensores — o ponto mais sensível a requisições repetidas:
/// o ESP32 reenvia quando não recebe confirmação (rede instável). A chave natural
/// (sensor + LeituraId) garante que cada leitura entre UMA única vez, mesmo com reenvio
/// simultâneo (o índice único no banco é a última linha de defesa).
/// </summary>
public sealed class TelemetriaService(
    ISensorRepository sensores,
    ILeituraRepository leituras,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    public async Task<RegistroLeituraResultado> RegistrarAsync(Guid sensorId, RegistrarLeituraRequest requisicao, CancellationToken ct)
    {
        var sensor = await ObterSensorAtivoAsync(sensorId, ct);
        var chave = requisicao.LeituraId.Trim();

        var existente = await leituras.ObterPorChaveExternaAsync(sensor.Id, chave, ct);
        if (existente is not null)
        {
            return new RegistroLeituraResultado(LeituraResponse.De(existente), Duplicada: true);
        }

        var agora = Agora();
        var leitura = CriarLeitura(sensor, requisicao, chave, agora);
        sensor.RegistrarComunicacao(agora);
        await leituras.AdicionarAsync(leitura, ct);

        try
        {
            await unitOfWork.SalvarAsync(ct);
            return new RegistroLeituraResultado(LeituraResponse.De(leitura), Duplicada: false);
        }
        catch (ConflitoPersistenciaException)
        {
            // Corrida: outra requisição idêntica gravou primeiro. Devolvemos a leitura que venceu.
            unitOfWork.DescartarAlteracoes();
            var vencedora = await leituras.ObterPorChaveExternaAsync(sensor.Id, chave, ct)
                ?? throw new ConflitoPersistenciaException("Conflito ao registrar a leitura. Tente novamente.");
            return new RegistroLeituraResultado(LeituraResponse.De(vencedora), Duplicada: true);
        }
    }

    /// <summary>
    /// Lote: o dispositivo guarda leituras enquanto está sem rede e envia tudo ao reconectar.
    /// Leituras repetidas são ignoradas e leituras inválidas são rejeitadas individualmente,
    /// sem derrubar o lote inteiro.
    /// </summary>
    public async Task<LoteLeiturasResponse> RegistrarLoteAsync(Guid sensorId, RegistrarLoteLeiturasRequest requisicao, CancellationToken ct)
    {
        var sensor = await ObterSensorAtivoAsync(sensorId, ct);
        var agora = Agora();
        var chaves = requisicao.Leituras.Select(l => l.LeituraId.Trim()).Distinct().ToList();
        var jaExistentes = await leituras.ObterChavesExistentesAsync(sensor.Id, chaves, ct);

        var vistasNoLote = new HashSet<string>(StringComparer.Ordinal);
        var rejeitadas = new List<LeituraRejeitada>();
        var duplicadas = 0;
        var novas = new List<LeituraSensor>();

        foreach (var item in requisicao.Leituras)
        {
            var chave = item.LeituraId.Trim();
            if (jaExistentes.Contains(chave) || !vistasNoLote.Add(chave))
            {
                duplicadas++;
                continue;
            }

            try
            {
                novas.Add(CriarLeitura(sensor, item, chave, agora));
            }
            catch (RegraNegocioException ex)
            {
                rejeitadas.Add(new LeituraRejeitada(chave, ex.Message));
            }
        }

        if (novas.Count > 0)
        {
            foreach (var leitura in novas)
            {
                await leituras.AdicionarAsync(leitura, ct);
            }

            sensor.RegistrarComunicacao(agora);
            try
            {
                await unitOfWork.SalvarAsync(ct);
            }
            catch (ConflitoPersistenciaException)
            {
                // Corrida com outro envio: processa uma a uma para descobrir quais já existem.
                unitOfWork.DescartarAlteracoes();
                var registradasIndividualmente = 0;
                foreach (var leitura in novas)
                {
                    var resultado = await RegistrarAsync(sensorId, ParaRequisicao(leitura), ct);
                    if (resultado.Duplicada)
                    {
                        duplicadas++;
                    }
                    else
                    {
                        registradasIndividualmente++;
                    }
                }

                return new LoteLeiturasResponse(requisicao.Leituras.Count, registradasIndividualmente, duplicadas, rejeitadas);
            }
        }

        return new LoteLeiturasResponse(requisicao.Leituras.Count, novas.Count, duplicadas, rejeitadas);
    }

    public async Task<PaginaResponse<LeituraResponse>> ListarPorViaAsync(Guid viaId, ParametrosPaginacao paginacao, CancellationToken ct)
    {
        var pagina = await leituras.ListarPorViaAsync(viaId, paginacao.PaginaAtual, paginacao.ItensPorPagina, ct);
        return PaginaResponse<LeituraResponse>.Criar(
            pagina.Itens.Select(LeituraResponse.De).ToList(),
            paginacao.PaginaAtual,
            paginacao.ItensPorPagina,
            pagina.Total);
    }

    private static LeituraSensor CriarLeitura(Sensor sensor, RegistrarLeituraRequest requisicao, string chave, DateTime agora)
    {
        if (requisicao.ColetadaEm is null || requisicao.VeiculosContados is null || requisicao.TamanhoFila is null)
        {
            throw new RegraNegocioException("LEITURA_INCOMPLETA", "ColetadaEm, VeiculosContados e TamanhoFila são obrigatórios.");
        }

        return new LeituraSensor(
            sensor,
            chave,
            Guarda.Utc(requisicao.ColetadaEm.Value),
            requisicao.VeiculosContados.Value,
            requisicao.TamanhoFila.Value,
            requisicao.VelocidadeMediaKmh,
            agora);
    }

    private static RegistrarLeituraRequest ParaRequisicao(LeituraSensor leitura) => new()
    {
        LeituraId = leitura.LeituraExternaId,
        ColetadaEm = leitura.ColetadaEm,
        VeiculosContados = leitura.VeiculosContados,
        TamanhoFila = leitura.TamanhoFila,
        VelocidadeMediaKmh = leitura.VelocidadeMediaKmh
    };

    private async Task<Sensor> ObterSensorAtivoAsync(Guid sensorId, CancellationToken ct)
    {
        var sensor = await sensores.ObterPorIdAsync(sensorId, ct)
            ?? throw new RecursoNaoEncontradoException("Sensor", sensorId);
        if (!sensor.Ativo)
        {
            throw new RegraNegocioException("SENSOR_INATIVO", "Este sensor está desativado e não pode enviar leituras.", TipoViolacao.AcessoNegado);
        }

        return sensor;
    }

    private DateTime Agora() => relogio.GetUtcNow().UtcDateTime;
}
