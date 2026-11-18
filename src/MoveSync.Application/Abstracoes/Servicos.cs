using MoveSync.Domain.Entidades;

namespace MoveSync.Application.Abstracoes;

/// <summary>Hash de senhas e segredos (implementado com PBKDF2 do ASP.NET Core Identity).</summary>
public interface IHashSenha
{
    string Gerar(string senha);

    bool Verificar(string hash, string senha);
}

public sealed record TokenGerado(string Token, DateTime ExpiraEm);

/// <summary>Emissão de JWT para pessoas (cidadão/gestor) e para dispositivos (sensor).</summary>
public interface IGeradorToken
{
    TokenGerado GerarParaUsuario(Usuario usuario);

    TokenGerado GerarParaSensor(Sensor sensor);
}

/// <summary>Evento enviado ao núcleo Java (Spring AI / RabbitMQ) para enriquecer a solução.</summary>
public sealed record EventoIntegracao(string Tipo, Guid CruzamentoId, string Resumo, DateTime OcorridoEm);

/// <summary>
/// Integração com serviço externo (núcleo Java). É "melhor esforço": se o serviço estiver fora,
/// a API .NET continua funcionando normalmente — a falha só aparece no health check.
/// </summary>
public interface INotificadorNucleo
{
    Task NotificarAsync(EventoIntegracao evento, CancellationToken ct);
}
