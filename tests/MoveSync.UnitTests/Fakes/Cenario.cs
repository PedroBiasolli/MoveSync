using MoveSync.Application.Servicos;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;
using MoveSync.Domain.Servicos;

namespace MoveSync.UnitTests.Fakes;

/// <summary>Monta os casos de uso com dependências falsas e oferece atalhos para preparar dados (Arrange).</summary>
public sealed class Cenario
{
    public Cenario()
    {
        Relogio = new RelogioFixo(new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc));
        Parametros = new ParametrosSemaforicos();
        Calculadora = new CalculadoraPlanoSemaforico(Parametros);
        Analisador = new AnalisadorDesempenho(Calculadora);

        Usuarios = new UsuarioRepositoryFalso(Banco);
        Cruzamentos = new CruzamentoRepositoryFalso(Banco);
        Sensores = new SensorRepositoryFalso(Banco);
        Leituras = new LeituraRepositoryFalso(Banco);
        Planos = new PlanoRepositoryFalso(Banco);
        Ocorrencias = new OcorrenciaRepositoryFalso(Banco);
        Travessias = new TravessiaRepositoryFalso(Banco);
        Emergencias = new EmergenciaRepositoryFalso(Banco);
    }

    public BancoEmMemoria Banco { get; } = new();

    public RelogioFixo Relogio { get; }

    public ParametrosSemaforicos Parametros { get; }

    public CalculadoraPlanoSemaforico Calculadora { get; }

    public AnalisadorDesempenho Analisador { get; }

    public UnidadeTrabalhoFalsa UnidadeTrabalho { get; } = new();

    public NotificadorFalso Notificador { get; } = new();

    public HashFalso Hash { get; } = new();

    public UsuarioRepositoryFalso Usuarios { get; }

    public CruzamentoRepositoryFalso Cruzamentos { get; }

    public SensorRepositoryFalso Sensores { get; }

    public LeituraRepositoryFalso Leituras { get; }

    public PlanoRepositoryFalso Planos { get; }

    public OcorrenciaRepositoryFalso Ocorrencias { get; }

    public TravessiaRepositoryFalso Travessias { get; }

    public EmergenciaRepositoryFalso Emergencias { get; }

    public DateTime Agora => Relogio.Agora;

    public PlanoSemaforicoService PlanoService() =>
        new(Cruzamentos, Leituras, Planos, Emergencias, Travessias, UnidadeTrabalho, Notificador, Calculadora, Analisador, Relogio);

    public TelemetriaService TelemetriaService() => new(Sensores, Leituras, UnidadeTrabalho, Relogio);

    public AutenticacaoService AutenticacaoService() =>
        new(Usuarios, Sensores, Hash, new GeradorTokenFalso(), UnidadeTrabalho, Relogio);

    public OcorrenciaService OcorrenciaService() => new(Ocorrencias, Cruzamentos, UnidadeTrabalho, Notificador, Relogio);

    public PrioridadeService PrioridadeService() =>
        new(Cruzamentos, Travessias, Emergencias, UnidadeTrabalho, PlanoService(), Calculadora, Notificador, Relogio);

    public ImpactoService ImpactoService(PremissasEconomicas? premissas = null) =>
        new(Cruzamentos, PlanoService(), new CalculadoraImpactoEconomico(), premissas ?? new PremissasEconomicas(), Parametros, Relogio);

    public CruzamentoService CruzamentoService() =>
        new(Cruzamentos, Sensores, Ocorrencias, UnidadeTrabalho, PlanoService(), Relogio);

    /// <summary>Cruzamento com duas vias: uma avenida (3 faixas) e uma rua (1 faixa).</summary>
    public Cruzamento CriarCruzamento(string codigo = "TST-001")
    {
        var cruzamento = new Cruzamento(codigo, "Av. Teste x R. Exemplo", "Av. Teste, 100 - Centro", -23.5, -46.6, 12, Agora);
        cruzamento.AdicionarVia("Av. Teste", "Norte → Sul", 3, 50, Agora);
        cruzamento.AdicionarVia("R. Exemplo", "Leste → Oeste", 1, 40, Agora);
        Banco.Cruzamentos.Add(cruzamento);
        return cruzamento;
    }

    public Via Via(Cruzamento cruzamento, string nome) => cruzamento.Vias.Single(v => v.Nome == nome);

    public Sensor CriarSensor(Cruzamento cruzamento, Via via, string codigo, bool ativo = true)
    {
        var sensor = new Sensor(codigo, cruzamento.Id, via.Id, TipoSensor.LacoIndutivo, Hash.Gerar("segredo-muito-seguro-123"), Agora);
        sensor.DefinirAtivo(ativo);
        Banco.Sensores.Add(sensor);
        return sensor;
    }

    public void RegistrarLeitura(Sensor sensor, int veiculos, int fila, TimeSpan? atras = null)
    {
        var coletadaEm = Agora - (atras ?? TimeSpan.FromSeconds(30));
        Banco.Leituras.Add(new LeituraSensor(sensor, Guid.NewGuid().ToString("N"), coletadaEm, veiculos, fila, null, Agora));
    }

    public Usuario CriarUsuario(PerfilUsuario perfil, string email)
    {
        var usuario = new Usuario("Pessoa Teste", email, Hash.Gerar("Senha@123"), perfil, Agora);
        Banco.Usuarios.Add(usuario);
        return usuario;
    }
}
