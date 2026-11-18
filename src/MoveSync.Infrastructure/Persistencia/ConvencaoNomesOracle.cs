using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MoveSync.Infrastructure.Persistencia;

/// <summary>
/// No Oracle, nomes entre aspas são sensíveis a maiúsculas. Para que qualquer pessoa consiga fazer
/// "SELECT * FROM MS_CRUZAMENTO" sem aspas, convertemos todas as colunas para MAIÚSCULAS_COM_UNDERLINE
/// (ex.: LarguraTravessiaMetros → LARGURA_TRAVESSIA_METROS). As tabelas usam o prefixo MS_ (MoveSync)
/// para não colidir com tabelas de outras disciplinas no mesmo usuário Oracle.
/// </summary>
public static class ConvencaoNomesOracle
{
    public static void Aplicar(ModelBuilder modelBuilder)
    {
        foreach (var entidade in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var propriedade in entidade.GetProperties())
            {
                propriedade.SetColumnName(ParaMaiusculasSnake(propriedade.Name));
            }
        }
    }

    public static string ParaMaiusculasSnake(string nome)
    {
        var sb = new StringBuilder(nome.Length + 8);
        for (var i = 0; i < nome.Length; i++)
        {
            var c = nome[i];
            if (i > 0 && char.IsUpper(c) && (char.IsLower(nome[i - 1]) || (i + 1 < nome.Length && char.IsLower(nome[i + 1]) && char.IsUpper(nome[i - 1]))))
            {
                sb.Append('_');
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }
}
