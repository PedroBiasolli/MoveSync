using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using MoveSync.Domain.Enums;

namespace MoveSync.Application.Contratos;

// -----------------------------------------------------------------------------
// Contratos de ENTRADA da API. As DataAnnotations são validadas automaticamente
// pelo ASP.NET Core 10 (builder.Services.AddValidation) antes de chegar ao endpoint:
// se algo estiver errado a API responde 400 com o detalhe de cada campo.
// Tipos de valor obrigatórios são "nullable" + [Required(ErrorMessage = "O campo {0} é obrigatório.")] para detectar ausência no JSON.
// -----------------------------------------------------------------------------

public sealed class RegistrarUsuarioRequest
{
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "O nome deve ter entre 3 e 120 caracteres.")]
    [Description("Nome completo do cidadão.")]
    public string Nome { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(160, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "A senha deve ter entre 8 e 64 caracteres.")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "A senha deve conter letras e números.")]
    public string Senha { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(64, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    public string Senha { get; init; } = string.Empty;
}

public sealed class TokenSensorRequest
{
    [Required(ErrorMessage = "Informe o código do sensor.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    [Description("Código do dispositivo, ex.: SNS-OSA001-N.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o segredo do sensor.")]
    [StringLength(128, MinimumLength = 16, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    public string Segredo { get; init; } = string.Empty;
}

public sealed class CriarViaRequest
{
    [Required(ErrorMessage = "Informe o nome da via.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o sentido.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    [Description("Sentido do tráfego, ex.: 'Centro → Bairro'.")]
    public string Sentido { get; init; } = string.Empty;

    [Range(1, 6, ErrorMessage = "Número de faixas deve estar entre 1 e 6.")]
    public int NumeroFaixas { get; init; } = 1;

    [Range(20, 80, ErrorMessage = "Velocidade máxima deve estar entre 20 e 80 km/h.")]
    [Description("Velocidade regulamentada. Define o amarelo: até 40 km/h = 3s, até 60 = 4s, acima = 5s.")]
    public int VelocidadeMaximaKmh { get; init; } = 40;
}

public sealed class AtualizarViaRequest
{
    [Required(ErrorMessage = "Informe o nome da via.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o sentido.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    public string Sentido { get; init; } = string.Empty;

    [Range(1, 6, ErrorMessage = "O campo {0} deve estar entre {1} e {2}.")]
    public int NumeroFaixas { get; init; } = 1;

    [Range(20, 80, ErrorMessage = "O campo {0} deve estar entre {1} e {2}.")]
    public int VelocidadeMaximaKmh { get; init; } = 40;

    public bool Ativa { get; init; } = true;
}

public sealed class CriarCruzamentoRequest
{
    [Required(ErrorMessage = "Informe o código do cruzamento.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Use apenas letras, números e hífen.")]
    [Description("Código único usado em campo, ex.: OSA-003.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o endereço.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    public string Endereco { get; init; } = string.Empty;

    [Range(-90.0, 90.0, ErrorMessage = "O campo {0} deve estar entre {1} e {2}.")]
    public double Latitude { get; init; }

    [Range(-180.0, 180.0, ErrorMessage = "O campo {0} deve estar entre {1} e {2}.")]
    public double Longitude { get; init; }

    [Range(3.0, 40.0, ErrorMessage = "A largura da travessia deve estar entre 3 e 40 metros.")]
    public double LarguraTravessiaMetros { get; init; } = 10;

    [Required(ErrorMessage = "Informe as vias do cruzamento.")]
    [MinLength(2, ErrorMessage = "Um cruzamento precisa de pelo menos 2 vias.")]
    [MaxLength(6, ErrorMessage = "Um cruzamento pode ter no máximo 6 vias.")]
    public List<CriarViaRequest> Vias { get; init; } = [];
}

public sealed class AtualizarCruzamentoRequest
{
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    public string Nome { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe o endereço.")]
    [StringLength(200, MinimumLength = 5, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    public string Endereco { get; init; } = string.Empty;

    [Range(-90.0, 90.0, ErrorMessage = "O campo {0} deve estar entre {1} e {2}.")]
    public double Latitude { get; init; }

    [Range(-180.0, 180.0, ErrorMessage = "O campo {0} deve estar entre {1} e {2}.")]
    public double Longitude { get; init; }

    [Range(3.0, 40.0, ErrorMessage = "O campo {0} deve estar entre {1} e {2}.")]
    public double LarguraTravessiaMetros { get; init; } = 10;

    public bool Ativo { get; init; } = true;
}

public sealed class AlterarModoRequest
{
    [Required(ErrorMessage = "Informe o modo: Adaptativo ou TempoFixo.")]
    [EnumDataType(typeof(ModoOperacao), ErrorMessage = "Valor inválido para o campo {0}.")]
    public ModoOperacao? Modo { get; init; }
}

public sealed class CriarSensorRequest
{
    [Required(ErrorMessage = "Informe o código do sensor.")]
    [StringLength(20, MinimumLength = 3, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Use apenas letras, números e hífen.")]
    public string Codigo { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a via monitorada.")]
    public Guid? ViaId { get; init; }

    [Required(ErrorMessage = "Informe o tipo: LacoIndutivo ou Camera.")]
    [EnumDataType(typeof(TipoSensor), ErrorMessage = "Valor inválido para o campo {0}.")]
    public TipoSensor? Tipo { get; init; }
}

public sealed class RegistrarLeituraRequest
{
    [Required(ErrorMessage = "Informe o identificador da leitura gerado pelo sensor.")]
    [StringLength(64, MinimumLength = 1, ErrorMessage = "O campo {0} deve ter entre {2} e {1} caracteres.")]
    [Description("Identificador único da leitura gerado no dispositivo. Reenviar o mesmo valor NÃO duplica a leitura (idempotência).")]
    public string LeituraId { get; init; } = string.Empty;

    [Required(ErrorMessage = "Informe a data/hora da coleta (UTC, ISO 8601).")]
    public DateTime? ColetadaEm { get; init; }

    [Required(ErrorMessage = "Informe os veículos contados.")]
    [Range(0, 500, ErrorMessage = "Veículos contados deve estar entre 0 e 500.")]
    public int? VeiculosContados { get; init; }

    [Required(ErrorMessage = "Informe o tamanho da fila.")]
    [Range(0, 200, ErrorMessage = "Tamanho da fila deve estar entre 0 e 200 veículos.")]
    public int? TamanhoFila { get; init; }

    [Range(0.0, 200.0, ErrorMessage = "O campo {0} deve estar entre {1} e {2}.")]
    public double? VelocidadeMediaKmh { get; init; }
}

public sealed class RegistrarLoteLeiturasRequest
{
    [Required(ErrorMessage = "O campo {0} é obrigatório.")]
    [MinLength(1, ErrorMessage = "Envie ao menos uma leitura.")]
    [MaxLength(100, ErrorMessage = "Envie no máximo 100 leituras por lote.")]
    public List<RegistrarLeituraRequest> Leituras { get; init; } = [];
}

public sealed class CriarOcorrenciaRequest
{
    [Required(ErrorMessage = "Informe o cruzamento.")]
    public Guid? CruzamentoId { get; init; }

    [Required(ErrorMessage = "Informe o tipo da ocorrência.")]
    [EnumDataType(typeof(TipoOcorrencia), ErrorMessage = "Valor inválido para o campo {0}.")]
    public TipoOcorrencia? Tipo { get; init; }

    [Required(ErrorMessage = "Descreva o problema.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "A descrição deve ter entre 10 e 500 caracteres.")]
    public string Descricao { get; init; } = string.Empty;
}

public sealed class AtualizarOcorrenciaRequest
{
    [Required(ErrorMessage = "Informe o tipo da ocorrência.")]
    [EnumDataType(typeof(TipoOcorrencia), ErrorMessage = "Valor inválido para o campo {0}.")]
    public TipoOcorrencia? Tipo { get; init; }

    [Required(ErrorMessage = "Descreva o problema.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "A descrição deve ter entre 10 e 500 caracteres.")]
    public string Descricao { get; init; } = string.Empty;
}

public sealed class AlterarStatusOcorrenciaRequest
{
    [Required(ErrorMessage = "Informe o novo status.")]
    [EnumDataType(typeof(StatusOcorrencia), ErrorMessage = "Valor inválido para o campo {0}.")]
    public StatusOcorrencia? Status { get; init; }

    [StringLength(500, ErrorMessage = "A resposta pode ter no máximo 500 caracteres.")]
    public string? Resposta { get; init; }
}

public sealed class SolicitarTravessiaRequest
{
    [Required(ErrorMessage = "Informe o motivo da travessia acessível.")]
    [EnumDataType(typeof(MotivoTravessiaAcessivel), ErrorMessage = "Valor inválido para o campo {0}.")]
    public MotivoTravessiaAcessivel? Motivo { get; init; }
}

public sealed class AcionarEmergenciaRequest
{
    [Required(ErrorMessage = "Informe a via por onde o veículo de emergência vai passar.")]
    public Guid? ViaId { get; init; }

    [Required(ErrorMessage = "Informe o tipo do veículo.")]
    [EnumDataType(typeof(TipoVeiculoEmergencia), ErrorMessage = "Valor inválido para o campo {0}.")]
    public TipoVeiculoEmergencia? TipoVeiculo { get; init; }

    [Range(30, 600, ErrorMessage = "A duração deve estar entre 30 e 600 segundos.")]
    public int DuracaoSegundos { get; init; } = 120;
}

/// <summary>
/// Parâmetros de paginação recebidos pela query string (?pagina=1&amp;tamanhoPagina=20).
/// São opcionais (nullable): se o app não informar, usamos página 1 com 20 itens.
/// </summary>
public sealed class ParametrosPaginacao
{
    public const int TamanhoPadrao = 20;

    [Range(1, 100000, ErrorMessage = "A página deve ser maior ou igual a 1.")]
    public int? Pagina { get; init; }

    [Range(1, 100, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
    public int? TamanhoPagina { get; init; }

    /// <summary>Página efetiva (1 quando não informada).</summary>
    public int PaginaAtual => Pagina ?? 1;

    /// <summary>Itens por página efetivos (20 quando não informado).</summary>
    public int ItensPorPagina => TamanhoPagina ?? TamanhoPadrao;
}

public sealed class AlterarAtivoSensorRequest
{
    [Required(ErrorMessage = "Informe se o sensor deve ficar ativo (true) ou inativo (false).")]
    public bool? Ativo { get; init; }
}

/// <summary>
/// Simulação de cenários no relatório de impacto (query string opcional).
/// Sem parâmetros, valem as premissas do appsettings (seção ModeloNegocio).
/// </summary>
public sealed class ParametrosImpacto
{
    [Range(1, 24, ErrorMessage = "As horas de referência por dia devem estar entre 1 e 24.")]
    public int? HorasReferenciaPorDia { get; init; }

    [Range(0.01, 1000.0, ErrorMessage = "O valor da hora deve estar entre R$ 0,01 e R$ 1.000,00.")]
    public decimal? ValorHoraReais { get; init; }

    [Range(0.0, 100000.0, ErrorMessage = "A assinatura mensal por cruzamento deve estar entre R$ 0 e R$ 100.000,00.")]
    public decimal? AssinaturaMensalPorCruzamentoReais { get; init; }
}
