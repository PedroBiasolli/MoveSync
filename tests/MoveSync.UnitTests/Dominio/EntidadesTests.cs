using MoveSync.Domain.Comum;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;
using MoveSync.Domain.Servicos;

namespace MoveSync.UnitTests.Dominio;

/// <summary>Invariantes das entidades: o domínio se protege mesmo que a API deixe algo passar.</summary>
public class EntidadesTests
{
    private static readonly DateTime Agora = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    private static Cruzamento NovoCruzamento() => new("tst-01", "Cruzamento Teste", "Rua Teste, 10", -23, -46, 12, Agora);

    // ---------------- Ocorrência ----------------

    [Theory]
    [InlineData(TipoOcorrencia.SemaforoApagado, PrioridadeOcorrencia.Critica)]
    [InlineData(TipoOcorrencia.SemaforoPiscando, PrioridadeOcorrencia.Alta)]
    [InlineData(TipoOcorrencia.TravessiaInsegura, PrioridadeOcorrencia.Alta)]
    [InlineData(TipoOcorrencia.TempoTravessiaInsuficiente, PrioridadeOcorrencia.Media)]
    [InlineData(TipoOcorrencia.SensorDanificado, PrioridadeOcorrencia.Media)]
    [InlineData(TipoOcorrencia.Outro, PrioridadeOcorrencia.Baixa)]
    public void Ocorrencia_PrioridadeDefinidaPeloTipo(TipoOcorrencia tipo, PrioridadeOcorrencia esperada)
    {
        var ocorrencia = new Ocorrencia(Guid.NewGuid(), Guid.NewGuid(), tipo, "Descrição do problema relatado", Agora);

        Assert.Equal(esperada, ocorrencia.Prioridade);
        Assert.Equal(StatusOcorrencia.Aberta, ocorrencia.Status);
    }

    [Theory]
    [InlineData(StatusOcorrencia.Aberta, StatusOcorrencia.EmAnalise, true)]
    [InlineData(StatusOcorrencia.Aberta, StatusOcorrencia.Resolvida, true)]
    [InlineData(StatusOcorrencia.EmAnalise, StatusOcorrencia.Rejeitada, true)]
    [InlineData(StatusOcorrencia.EmAnalise, StatusOcorrencia.Aberta, false)]
    [InlineData(StatusOcorrencia.Resolvida, StatusOcorrencia.EmAnalise, false)]
    [InlineData(StatusOcorrencia.Rejeitada, StatusOcorrencia.Resolvida, false)]
    public void Ocorrencia_MaquinaDeEstados(StatusOcorrencia de, StatusOcorrencia para, bool permitido) =>
        Assert.Equal(permitido, Ocorrencia.TransicaoPermitida(de, para));

    [Fact]
    public void Ocorrencia_ResolverSemResposta_Lanca()
    {
        var ocorrencia = new Ocorrencia(Guid.NewGuid(), Guid.NewGuid(), TipoOcorrencia.Outro, "Descrição do problema relatado", Agora);

        var excecao = Assert.Throws<RegraNegocioException>(() => ocorrencia.AlterarStatus(StatusOcorrencia.Resolvida, " ", Agora));
        Assert.Equal("RESPOSTA_OBRIGATORIA", excecao.Codigo);
    }

    [Fact]
    public void Ocorrencia_TransicaoInvalida_LancaConflito()
    {
        var ocorrencia = new Ocorrencia(Guid.NewGuid(), Guid.NewGuid(), TipoOcorrencia.Outro, "Descrição do problema relatado", Agora);
        ocorrencia.AlterarStatus(StatusOcorrencia.Resolvida, "Semáforo consertado.", Agora);

        var excecao = Assert.Throws<RegraNegocioException>(() => ocorrencia.AlterarStatus(StatusOcorrencia.EmAnalise, null, Agora));
        Assert.Equal(TipoViolacao.Conflito, excecao.Tipo);
    }

    [Fact]
    public void Ocorrencia_EdicaoPorOutroUsuario_AcessoNegado()
    {
        var ocorrencia = new Ocorrencia(Guid.NewGuid(), Guid.NewGuid(), TipoOcorrencia.Outro, "Descrição do problema relatado", Agora);

        var excecao = Assert.Throws<RegraNegocioException>(() =>
            ocorrencia.AtualizarPeloAutor(Guid.NewGuid(), TipoOcorrencia.Outro, "Outra descrição qualquer", Agora));
        Assert.Equal(TipoViolacao.AcessoNegado, excecao.Tipo);
    }

    [Fact]
    public void Ocorrencia_EdicaoAposAnalise_Conflito()
    {
        var autor = Guid.NewGuid();
        var ocorrencia = new Ocorrencia(Guid.NewGuid(), autor, TipoOcorrencia.Outro, "Descrição do problema relatado", Agora);
        ocorrencia.AlterarStatus(StatusOcorrencia.EmAnalise, null, Agora);

        var excecao = Assert.Throws<RegraNegocioException>(() =>
            ocorrencia.AtualizarPeloAutor(autor, TipoOcorrencia.Outro, "Outra descrição qualquer", Agora));
        Assert.Equal("OCORRENCIA_NAO_EDITAVEL", excecao.Codigo);
    }

    [Fact]
    public void Ocorrencia_MudarTipo_RecalculaPrioridade()
    {
        var autor = Guid.NewGuid();
        var ocorrencia = new Ocorrencia(Guid.NewGuid(), autor, TipoOcorrencia.Outro, "Descrição do problema relatado", Agora);

        ocorrencia.AtualizarPeloAutor(autor, TipoOcorrencia.SemaforoApagado, "O semáforo apagou de vez", Agora);

        Assert.Equal(PrioridadeOcorrencia.Critica, ocorrencia.Prioridade);
    }

    // ---------------- Leitura de sensor ----------------

    private static Sensor NovoSensor() => new("SNS-01", Guid.NewGuid(), Guid.NewGuid(), TipoSensor.Camera, "hash", Agora);

    [Fact]
    public void Leitura_ComDataNoFuturo_Lanca()
    {
        var excecao = Assert.Throws<RegraNegocioException>(() =>
            new LeituraSensor(NovoSensor(), "L1", Agora.AddMinutes(10), 10, 2, null, Agora));
        Assert.Equal("LEITURA_NO_FUTURO", excecao.Codigo);
    }

    [Fact]
    public void Leitura_ComMaisDe24Horas_Lanca()
    {
        var excecao = Assert.Throws<RegraNegocioException>(() =>
            new LeituraSensor(NovoSensor(), "L1", Agora.AddHours(-25), 10, 2, null, Agora));
        Assert.Equal("LEITURA_EXPIRADA", excecao.Codigo);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(501, 0)]
    [InlineData(10, 201)]
    public void Leitura_ValoresForaDaFaixa_Lanca(int veiculos, int fila) =>
        Assert.Throws<RegraNegocioException>(() => new LeituraSensor(NovoSensor(), "L1", Agora, veiculos, fila, null, Agora));

    [Fact]
    public void Leitura_Valida_HerdaViaECruzamentoDoSensor()
    {
        var sensor = NovoSensor();

        var leitura = new LeituraSensor(sensor, "  L-77 ", Agora.AddSeconds(-30), 12, 3, 35.5, Agora);

        Assert.Equal(sensor.ViaId, leitura.ViaId);
        Assert.Equal(sensor.CruzamentoId, leitura.CruzamentoId);
        Assert.Equal("L-77", leitura.LeituraExternaId);
    }

    // ---------------- Sensor ----------------

    [Fact]
    public void Sensor_SemLeituraDentroDaTolerancia_EstaOffline()
    {
        var sensor = NovoSensor();
        Assert.True(sensor.EstaOffline(Agora, TimeSpan.FromMinutes(5)));

        sensor.RegistrarComunicacao(Agora);
        Assert.False(sensor.EstaOffline(Agora.AddMinutes(4), TimeSpan.FromMinutes(5)));
        Assert.True(sensor.EstaOffline(Agora.AddMinutes(6), TimeSpan.FromMinutes(5)));
    }

    // ---------------- Cruzamento e vias ----------------

    [Fact]
    public void Cruzamento_CodigoNormalizadoEmMaiusculas() => Assert.Equal("TST-01", NovoCruzamento().Codigo);

    [Fact]
    public void Cruzamento_CodigoComCaractereInvalido_Lanca() =>
        Assert.Throws<RegraNegocioException>(() => new Cruzamento("TST 01!", "Nome válido", "Endereço válido", 0, 0, 10, Agora));

    [Fact]
    public void Cruzamento_ViaComNomeRepetido_Conflito()
    {
        var cruzamento = NovoCruzamento();
        cruzamento.AdicionarVia("Av. Principal", "N → S", 2, 50, Agora);

        var excecao = Assert.Throws<RegraNegocioException>(() => cruzamento.AdicionarVia("av. principal", "S → N", 2, 50, Agora));
        Assert.Equal("VIA_DUPLICADA", excecao.Codigo);
    }

    [Fact]
    public void Cruzamento_MaisDeSeisVias_Conflito()
    {
        var cruzamento = NovoCruzamento();
        for (var i = 1; i <= Cruzamento.MaximoVias; i++)
        {
            cruzamento.AdicionarVia($"Via {i}", "Sentido", 1, 40, Agora);
        }

        var excecao = Assert.Throws<RegraNegocioException>(() => cruzamento.AdicionarVia("Via 7", "Sentido", 1, 40, Agora));
        Assert.Equal("LIMITE_VIAS", excecao.Codigo);
    }

    [Theory]
    [InlineData(0, 40)]
    [InlineData(7, 40)]
    [InlineData(2, 10)]
    [InlineData(2, 100)]
    public void Via_FaixasOuVelocidadeInvalidas_Lanca(int faixas, int velocidade) =>
        Assert.Throws<RegraNegocioException>(() => NovoCruzamento().AdicionarVia("Via", "Sentido", faixas, velocidade, Agora));

    // ---------------- Emergência e travessia ----------------

    [Theory]
    [InlineData(29)]
    [InlineData(601)]
    public void Emergencia_DuracaoForaDoIntervalo_Lanca(int duracao) =>
        Assert.Throws<RegraNegocioException>(() =>
            new AcionamentoEmergencia(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TipoVeiculoEmergencia.Ambulancia, duracao, Agora));

    [Fact]
    public void Emergencia_ExpiraSozinhaENaoPodeSerEncerradaDuasVezes()
    {
        var emergencia = new AcionamentoEmergencia(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TipoVeiculoEmergencia.Bombeiros, 60, Agora);
        Assert.True(emergencia.EstaAtivo(Agora.AddSeconds(59)));
        Assert.False(emergencia.EstaAtivo(Agora.AddSeconds(61)));

        emergencia.Encerrar(Agora.AddSeconds(10));
        Assert.Throws<RegraNegocioException>(() => emergencia.Encerrar(Agora.AddSeconds(20)));
    }

    [Fact]
    public void TravessiaAcessivel_ValeCincoMinutos()
    {
        var solicitacao = new SolicitacaoTravessia(Guid.NewGuid(), Guid.NewGuid(), MotivoTravessiaAcessivel.PessoaIdosa, Agora);

        Assert.True(solicitacao.EstaVigente(Agora.AddMinutes(4)));
        Assert.False(solicitacao.EstaVigente(Agora.AddMinutes(5)));
    }

    // ---------------- Plano semafórico ----------------

    [Fact]
    public void Plano_EquivalenteDentroDaTolerancia_NaoGeraNovoRegistro()
    {
        var via1 = Guid.NewGuid();
        var via2 = Guid.NewGuid();
        PlanoCalculado Plano(int verde1) => new(
            ModoPlano.Adaptativo,
            80,
            14,
            false,
            [new FaseCalculada(via1, "A", verde1, 4, 2, 500, 0.2), new FaseCalculada(via2, "B", 20, 3, 2, 100, 0.1)],
            "teste");

        var gravado = new PlanoSemaforico(Guid.NewGuid(), Plano(40), Agora);

        Assert.True(gravado.EquivalenteA(Plano(42), toleranciaSegundos: 2));
        Assert.False(gravado.EquivalenteA(Plano(43), toleranciaSegundos: 2));
        Assert.False(gravado.EquivalenteA(Plano(40) with { Modo = ModoPlano.Emergencia }, toleranciaSegundos: 2));
    }
}
