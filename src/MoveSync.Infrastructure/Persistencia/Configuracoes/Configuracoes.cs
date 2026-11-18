using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoveSync.Domain.Entidades;

namespace MoveSync.Infrastructure.Persistencia.Configuracoes;

// Mapeamento Fluent API de cada entidade para as tabelas MS_*.
// Observação importante: todas as chaves são Guid geradas pelo domínio, por isso ValueGeneratedNever().
// Sem isso o EF entenderia uma Via nova (com Id já preenchido) como "existente" e tentaria um UPDATE.

internal sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> b)
    {
        b.ToTable("MS_USUARIO");
        b.HasKey(u => u.Id);
        b.Property(u => u.Id).ValueGeneratedNever();
        b.Property(u => u.Nome).HasMaxLength(120).IsRequired();
        b.Property(u => u.Email).HasMaxLength(160).IsRequired();
        b.Property(u => u.SenhaHash).HasMaxLength(200).IsRequired();
        b.HasIndex(u => u.Email).IsUnique();
    }
}

internal sealed class CruzamentoConfiguracao : IEntityTypeConfiguration<Cruzamento>
{
    public void Configure(EntityTypeBuilder<Cruzamento> b)
    {
        b.ToTable("MS_CRUZAMENTO");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.Codigo).HasMaxLength(20).IsRequired();
        b.Property(c => c.Nome).HasMaxLength(120).IsRequired();
        b.Property(c => c.Endereco).HasMaxLength(200).IsRequired();
        b.HasIndex(c => c.Codigo).IsUnique();
        b.Ignore(c => c.ViasAtivas);

        b.HasMany(c => c.Vias)
            .WithOne()
            .HasForeignKey(v => v.CruzamentoId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(c => c.Vias).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ViaConfiguracao : IEntityTypeConfiguration<Via>
{
    public void Configure(EntityTypeBuilder<Via> b)
    {
        b.ToTable("MS_VIA");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).ValueGeneratedNever();
        b.Property(v => v.Nome).HasMaxLength(100).IsRequired();
        b.Property(v => v.Sentido).HasMaxLength(60).IsRequired();
    }
}

internal sealed class SensorConfiguracao : IEntityTypeConfiguration<Sensor>
{
    public void Configure(EntityTypeBuilder<Sensor> b)
    {
        b.ToTable("MS_SENSOR");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.Codigo).HasMaxLength(20).IsRequired();
        b.Property(s => s.SegredoHash).HasMaxLength(200).IsRequired();
        b.HasIndex(s => s.Codigo).IsUnique();

        // Regra: um sensor por via (evita contar veículos em dobro).
        b.HasIndex(s => s.ViaId).IsUnique();

        b.HasOne<Cruzamento>()
            .WithMany()
            .HasForeignKey(s => s.CruzamentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LeituraSensorConfiguracao : IEntityTypeConfiguration<LeituraSensor>
{
    public void Configure(EntityTypeBuilder<LeituraSensor> b)
    {
        b.ToTable("MS_LEITURA_SENSOR");
        b.HasKey(l => l.Id);
        b.Property(l => l.Id).ValueGeneratedNever();
        b.Property(l => l.LeituraExternaId).HasMaxLength(64).IsRequired();

        // Chave natural de idempotência da telemetria.
        b.HasIndex(l => new { l.SensorId, l.LeituraExternaId }).IsUnique();
        b.HasIndex(l => new { l.CruzamentoId, l.ColetadaEm });
        b.HasIndex(l => new { l.ViaId, l.ColetadaEm });

        b.HasOne<Sensor>()
            .WithMany()
            .HasForeignKey(l => l.SensorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PlanoSemaforicoConfiguracao : IEntityTypeConfiguration<PlanoSemaforico>
{
    public void Configure(EntityTypeBuilder<PlanoSemaforico> b)
    {
        b.ToTable("MS_PLANO_SEMAFORICO");
        b.HasKey(p => p.Id);
        b.Property(p => p.Id).ValueGeneratedNever();
        b.Property(p => p.Justificativa).HasMaxLength(2000).IsRequired();
        b.HasIndex(p => new { p.CruzamentoId, p.Vigente });

        b.HasOne<Cruzamento>()
            .WithMany()
            .HasForeignKey(p => p.CruzamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(p => p.Fases)
            .WithOne()
            .HasForeignKey(f => f.PlanoId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Navigation(p => p.Fases).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class FasePlanoConfiguracao : IEntityTypeConfiguration<FasePlano>
{
    public void Configure(EntityTypeBuilder<FasePlano> b)
    {
        b.ToTable("MS_FASE_PLANO");
        b.HasKey(f => f.Id);
        b.Property(f => f.Id).ValueGeneratedNever();
        b.Property(f => f.NomeVia).HasMaxLength(100).IsRequired();
    }
}

internal sealed class OcorrenciaConfiguracao : IEntityTypeConfiguration<Ocorrencia>
{
    public void Configure(EntityTypeBuilder<Ocorrencia> b)
    {
        b.ToTable("MS_OCORRENCIA");
        b.HasKey(o => o.Id);
        b.Property(o => o.Id).ValueGeneratedNever();
        b.Property(o => o.Descricao).HasMaxLength(500).IsRequired();
        b.Property(o => o.RespostaGestor).HasMaxLength(500);
        b.Ignore(o => o.EstaFinalizada);
        b.HasIndex(o => o.AutorId);
        b.HasIndex(o => new { o.Status, o.Prioridade });

        // Ocorrências preservam o histórico do cidadão: impedem a exclusão do cruzamento/usuário.
        b.HasOne<Cruzamento>()
            .WithMany()
            .HasForeignKey(o => o.CruzamentoId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(o => o.AutorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SolicitacaoTravessiaConfiguracao : IEntityTypeConfiguration<SolicitacaoTravessia>
{
    public void Configure(EntityTypeBuilder<SolicitacaoTravessia> b)
    {
        b.ToTable("MS_SOLICITACAO_TRAVESSIA");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.HasIndex(s => new { s.CruzamentoId, s.ExpiraEm });

        b.HasOne<Cruzamento>()
            .WithMany()
            .HasForeignKey(s => s.CruzamentoId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(s => s.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AcionamentoEmergenciaConfiguracao : IEntityTypeConfiguration<AcionamentoEmergencia>
{
    public void Configure(EntityTypeBuilder<AcionamentoEmergencia> b)
    {
        b.ToTable("MS_ACIONAMENTO_EMERGENCIA");
        b.HasKey(a => a.Id);
        b.Property(a => a.Id).ValueGeneratedNever();
        b.HasIndex(a => new { a.CruzamentoId, a.ExpiraEm });

        b.HasOne<Cruzamento>()
            .WithMany()
            .HasForeignKey(a => a.CruzamentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RegistroIdempotenciaConfiguracao : IEntityTypeConfiguration<RegistroIdempotencia>
{
    public void Configure(EntityTypeBuilder<RegistroIdempotencia> b)
    {
        b.ToTable("MS_IDEMPOTENCIA");
        b.HasKey(r => r.Id);
        b.Property(r => r.Id).ValueGeneratedNever();
        b.Property(r => r.Escopo).HasMaxLength(300).IsRequired();
        b.Property(r => r.Chave).HasMaxLength(100).IsRequired();
        b.Property(r => r.HashRequisicao).HasMaxLength(64).IsRequired();
        b.Property(r => r.ContentType).HasMaxLength(100);
        b.Property(r => r.Location).HasMaxLength(500);
        b.HasIndex(r => new { r.Escopo, r.Chave }).IsUnique();
        b.HasIndex(r => r.ExpiraEm);
    }
}
