using MoveSync.Application.Abstracoes;
using MoveSync.Application.Contratos;
using MoveSync.Application.Excecoes;
using MoveSync.Domain.Comum;
using MoveSync.Domain.Entidades;
using MoveSync.Domain.Enums;

namespace MoveSync.Application.Servicos;

/// <summary>
/// Cadastro e login. Decisões de segurança:
///  • Auto-cadastro cria SEMPRE perfil Cidadão (gestor é criado pela prefeitura/seed).
///  • Mensagem de erro de login é genérica, para não revelar quais e-mails existem.
///  • Senhas e segredos de sensores são guardados apenas como hash (PBKDF2).
/// </summary>
public sealed class AutenticacaoService(
    IUsuarioRepository usuarios,
    ISensorRepository sensores,
    IHashSenha hashSenha,
    IGeradorToken geradorToken,
    IUnitOfWork unitOfWork,
    TimeProvider relogio)
{
    // Hash fictício usado quando o e-mail não existe, para o tempo de resposta ser parecido (evita enumeração por tempo).
    private readonly Lazy<string> _hashFicticio = new(() => hashSenha.Gerar(Guid.NewGuid().ToString()));

    public async Task<UsuarioResponse> RegistrarAsync(RegistrarUsuarioRequest requisicao, CancellationToken ct)
    {
        var email = Usuario.NormalizarEmail(requisicao.Email);
        if (await usuarios.EmailExisteAsync(email, ct))
        {
            throw new RegraNegocioException("EMAIL_JA_CADASTRADO", "Já existe uma conta com este e-mail.", TipoViolacao.Conflito);
        }

        var usuario = new Usuario(
            requisicao.Nome,
            email,
            hashSenha.Gerar(requisicao.Senha),
            PerfilUsuario.Cidadao,
            Agora());

        await usuarios.AdicionarAsync(usuario, ct);
        try
        {
            await unitOfWork.SalvarAsync(ct);
        }
        catch (ConflitoPersistenciaException)
        {
            throw new RegraNegocioException("EMAIL_JA_CADASTRADO", "Já existe uma conta com este e-mail.", TipoViolacao.Conflito);
        }

        return UsuarioResponse.De(usuario);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest requisicao, CancellationToken ct)
    {
        var usuario = await usuarios.ObterPorEmailAsync(Usuario.NormalizarEmail(requisicao.Email), ct);
        if (usuario is null)
        {
            hashSenha.Verificar(_hashFicticio.Value, requisicao.Senha);
            throw new CredenciaisInvalidasException();
        }

        if (!usuario.Ativo || !hashSenha.Verificar(usuario.SenhaHash, requisicao.Senha))
        {
            throw new CredenciaisInvalidasException();
        }

        var token = geradorToken.GerarParaUsuario(usuario);
        return new TokenResponse(token.Token, "Bearer", token.ExpiraEm, usuario.Perfil.ToString(), UsuarioResponse.De(usuario));
    }

    /// <summary>Login de dispositivo IoT (ESP32/câmera) com código + segredo.</summary>
    public async Task<TokenResponse> LoginSensorAsync(TokenSensorRequest requisicao, CancellationToken ct)
    {
        Sensor? sensor = null;
        try
        {
            sensor = await sensores.ObterPorCodigoAsync(Cruzamento.NormalizarCodigo(requisicao.Codigo), ct);
        }
        catch (RegraNegocioException)
        {
            // código mal formatado é tratado como credencial inválida
        }

        if (sensor is null)
        {
            hashSenha.Verificar(_hashFicticio.Value, requisicao.Segredo);
            throw new CredenciaisInvalidasException();
        }

        if (!sensor.Ativo || !hashSenha.Verificar(sensor.SegredoHash, requisicao.Segredo))
        {
            throw new CredenciaisInvalidasException();
        }

        var token = geradorToken.GerarParaSensor(sensor);
        return new TokenResponse(token.Token, "Bearer", token.ExpiraEm, "Sensor", null);
    }

    public async Task<UsuarioResponse> ObterPerfilAsync(Guid usuarioId, CancellationToken ct)
    {
        var usuario = await usuarios.ObterPorIdAsync(usuarioId, ct)
            ?? throw new RecursoNaoEncontradoException("Usuário", usuarioId);
        return UsuarioResponse.De(usuario);
    }

    private DateTime Agora() => relogio.GetUtcNow().UtcDateTime;
}
