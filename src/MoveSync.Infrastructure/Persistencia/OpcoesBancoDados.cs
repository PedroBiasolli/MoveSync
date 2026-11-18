namespace MoveSync.Infrastructure.Persistencia;

/// <summary>Seção "BancoDados" do appsettings.</summary>
public sealed class OpcoesBancoDados
{
    public const string Secao = "BancoDados";

    /// <summary>"Oracle" (nuvem/produção) ou "Sqlite" (desenvolvimento local e testes).</summary>
    public string Provedor { get; set; } = "Sqlite";

    /// <summary>Cria as tabelas MS_* automaticamente na inicialização, se ainda não existirem.</summary>
    public bool CriarTabelasAutomaticamente { get; set; } = true;

    /// <summary>
    /// Versão do Oracle de destino (19, 21 ou 23). O servidor Oracle da FIAP é 19c: sem isso o
    /// EF Core 10 geraria colunas BOOLEAN, que só existem a partir do Oracle 23ai.
    /// </summary>
    public int VersaoOracle { get; set; } = 19;

    public bool EhOracle => string.Equals(Provedor, "Oracle", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Seção "Seed": dados de demonstração. Senhas vêm de configuração/variáveis de ambiente, nunca do código.</summary>
public sealed class OpcoesSeed
{
    public const string Secao = "Seed";

    public bool Habilitado { get; set; }

    public string GestorNome { get; set; } = "Gestor de Trânsito";

    public string GestorEmail { get; set; } = "gestor@movesync.dev";

    public string? GestorSenha { get; set; }

    public string CidadaoNome { get; set; } = "Cidadã Demonstração";

    public string CidadaoEmail { get; set; } = "cidada@movesync.dev";

    public string? CidadaoSenha { get; set; }

    /// <summary>Segredo comum aos sensores de demonstração (mín. 16 caracteres).</summary>
    public string? SegredoSensores { get; set; }
}
