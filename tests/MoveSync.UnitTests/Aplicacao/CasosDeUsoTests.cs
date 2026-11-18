using MoveSync.Application.Contratos;
using MoveSync.Application.Excecoes;
using MoveSync.Domain.Comum;
using MoveSync.Domain.Enums;
using MoveSync.UnitTests.Fakes;

namespace MoveSync.UnitTests.Aplicacao;

/// <summary>Casos de uso (camada Application) com repositórios em memória.</summary>
public class CasosDeUsoTests
{
    // ---------------- Telemetria: idempotência pela chave natural ----------------

    [Fact]
    public async Task Telemetria_MesmaLeituraEnviadaDuasVezes_GravaUmaUnicaVez()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        var sensor = cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "Av. Teste"), "SNS-A");
        var servico = cenario.TelemetriaService();
        var leitura = new RegistrarLeituraRequest { LeituraId = "esp32-0001", ColetadaEm = cenario.Agora, VeiculosContados = 12, TamanhoFila = 4 };

        var primeira = await servico.RegistrarAsync(sensor.Id, leitura, CancellationToken.None);
        var segunda = await servico.RegistrarAsync(sensor.Id, leitura, CancellationToken.None);

        Assert.False(primeira.Duplicada);
        Assert.True(segunda.Duplicada);
        Assert.Equal(primeira.Leitura.Id, segunda.Leitura.Id);
        Assert.Single(cenario.Banco.Leituras);
    }

    [Fact]
    public async Task Telemetria_SensorInativo_AcessoNegado()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        var sensor = cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "Av. Teste"), "SNS-A", ativo: false);
        var leitura = new RegistrarLeituraRequest { LeituraId = "x1", ColetadaEm = cenario.Agora, VeiculosContados = 1, TamanhoFila = 0 };

        var excecao = await Assert.ThrowsAsync<RegraNegocioException>(() =>
            cenario.TelemetriaService().RegistrarAsync(sensor.Id, leitura, CancellationToken.None));
        Assert.Equal(TipoViolacao.AcessoNegado, excecao.Tipo);
    }

    [Fact]
    public async Task Telemetria_Lote_IgnoraDuplicadasERejeitaInvalidasSemDerrubarOLote()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        var sensor = cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "Av. Teste"), "SNS-A");
        var servico = cenario.TelemetriaService();
        await servico.RegistrarAsync(sensor.Id, new RegistrarLeituraRequest { LeituraId = "L1", ColetadaEm = cenario.Agora, VeiculosContados = 5, TamanhoFila = 1 }, CancellationToken.None);

        var lote = new RegistrarLoteLeiturasRequest
        {
            Leituras =
            [
                new() { LeituraId = "L1", ColetadaEm = cenario.Agora, VeiculosContados = 5, TamanhoFila = 1 },                 // já existia
                new() { LeituraId = "L2", ColetadaEm = cenario.Agora, VeiculosContados = 7, TamanhoFila = 2 },                 // nova
                new() { LeituraId = "L2", ColetadaEm = cenario.Agora, VeiculosContados = 7, TamanhoFila = 2 },                 // repetida no lote
                new() { LeituraId = "L3", ColetadaEm = cenario.Agora.AddHours(1), VeiculosContados = 3, TamanhoFila = 0 }      // futuro: inválida
            ]
        };

        var resultado = await servico.RegistrarLoteAsync(sensor.Id, lote, CancellationToken.None);

        Assert.Equal(4, resultado.Recebidas);
        Assert.Equal(1, resultado.Registradas);
        Assert.Equal(2, resultado.Duplicadas);
        Assert.Equal("L3", Assert.Single(resultado.Rejeitadas).LeituraId);
        Assert.Equal(2, cenario.Banco.Leituras.Count);
    }

    // ---------------- Autenticação ----------------

    [Fact]
    public async Task Registro_SempreCriaPerfilCidadao()
    {
        var cenario = new Cenario();

        var usuario = await cenario.AutenticacaoService().RegistrarAsync(
            new RegistrarUsuarioRequest { Nome = "Maria Silva", Email = "  MARIA@Email.com ", Senha = "Senha123" },
            CancellationToken.None);

        Assert.Equal(PerfilUsuario.Cidadao, usuario.Perfil);
        Assert.Equal("maria@email.com", usuario.Email);
    }

    [Fact]
    public async Task Registro_EmailDuplicado_Conflito()
    {
        var cenario = new Cenario();
        cenario.CriarUsuario(PerfilUsuario.Cidadao, "joao@email.com");

        var excecao = await Assert.ThrowsAsync<RegraNegocioException>(() => cenario.AutenticacaoService().RegistrarAsync(
            new RegistrarUsuarioRequest { Nome = "João", Email = "joao@email.com", Senha = "Senha123" },
            CancellationToken.None));
        Assert.Equal(TipoViolacao.Conflito, excecao.Tipo);
    }

    [Theory]
    [InlineData("gestor@email.com", "SenhaErrada1")]
    [InlineData("naoexiste@email.com", "Senha@123")]
    public async Task Login_CredenciaisInvalidas_MensagemGenerica(string email, string senha)
    {
        var cenario = new Cenario();
        cenario.CriarUsuario(PerfilUsuario.Gestor, "gestor@email.com");

        await Assert.ThrowsAsync<CredenciaisInvalidasException>(() =>
            cenario.AutenticacaoService().LoginAsync(new LoginRequest { Email = email, Senha = senha }, CancellationToken.None));
    }

    [Fact]
    public async Task Login_Valido_RetornaTokenComPerfil()
    {
        var cenario = new Cenario();
        cenario.CriarUsuario(PerfilUsuario.Gestor, "gestor@email.com");

        var token = await cenario.AutenticacaoService().LoginAsync(new LoginRequest { Email = "gestor@email.com", Senha = "Senha@123" }, CancellationToken.None);

        Assert.Equal("Gestor", token.Perfil);
        Assert.Equal("Bearer", token.TokenType);
    }

    // ---------------- Motor semafórico (orquestração) ----------------

    [Fact]
    public async Task Plano_SemTelemetria_EntraEmContingencia()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();

        var plano = await cenario.PlanoService().RecalcularAsync(cruzamento.Id, CancellationToken.None);

        Assert.Equal(ModoPlano.Contingencia, plano.Modo);
        Assert.Contains(cenario.Notificador.Eventos, e => e.Tipo == "CRUZAMENTO_EM_CONTINGENCIA");
    }

    [Fact]
    public async Task Plano_ComTelemetriaEmTodasAsVias_FicaAdaptativo()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        cenario.RegistrarLeitura(cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "Av. Teste"), "SNS-A"), veiculos: 90, fila: 10);
        cenario.RegistrarLeitura(cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "R. Exemplo"), "SNS-B"), veiculos: 8, fila: 1);

        var plano = await cenario.PlanoService().RecalcularAsync(cruzamento.Id, CancellationToken.None);

        Assert.Equal(ModoPlano.Adaptativo, plano.Modo);
        Assert.True(plano.Fases.Single(f => f.NomeVia == "Av. Teste").VerdeSegundos > plano.Fases.Single(f => f.NomeVia == "R. Exemplo").VerdeSegundos);
    }

    [Fact]
    public async Task Plano_LeiturasAntigasForaDaJanela_SaoIgnoradas()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        cenario.RegistrarLeitura(cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "Av. Teste"), "SNS-A"), 90, 10, atras: TimeSpan.FromMinutes(10));
        cenario.RegistrarLeitura(cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "R. Exemplo"), "SNS-B"), 8, 1);

        var plano = await cenario.PlanoService().RecalcularAsync(cruzamento.Id, CancellationToken.None);

        Assert.Equal(ModoPlano.Contingencia, plano.Modo);
    }

    [Fact]
    public async Task Plano_RecalcularComMesmosDados_NaoCriaNovoRegistro()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        var servico = cenario.PlanoService();

        var primeiro = await servico.RecalcularAsync(cruzamento.Id, CancellationToken.None);
        var segundo = await servico.RecalcularAsync(cruzamento.Id, CancellationToken.None);

        Assert.Equal(primeiro.Id, segundo.Id);
        Assert.Single(cenario.Banco.Planos);
    }

    [Fact]
    public async Task Status_CidadaoVeEsperaEstimadaPorViaSemAlterarDados()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        cenario.RegistrarLeitura(cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "Av. Teste"), "SNS-A"), 60, 5);
        cenario.RegistrarLeitura(cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "R. Exemplo"), "SNS-B"), 10, 1);

        var status = await cenario.PlanoService().ObterStatusAsync(cruzamento.Id, CancellationToken.None);

        Assert.Equal(2, status.Vias.Count);
        Assert.True(status.PlanoProvisorio);
        Assert.All(status.Vias, v => Assert.True(v.EsperaMediaSegundos >= 0));
        Assert.Empty(cenario.Banco.Planos);
    }

    [Fact]
    public async Task Status_CruzamentoInexistente_NaoEncontrado() =>
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => new Cenario().PlanoService().ObterStatusAsync(Guid.NewGuid(), CancellationToken.None));

    // ---------------- Prioridades ----------------

    [Fact]
    public async Task Emergencia_AplicaCorredorVerdeENotificaNucleo()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        var gestor = cenario.CriarUsuario(PerfilUsuario.Gestor, "gestor@email.com");
        var rua = cenario.Via(cruzamento, "R. Exemplo");

        var resposta = await cenario.PrioridadeService().AcionarEmergenciaAsync(
            cruzamento.Id,
            gestor.Id,
            new AcionarEmergenciaRequest { ViaId = rua.Id, TipoVeiculo = TipoVeiculoEmergencia.Ambulancia, DuracaoSegundos = 120 },
            CancellationToken.None);

        Assert.True(resposta.Ativa);
        Assert.Equal(ModoPlano.Emergencia, resposta.PlanoAplicado!.Modo);
        Assert.Equal(90, resposta.PlanoAplicado.Fases.Single(f => f.ViaId == rua.Id).VerdeSegundos);
        Assert.Contains(cenario.Notificador.Eventos, e => e.Tipo == "EMERGENCIA_ACIONADA");
    }

    [Fact]
    public async Task Emergencia_SegundoAcionamentoSimultaneo_Conflito()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        var gestor = cenario.CriarUsuario(PerfilUsuario.Gestor, "gestor@email.com");
        var requisicao = new AcionarEmergenciaRequest { ViaId = cenario.Via(cruzamento, "Av. Teste").Id, TipoVeiculo = TipoVeiculoEmergencia.Bombeiros };
        var servico = cenario.PrioridadeService();
        await servico.AcionarEmergenciaAsync(cruzamento.Id, gestor.Id, requisicao, CancellationToken.None);

        var excecao = await Assert.ThrowsAsync<RegraNegocioException>(() =>
            servico.AcionarEmergenciaAsync(cruzamento.Id, gestor.Id, requisicao, CancellationToken.None));
        Assert.Equal("EMERGENCIA_JA_ATIVA", excecao.Codigo);
    }

    [Fact]
    public async Task TravessiaAcessivel_SegundoPedidoDoMesmoCidadao_DevolveOMesmo()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        var cidada = cenario.CriarUsuario(PerfilUsuario.Cidadao, "ana@email.com");
        var servico = cenario.PrioridadeService();
        var requisicao = new SolicitarTravessiaRequest { Motivo = MotivoTravessiaAcessivel.MobilidadeReduzida };

        var primeira = await servico.SolicitarTravessiaAsync(cruzamento.Id, cidada.Id, requisicao, CancellationToken.None);
        var segunda = await servico.SolicitarTravessiaAsync(cruzamento.Id, cidada.Id, requisicao, CancellationToken.None);

        Assert.True(primeira.Criada);
        Assert.False(segunda.Criada);
        Assert.Equal(primeira.Solicitacao.Id, segunda.Solicitacao.Id);
        Assert.True(primeira.Solicitacao.TempoTravessiaSegundos > primeira.Solicitacao.TempoTravessiaPadraoSegundos);
        Assert.Single(cenario.Banco.Travessias);
    }

    // ---------------- Ocorrências: privacidade ----------------

    [Fact]
    public async Task Ocorrencia_CidadaoNaoEnxergaOcorrenciaDeOutro_GestorEnxerga()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        var autora = cenario.CriarUsuario(PerfilUsuario.Cidadao, "autora@email.com");
        var outro = cenario.CriarUsuario(PerfilUsuario.Cidadao, "outro@email.com");
        var gestor = cenario.CriarUsuario(PerfilUsuario.Gestor, "gestor@email.com");
        var servico = cenario.OcorrenciaService();
        var criada = await servico.CriarAsync(
            autora.Id,
            new CriarOcorrenciaRequest { CruzamentoId = cruzamento.Id, Tipo = TipoOcorrencia.SemaforoApagado, Descricao = "Semáforo apagado desde cedo" },
            CancellationToken.None);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.ObterAsync(criada.Id, outro.Id, false, CancellationToken.None));
        var vistaPeloGestor = await servico.ObterAsync(criada.Id, gestor.Id, true, CancellationToken.None);

        Assert.Equal(PrioridadeOcorrencia.Critica, vistaPeloGestor.Prioridade);
        Assert.Contains(cenario.Notificador.Eventos, e => e.Tipo == "OCORRENCIA_CRITICA");
    }

    [Fact]
    public async Task Cruzamento_ComOcorrencias_NaoPodeSerExcluido()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        var cidada = cenario.CriarUsuario(PerfilUsuario.Cidadao, "ana@email.com");
        await cenario.OcorrenciaService().CriarAsync(
            cidada.Id,
            new CriarOcorrenciaRequest { CruzamentoId = cruzamento.Id, Tipo = TipoOcorrencia.Outro, Descricao = "Descrição do problema relatado" },
            CancellationToken.None);

        var excecao = await Assert.ThrowsAsync<RegraNegocioException>(() => cenario.CruzamentoService().RemoverAsync(cruzamento.Id, CancellationToken.None));

        Assert.Equal("CRUZAMENTO_COM_OCORRENCIAS", excecao.Codigo);
        Assert.Single(cenario.Banco.Cruzamentos);
    }
}

/// <summary>Relatório de impacto para a prefeitura (modelo de negócio dentro da API).</summary>
public class ImpactoServiceTests
{
    [Fact]
    public async Task Impacto_ComTelemetria_SomaGanhoMedidoEProjetaBeneficioCusto()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        cenario.RegistrarLeitura(cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "Av. Teste"), "SNS-A"), veiculos: 90, fila: 12);
        cenario.RegistrarLeitura(cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "R. Exemplo"), "SNS-B"), veiculos: 6, fila: 1);
        cenario.CriarCruzamento("TST-002"); // segundo cruzamento contratado, ainda sem sensores

        var relatorio = await cenario.ImpactoService().GerarAsync(new ParametrosImpacto(), CancellationToken.None);

        Assert.Equal(2, relatorio.CruzamentosContratados);
        Assert.Equal(1, relatorio.CruzamentosComDados);
        Assert.True(relatorio.Resumo.HorasEvitadasPorMes > 0);
        Assert.True(relatorio.Resumo.ValorTempoEconomizadoMesReais > 0);
        Assert.Equal(2 * 590m, relatorio.Resumo.AssinaturaMensalReais);
        Assert.True(relatorio.Resumo.RelacaoBeneficioCusto.HasValue);
        Assert.False(relatorio.Cruzamentos.Single(c => c.Codigo == "TST-002").DadosSuficientes);
        Assert.Contains("1 de 2", relatorio.Interpretacao);
    }

    [Fact]
    public async Task Impacto_SimulacaoDeCenario_SobrescrevePremissasSemAlterarConfiguracao()
    {
        var cenario = new Cenario();
        var cruzamento = cenario.CriarCruzamento();
        cenario.RegistrarLeitura(cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "Av. Teste"), "SNS-A"), 90, 12);
        cenario.RegistrarLeitura(cenario.CriarSensor(cruzamento, cenario.Via(cruzamento, "R. Exemplo"), "SNS-B"), 6, 1);
        var servico = cenario.ImpactoService();

        var padrao = await servico.GerarAsync(new ParametrosImpacto(), CancellationToken.None);
        var simulado = await servico.GerarAsync(new ParametrosImpacto { HorasReferenciaPorDia = 20, AssinaturaMensalPorCruzamentoReais = 0m }, CancellationToken.None);

        Assert.Equal(20, simulado.Premissas.HorasReferenciaPorDia);
        Assert.InRange(simulado.Resumo.HorasEvitadasPorMes, padrao.Resumo.HorasEvitadasPorMes * 2 - 1, padrao.Resumo.HorasEvitadasPorMes * 2 + 1); // dobra (±1 h de arredondamento)
        Assert.Null(simulado.Resumo.RelacaoBeneficioCusto); // piloto gratuito
        Assert.Equal(10, padrao.Premissas.HorasReferenciaPorDia);
    }

    [Fact]
    public async Task Impacto_SemTelemetria_InformaQueFaltamDadosSemInventarGanho()
    {
        var cenario = new Cenario();
        cenario.CriarCruzamento();

        var relatorio = await cenario.ImpactoService().GerarAsync(new ParametrosImpacto(), CancellationToken.None);

        Assert.Equal(0, relatorio.CruzamentosComDados);
        Assert.Equal(0, relatorio.Resumo.HorasEvitadasPorMes);
        Assert.Null(relatorio.Resumo.RelacaoBeneficioCusto);
        Assert.Contains("telemetria", relatorio.Interpretacao);
    }
}
