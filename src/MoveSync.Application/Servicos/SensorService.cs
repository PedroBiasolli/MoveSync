using System.Security.Cryptography;
using MoveSync.Application.Abstracoes;
using MoveSync.Application.Contratos;
using MoveSync.Application.Excecoes;
using MoveSync.Domain.Comum;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Servicos;

namespace MoveSync.Application.Servicos;

/// <summary>Cadastro dos dispositivos IoT. O segredo é mostrado UMA única vez, na criação.</summary>
public sealed class SensorService(
    ISensorRepository sensores,
    ICruzamentoRepository cruzamentos,
    IHashSenha hashSenha,
    IUnitOfWork unitOfWork,
    ParametrosSemaforicos parametros,
    TimeProvider relogio)
{
    private TimeSpan ToleranciaOffline => TimeSpan.FromMinutes(parametros.JanelaDadosMinutos);

    public async Task<IReadOnlyList<SensorResponse>> ListarPorCruzamentoAsync(Guid cruzamentoId, CancellationToken ct)
    {
        _ = await cruzamentos.ObterPorIdAsync(cruzamentoId, ct)
            ?? throw new RecursoNaoEncontradoException("Cruzamento", cruzamentoId);
        var agora = Agora();
        var lista = await sensores.ListarAsync(cruzamentoId, ct);
        return lista.Select(s => SensorResponse.De(s, agora, ToleranciaOffline)).ToList();
    }

    public async Task<SensorCriadoResponse> CriarAsync(Guid cruzamentoId, CriarSensorRequest requisicao, CancellationToken ct)
    {
        var cruzamento = await cruzamentos.ObterPorIdAsync(cruzamentoId, ct)
            ?? throw new RecursoNaoEncontradoException("Cruzamento", cruzamentoId);
        var via = cruzamento.ObterVia(requisicao.ViaId!.Value);

        var codigo = Cruzamento.NormalizarCodigo(requisicao.Codigo);
        if (await sensores.CodigoExisteAsync(codigo, ct))
        {
            throw new RegraNegocioException("CODIGO_SENSOR_DUPLICADO", $"Já existe um sensor com o código '{codigo}'.", TipoViolacao.Conflito);
        }

        if (await sensores.ExisteParaViaAsync(via.Id, ct))
        {
            throw new RegraNegocioException(
                "VIA_JA_MONITORADA",
                "Esta via já possui um sensor. Use um sensor por via para não contar veículos em dobro.",
                TipoViolacao.Conflito);
        }

        var segredo = GerarSegredo();
        var agora = Agora();
        var sensor = new Sensor(codigo, cruzamento.Id, via.Id, requisicao.Tipo!.Value, hashSenha.Gerar(segredo), agora);
        await sensores.AdicionarAsync(sensor, ct);
        try
        {
            await unitOfWork.SalvarAsync(ct);
        }
        catch (ConflitoPersistenciaException)
        {
            throw new RegraNegocioException("CODIGO_SENSOR_DUPLICADO", $"Já existe um sensor com o código '{codigo}'.", TipoViolacao.Conflito);
        }

        return new SensorCriadoResponse(
            SensorResponse.De(sensor, agora, ToleranciaOffline),
            segredo,
            "Guarde este segredo e grave-o no dispositivo: ele não será exibido novamente.");
    }

    public async Task<SensorResponse> AlterarAtivoAsync(Guid sensorId, bool ativo, CancellationToken ct)
    {
        var sensor = await ObterAsync(sensorId, ct);
        sensor.DefinirAtivo(ativo);
        await unitOfWork.SalvarAsync(ct);
        return SensorResponse.De(sensor, Agora(), ToleranciaOffline);
    }

    public async Task RemoverAsync(Guid sensorId, CancellationToken ct)
    {
        var sensor = await ObterAsync(sensorId, ct);
        sensores.Remover(sensor);
        await unitOfWork.SalvarAsync(ct);
    }

    private async Task<Sensor> ObterAsync(Guid sensorId, CancellationToken ct) =>
        await sensores.ObterPorIdAsync(sensorId, ct) ?? throw new RecursoNaoEncontradoException("Sensor", sensorId);

    private static string GerarSegredo() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private DateTime Agora() => relogio.GetUtcNow().UtcDateTime;
}
