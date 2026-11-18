using MoveSync.Domain.Enums;
using MoveSync.Domain.Servicos;

namespace MoveSync.UnitTests.Dominio;

/// <summary>Estimativa de espera para o cidadão e medição de impacto (tempo fixo × adaptativo).</summary>
public class AnalisadorDesempenhoTests
{
    [Fact]
    public void EstimarEspera_UsaFormulaDeChegadasUniformes()
    {
        // Ciclo 100 s, verde 40 s → vermelho 60 s; espera média = 60²/(2·100) = 18 s
        var estimativa = AnalisadorDesempenho.EstimarEspera(100, 40, 300, 1800, possuiDados: true);

        Assert.Equal(18, estimativa.EsperaMediaSegundos);
        Assert.Equal(60, estimativa.EsperaMaximaSegundos);
    }

    [Theory]
    [InlineData(300, NivelCongestionamento.Livre)]      // x = 300 / (1800·0,5) = 0,33
    [InlineData(700, NivelCongestionamento.Moderado)]   // x = 0,78
    [InlineData(850, NivelCongestionamento.Intenso)]    // x = 0,94
    public void EstimarEspera_ClassificaCongestionamentoPeloGrauDeSaturacao(double fluxo, NivelCongestionamento esperado)
    {
        var estimativa = AnalisadorDesempenho.EstimarEspera(100, 50, fluxo, 1800, possuiDados: true);

        Assert.Equal(esperado, estimativa.Nivel);
    }

    [Fact]
    public void EstimarEspera_SemDados_NivelIndeterminado()
    {
        var estimativa = AnalisadorDesempenho.EstimarEspera(100, 50, 0, 1800, possuiDados: false);

        Assert.Equal(NivelCongestionamento.Indeterminado, estimativa.Nivel);
    }

    [Fact]
    public void EstimarEspera_VerdeMaiorQueCiclo_Lanca() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AnalisadorDesempenho.EstimarEspera(60, 70, 100, 1800, true));

    [Fact]
    public void EstimarEsperaPedestre_DependeDoTempoSemFaseDePedestres() =>
        Assert.Equal(32, AnalisadorDesempenho.EstimarEsperaPedestre(100, 20)); // 80²/200 = 32

    [Fact]
    public void AtrasoWebster_SemFluxo_ZeroAtraso() =>
        Assert.Equal(0, AnalisadorDesempenho.AtrasoWebster(90, 30, 0, 1800));

    [Fact]
    public void AtrasoWebster_MaisVerde_MenosAtraso()
    {
        var poucoVerde = AnalisadorDesempenho.AtrasoWebster(90, 20, 500, 1800);
        var muitoVerde = AnalisadorDesempenho.AtrasoWebster(90, 50, 500, 1800);

        Assert.True(muitoVerde < poucoVerde);
    }

    [Fact]
    public void Comparar_DemandaDesequilibrada_AdaptativoReduzAtrasoEEmissoes()
    {
        var calculadora = new CalculadoraPlanoSemaforico(new ParametrosSemaforicos());
        var analisador = new AnalisadorDesempenho(calculadora);
        var entrada = new EntradaCalculoPlano(
            ModoOperacao.Adaptativo,
            12,
            [
                new DemandaVia(Guid.NewGuid(), "Avenida movimentada", 3, 50, 150, 12, true),
                new DemandaVia(Guid.NewGuid(), "Rua tranquila", 1, 40, 8, 0, true)
            ]);

        var resultado = analisador.Comparar(entrada);

        Assert.True(resultado.ReducaoAtrasoPercentual > 0, $"Esperava redução de atraso, obtido {resultado.ReducaoAtrasoPercentual}%.");
        Assert.True(resultado.Adaptativo.AtrasoMedioSegundosPorVeiculo < resultado.TempoFixo.AtrasoMedioSegundosPorVeiculo);
        Assert.True(resultado.Co2EvitadoKgPorHora > 0);
    }
}
