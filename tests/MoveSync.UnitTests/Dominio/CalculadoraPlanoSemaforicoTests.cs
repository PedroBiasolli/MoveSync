using MoveSync.Domain.Comum;
using MoveSync.Domain.Enums;
using MoveSync.Domain.Servicos;

namespace MoveSync.UnitTests.Dominio;

/// <summary>Regras do motor semafórico (o coração da MoveSync).</summary>
public class CalculadoraPlanoSemaforicoTests
{
    private static readonly Guid Avenida = Guid.NewGuid();
    private static readonly Guid Rua = Guid.NewGuid();
    private readonly CalculadoraPlanoSemaforico _calculadora = new(new ParametrosSemaforicos());

    private static EntradaCalculoPlano Entrada(
        int veiculosAvenida,
        int veiculosRua,
        bool dadosAvenida = true,
        bool dadosRua = true,
        ModoOperacao modo = ModoOperacao.Adaptativo,
        bool acessivel = false,
        Guid? emergencia = null,
        int filaAvenida = 0,
        int filaRua = 0) =>
        new(
            modo,
            12,
            [
                new DemandaVia(Avenida, "Avenida", 3, 50, veiculosAvenida, filaAvenida, dadosAvenida),
                new DemandaVia(Rua, "Rua", 1, 40, veiculosRua, filaRua, dadosRua)
            ],
            acessivel,
            emergencia);

    [Fact]
    public void Adaptativo_ViaComMaisDemandaRecebeMaisVerde()
    {
        var plano = _calculadora.Calcular(Entrada(veiculosAvenida: 120, veiculosRua: 10));

        var verdeAvenida = plano.Fases.Single(f => f.ViaId == Avenida).VerdeSegundos;
        var verdeRua = plano.Fases.Single(f => f.ViaId == Rua).VerdeSegundos;
        Assert.Equal(ModoPlano.Adaptativo, plano.Modo);
        Assert.True(verdeAvenida > verdeRua, $"Avenida ({verdeAvenida}s) deveria ter mais verde que a rua ({verdeRua}s).");
    }

    [Fact]
    public void Adaptativo_NuncaUltrapassaLimitesDeSeguranca()
    {
        var parametros = new ParametrosSemaforicos();
        var plano = _calculadora.Calcular(Entrada(veiculosAvenida: 500, veiculosRua: 1, filaAvenida: 200));

        Assert.All(plano.Fases, fase =>
        {
            Assert.InRange(fase.VerdeSegundos, parametros.VerdeMinimoSegundos, parametros.VerdeMaximoSegundos);
            Assert.InRange(fase.AmareloSegundos, 3, 5);
            Assert.Equal(parametros.VermelhoGeralSegundos, fase.VermelhoGeralSegundos);
        });
        Assert.Equal(parametros.VerdeMinimoSegundos, plano.Fases.Single(f => f.ViaId == Rua).VerdeSegundos);
    }

    [Fact]
    public void ViaSemDadosRecentes_EntraEmContingenciaComTempoFixoSeguro()
    {
        var plano = _calculadora.Calcular(Entrada(veiculosAvenida: 120, veiculosRua: 0, dadosRua: false));

        Assert.Equal(ModoPlano.Contingencia, plano.Modo);
        Assert.All(plano.Fases, f => Assert.Equal(30, f.VerdeSegundos));
        Assert.Contains("contingência", plano.Justificativa, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Rua", plano.Justificativa, StringComparison.Ordinal);
    }

    [Fact]
    public void ModoTempoFixoDoGestor_IgnoraDemanda()
    {
        var plano = _calculadora.Calcular(Entrada(veiculosAvenida: 300, veiculosRua: 5, modo: ModoOperacao.TempoFixo));

        Assert.Equal(ModoPlano.TempoFixo, plano.Modo);
        Assert.All(plano.Fases, f => Assert.Equal(30, f.VerdeSegundos));
    }

    [Fact]
    public void Emergencia_ViaDoVeiculoRecebeVerdeMaximoEDemaisOMinimo()
    {
        var plano = _calculadora.Calcular(Entrada(veiculosAvenida: 200, veiculosRua: 5, emergencia: Rua));

        Assert.Equal(ModoPlano.Emergencia, plano.Modo);
        Assert.Equal(90, plano.Fases.Single(f => f.ViaId == Rua).VerdeSegundos);
        Assert.Equal(15, plano.Fases.Single(f => f.ViaId == Avenida).VerdeSegundos);
    }

    [Fact]
    public void Emergencia_TemPrioridadeSobreFaltaDeDados()
    {
        var plano = _calculadora.Calcular(Entrada(0, 0, dadosAvenida: false, dadosRua: false, emergencia: Avenida));

        Assert.Equal(ModoPlano.Emergencia, plano.Modo);
    }

    [Fact]
    public void Emergencia_ComViaQueNaoPertenceAoCruzamento_Lanca()
    {
        var excecao = Assert.Throws<RegraNegocioException>(() => _calculadora.Calcular(Entrada(10, 10, emergencia: Guid.NewGuid())));

        Assert.Equal("VIA_EMERGENCIA_INVALIDA", excecao.Codigo);
    }

    [Fact]
    public void TravessiaAcessivel_AmpliaAFaseDePedestres()
    {
        var padrao = _calculadora.Calcular(Entrada(50, 50));
        var acessivel = _calculadora.Calcular(Entrada(50, 50, acessivel: true));

        // 12 m: ceil(12/1,2)+4 = 14 s (padrão) e ceil(12/0,8)+4 = 19 s (acessível)
        Assert.Equal(14, padrao.TempoTravessiaPedestreSegundos);
        Assert.Equal(19, acessivel.TempoTravessiaPedestreSegundos);
        Assert.True(acessivel.TravessiaAcessivel);
    }

    [Fact]
    public void SemVeiculosDetectados_AplicaVerdeMinimoParaReduzirEspera()
    {
        var plano = _calculadora.Calcular(Entrada(0, 0));

        Assert.Equal(ModoPlano.Adaptativo, plano.Modo);
        Assert.All(plano.Fases, f => Assert.Equal(15, f.VerdeSegundos));
    }

    [Fact]
    public void Ciclo_EhASomaDeTodasAsFasesMaisAFaseDePedestres()
    {
        var plano = _calculadora.Calcular(Entrada(80, 30));

        var soma = plano.Fases.Sum(f => f.VerdeSegundos + f.AmareloSegundos + f.VermelhoGeralSegundos) + plano.TempoTravessiaPedestreSegundos;
        Assert.Equal(soma, plano.CicloSegundos);
    }

    [Fact]
    public void FilaAcumulada_AumentaODemandaDaVia()
    {
        var semFila = _calculadora.Calcular(Entrada(40, 40));
        var comFila = _calculadora.Calcular(Entrada(40, 40, filaRua: 30));

        Assert.True(
            comFila.Fases.Single(f => f.ViaId == Rua).VerdeSegundos > semFila.Fases.Single(f => f.ViaId == Rua).VerdeSegundos,
            "A fila acumulada deveria aumentar o verde da via.");
    }

    [Fact]
    public void OcupacaoCritica_LevaCicloAoMaximoEInformaNaJustificativa()
    {
        var plano = _calculadora.Calcular(Entrada(veiculosAvenida: 400, veiculosRua: 150));

        Assert.Contains("crítica", plano.Justificativa, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MenosDeDuasVias_Lanca()
    {
        var entrada = new EntradaCalculoPlano(ModoOperacao.Adaptativo, 10, [new DemandaVia(Avenida, "Avenida", 2, 50, 10, 0, true)]);

        var excecao = Assert.Throws<RegraNegocioException>(() => _calculadora.Calcular(entrada));
        Assert.Equal("VIAS_INSUFICIENTES", excecao.Codigo);
    }

    [Theory]
    [InlineData(30, 3)]
    [InlineData(40, 3)]
    [InlineData(50, 4)]
    [InlineData(60, 4)]
    [InlineData(70, 5)]
    [InlineData(80, 5)]
    public void Amarelo_DependeDaVelocidadeDaVia(int velocidadeKmh, int amareloEsperado) =>
        Assert.Equal(amareloEsperado, ParametrosSemaforicos.TempoAmareloSegundos(velocidadeKmh));

    [Fact]
    public void Justificativa_ExplicaADecisaoEmLinguagemSimples()
    {
        var plano = _calculadora.Calcular(Entrada(100, 20));

        Assert.Contains("Webster", plano.Justificativa, StringComparison.Ordinal);
        Assert.Contains("Avenida", plano.Justificativa, StringComparison.Ordinal);
        Assert.Contains("de verde", plano.Justificativa, StringComparison.Ordinal);
    }
}
