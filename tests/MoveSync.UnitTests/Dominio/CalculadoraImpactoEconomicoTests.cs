using MoveSync.Domain.Comum;
using MoveSync.Domain.Servicos;

namespace MoveSync.UnitTests.Dominio;

/// <summary>
/// Regra do modelo de negócio: transforma o ganho de tráfego medido em números que a prefeitura entende
/// (horas devolvidas à população, CO₂, R$, relação benefício/custo e payback social).
/// </summary>
public class CalculadoraImpactoEconomicoTests
{
    private static readonly CalculadoraImpactoEconomico Calculadora = new();

    private static readonly PremissasEconomicas Premissas = new()
    {
        ValorHoraCidadaoReais = 20m,
        HorasReferenciaPorDia = 10,
        DiasPorMes = 20,
        AssinaturaMensalPorCruzamentoReais = 500m,
        ImplantacaoPorCruzamentoReais = 4_000m
    };

    [Fact]
    public void Calcular_ProjetaHorasCo2EValorParaOMes()
    {
        var ganhos = new[]
        {
            new GanhoCruzamento(Guid.NewGuid(), HorasEvitadasPorHora: 2.0, Co2EvitadoKgPorHora: 4.0),
            new GanhoCruzamento(Guid.NewGuid(), HorasEvitadasPorHora: 1.0, Co2EvitadoKgPorHora: 2.0)
        };

        var r = Calculadora.Calcular(ganhos, cruzamentosContratados: 2, Premissas);

        Assert.Equal(30, r.HorasEvitadasPorDia, 6);            // (2 + 1) h/h × 10 h
        Assert.Equal(600, r.HorasEvitadasPorMes, 6);           // × 20 dias
        Assert.Equal(1.2, r.Co2EvitadoToneladasPorMes, 6);     // 6 kg/h × 10 × 20 / 1000
        Assert.Equal(12_000m, r.ValorTempoEconomizadoMesReais); // 600 h × R$ 20
        Assert.Equal(1_000m, r.AssinaturaMensalReais);         // 2 × R$ 500
        Assert.Equal(8_000m, r.ImplantacaoTotalReais);         // 2 × R$ 4.000
        Assert.Equal(12.0, r.RelacaoBeneficioCusto!.Value, 6);
        Assert.Equal(0.7, r.PaybackSocialImplantacaoMeses!.Value, 6);     // R$ 8.000 ÷ (12.000 − 1.000) ≈ 0,73 → 0,7 mês
    }

    [Fact]
    public void Calcular_SemDadosDeTelemetria_NaoInventaBeneficioNemRelacao()
    {
        var r = Calculadora.Calcular([], cruzamentosContratados: 3, Premissas);

        Assert.Equal(0, r.HorasEvitadasPorMes);
        Assert.Equal(0m, r.ValorTempoEconomizadoMesReais);
        Assert.Equal(1_500m, r.AssinaturaMensalReais);  // o custo existe mesmo sem dado
        Assert.Null(r.RelacaoBeneficioCusto);
        Assert.Null(r.PaybackSocialImplantacaoMeses);
    }

    [Fact]
    public void Calcular_GanhoNegativoEmUmCruzamento_ContaComoZero()
    {
        // Em demanda muito baixa o adaptativo pode empatar com o fixo; um ruído negativo não pode "descontar" ganho de outro cruzamento.
        var ganhos = new[]
        {
            new GanhoCruzamento(Guid.NewGuid(), 1.0, 1.0),
            new GanhoCruzamento(Guid.NewGuid(), -0.5, -0.5)
        };

        var r = Calculadora.Calcular(ganhos, 2, Premissas);

        Assert.Equal(200, r.HorasEvitadasPorMes, 6);
        Assert.Equal(0.2, r.Co2EvitadoToneladasPorMes, 6);
    }

    [Fact]
    public void Calcular_BeneficioMenorQueAssinatura_SemPaybackERelacaoAbaixoDeUm()
    {
        var ganhos = new[] { new GanhoCruzamento(Guid.NewGuid(), 0.1, 0.1) }; // 20 h/mês × R$ 20 = R$ 400

        var r = Calculadora.Calcular(ganhos, 1, Premissas);

        Assert.Equal(0.8, r.RelacaoBeneficioCusto!.Value, 6);
        Assert.Null(r.PaybackSocialImplantacaoMeses);
    }

    [Fact]
    public void Calcular_AssinaturaGratuita_PilotoSemRelacaoBeneficioCusto()
    {
        var piloto = Premissas with { AssinaturaMensalPorCruzamentoReais = 0m, ImplantacaoPorCruzamentoReais = 0m };

        var r = Calculadora.Calcular([new GanhoCruzamento(Guid.NewGuid(), 1, 1)], 1, piloto);

        Assert.Equal(4_000m, r.ValorTempoEconomizadoMesReais);
        Assert.Null(r.RelacaoBeneficioCusto);
        Assert.Null(r.PaybackSocialImplantacaoMeses);
    }

    [Fact]
    public void Calcular_QuantidadeContratadaNegativa_Rejeita()
    {
        var erro = Assert.Throws<RegraNegocioException>(() => Calculadora.Calcular([], -1, Premissas));
        Assert.Equal("CAMPO_INVALIDO", erro.Codigo);
    }

    [Theory]
    [InlineData(0, 10, 22)]   // valor da hora zero
    [InlineData(21, 0, 22)]   // horas por dia fora de 1–24
    [InlineData(21, 25, 22)]
    [InlineData(21, 10, 0)]   // dias por mês fora de 1–31
    [InlineData(21, 10, 32)]
    public void Premissas_ForaDaFaixa_SaoRejeitadas(double valorHora, int horasDia, int diasMes)
    {
        var premissas = new PremissasEconomicas
        {
            ValorHoraCidadaoReais = (decimal)valorHora,
            HorasReferenciaPorDia = horasDia,
            DiasPorMes = diasMes
        };

        Assert.Throws<RegraNegocioException>(premissas.Validar);
    }

    [Fact]
    public void Premissas_Padrao_SaoValidas()
    {
        new PremissasEconomicas().Validar(); // não deve lançar exceção

        Assert.Equal(21.15m, new PremissasEconomicas().ValorHoraCidadaoReais);
    }
}
