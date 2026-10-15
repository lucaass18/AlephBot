using Microsoft.EntityFrameworkCore;

namespace AlephBot.Threnodian.Dados;

// O banco do bot, um schema por assunto, e o do n8n só de leitura: cada tabela tem dono.
public sealed class AlephDb(DbContextOptions<AlephDb> options) : DbContext(options)
{
    // O bot em si: o que cada servidor escolheu.
    public const string Schema = "aleph";

    // A telemetria que o painel lê — cresce sozinha e não se mistura ao resto.
    public const string SchemaDeStats = "stats";

    // O cache do /ma: fichas de anime e mangá, personagens e buscas.
    public const string SchemaDeObras = "obras";

    // Onde o n8n guarda o que é dele. O bot não cria nem mexe em nada aqui.
    public const string SchemaDoN8n = "n8n";

    public DbSet<ConfigDoServidor> ConfigsDosServidores => Set<ConfigDoServidor>();

    public DbSet<ColetaDeStats> ColetasDeStats => Set<ColetaDeStats>();

    public DbSet<PlaylistSalva> Playlists => Set<PlaylistSalva>();

    public DbSet<ObraGuardada> Obras => Set<ObraGuardada>();

    public DbSet<BuscaGuardada> Buscas => Set<BuscaGuardada>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        // o padrão é o do bot; quem é de outro assunto diz o seu logo abaixo
        modelo.HasDefaultSchema(Schema);

        modelo.Entity<ObraGuardada>(obra =>
        {
            obra.ToTable("obra", SchemaDeObras);
            obra.HasKey(o => o.Id);

            obra.Property(o => o.Id).HasColumnName("id");
            obra.Property(o => o.Fonte).HasColumnName("fonte").HasMaxLength(40);
            obra.Property(o => o.Tipo).HasColumnName("tipo").HasConversion<int>();
            obra.Property(o => o.IdNaFonte).HasColumnName("id_na_fonte");
            obra.Property(o => o.Endereco).HasColumnName("endereco");
            obra.Property(o => o.Titulo).HasColumnName("titulo");
            obra.Property(o => o.TituloEmIngles).HasColumnName("titulo_em_ingles");
            obra.Property(o => o.TituloEmJapones).HasColumnName("titulo_em_japones");
            obra.Property(o => o.Sinonimos).HasColumnName("sinonimos");
            obra.Property(o => o.Imagem).HasColumnName("imagem");
            obra.Property(o => o.Formato).HasColumnName("formato");
            obra.Property(o => o.Situacao).HasColumnName("situacao").HasConversion<int>();
            obra.Property(o => o.Episodios).HasColumnName("episodios");
            obra.Property(o => o.Capitulos).HasColumnName("capitulos");
            obra.Property(o => o.Volumes).HasColumnName("volumes");
            obra.Property(o => o.Inicio).HasColumnName("inicio").HasMaxLength(10);
            obra.Property(o => o.Fim).HasColumnName("fim").HasMaxLength(10);
            obra.Property(o => o.Nota).HasColumnName("nota");
            obra.Property(o => o.Votos).HasColumnName("votos");
            obra.Property(o => o.Posicao).HasColumnName("posicao");
            obra.Property(o => o.Popularidade).HasColumnName("popularidade");
            obra.Property(o => o.Sinopse).HasColumnName("sinopse");
            obra.Property(o => o.SinopseEmPortugues).HasColumnName("sinopse_em_portugues");
            obra.Property(o => o.Generos).HasColumnName("generos");
            obra.Property(o => o.Autoria).HasColumnName("autoria");
            obra.Property(o => o.Classificacao).HasColumnName("classificacao");
            obra.Property(o => o.Procura).HasColumnName("procura");
            obra.Property(o => o.VistoEm).HasColumnName("visto_em");

            // a mesma obra na mesma fonte é uma linha só: buscar de novo atualiza, não duplica
            obra.HasIndex(o => new { o.Fonte, o.Tipo, o.IdNaFonte })
                .IsUnique()
                .HasDatabaseName("ix_obra_fonte_tipo_id");

            // com a fonte fora, achar a ficha é casar o que a pessoa digitou com os nomes
            obra.HasIndex(o => o.Procura).HasDatabaseName("ix_obra_procura");

            obra.HasMany(o => o.Personagens)
                .WithOne()
                .HasForeignKey(p => p.ObraId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelo.Entity<PersonagemGuardado>(personagem =>
        {
            personagem.ToTable("obra_personagem", SchemaDeObras);
            personagem.HasKey(p => p.Id);

            personagem.Property(p => p.Id).HasColumnName("id");
            personagem.Property(p => p.ObraId).HasColumnName("obra_id");
            personagem.Property(p => p.Ordem).HasColumnName("ordem");
            personagem.Property(p => p.Nome).HasColumnName("nome");
            personagem.Property(p => p.NomeNativo).HasColumnName("nome_nativo");
            personagem.Property(p => p.OutrosNomes).HasColumnName("outros_nomes");
            personagem.Property(p => p.Principal).HasColumnName("principal");
            personagem.Property(p => p.Imagem).HasColumnName("imagem");
        });

        modelo.Entity<BuscaGuardada>(busca =>
        {
            busca.ToTable("obra_busca", SchemaDeObras);
            busca.HasKey(b => b.Id);

            busca.Property(b => b.Id).HasColumnName("id");
            busca.Property(b => b.Termo).HasColumnName("termo").HasMaxLength(BuscaGuardada.TermoNoMaximo);
            busca.Property(b => b.Tipo).HasColumnName("tipo").HasConversion<int>();
            busca.Property(b => b.ObraId).HasColumnName("obra_id");
            busca.Property(b => b.VistoEm).HasColumnName("visto_em");

            // o mesmo termo procurado de novo tem que cair na mesma ficha, não criar outra
            busca.HasIndex(b => new { b.Termo, b.Tipo })
                .IsUnique()
                .HasDatabaseName("ix_obra_busca_termo_tipo");

            busca.HasOne(b => b.Obra)
                .WithMany()
                .HasForeignKey(b => b.ObraId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelo.Entity<PlaylistSalva>(playlist =>
        {
            playlist.ToTable("playlist");
            playlist.HasKey(p => p.Id);

            playlist.Property(p => p.Id).HasColumnName("id");
            playlist.Property(p => p.GuildId).HasColumnName("guild_id").HasConversion<long>();
            playlist.Property(p => p.Nome).HasColumnName("nome").HasMaxLength(PlaylistSalva.NomeNoMáximo);
            playlist.Property(p => p.CriadaPor).HasColumnName("criada_por").HasConversion<long>();
            playlist.Property(p => p.CriadaEm).HasColumnName("criada_em");

            // dois nomes iguais fariam "/playlist tocar fulano" virar sorteio; garante o banco
            playlist.HasIndex(p => new { p.GuildId, p.Nome })
                .IsUnique()
                .HasDatabaseName("ix_playlist_guild_nome");

            // apagar a playlist leva as faixas junto: faixa órfã não toca nem aparece
            playlist.HasMany(p => p.Faixas)
                .WithOne()
                .HasForeignKey(f => f.PlaylistId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelo.Entity<FaixaDaPlaylist>(faixa =>
        {
            faixa.ToTable("playlist_faixa");
            faixa.HasKey(f => f.Id);

            faixa.Property(f => f.Id).HasColumnName("id");
            faixa.Property(f => f.PlaylistId).HasColumnName("playlist_id");
            faixa.Property(f => f.Ordem).HasColumnName("ordem");

            // o encoded do Lavalink não tem tamanho fixo; título longo a gente corta na hora
            faixa.Property(f => f.Dados).HasColumnName("dados");
            faixa.Property(f => f.Titulo).HasColumnName("titulo").HasMaxLength(200);

            faixa.HasIndex(f => new { f.PlaylistId, f.Ordem }).HasDatabaseName("ix_playlist_faixa_ordem");
        });

        modelo.Entity<ColetaDeStats>(coleta =>
        {
            coleta.ToTable("stats_historico", SchemaDeStats);
            coleta.HasKey(c => c.Id);

            coleta.Property(c => c.Id).HasColumnName("id");
            coleta.Property(c => c.ColetadoEm).HasColumnName("coletado_em");
            coleta.Property(c => c.Servidores).HasColumnName("servidores");
            coleta.Property(c => c.Membros).HasColumnName("membros");
            coleta.Property(c => c.Online).HasColumnName("online");
            coleta.Property(c => c.Comandos).HasColumnName("comandos");
            coleta.Property(c => c.Players).HasColumnName("players");
            coleta.Property(c => c.Tocando).HasColumnName("tocando");
            coleta.Property(c => c.Pausados).HasColumnName("pausados");
            coleta.Property(c => c.NaFila).HasColumnName("na_fila");
            coleta.Property(c => c.MemoriaMb).HasColumnName("memoria_mb");
            coleta.Property(c => c.HeapMb).HasColumnName("heap_mb");

            // toda leitura do painel é por intervalo, e sem índice isso vira varredura da tabela
            coleta.HasIndex(c => c.ColetadoEm).HasDatabaseName("ix_stats_historico_coletado_em");
        });

        modelo.Entity<ConfigDoServidor>(config =>
        {
            config.ToTable("config_do_servidor");
            config.HasKey(c => c.GuildId);

            // o id do Discord é ulong de 64 bits, que o Postgres não tem: vai como bigint
            config.Property(c => c.GuildId)
                .HasColumnName("guild_id")
                .HasConversion<long>();

            // o maior prefixo que faz sentido cabe numa palavra; sem limite, nada passaria
            config.Property(c => c.Prefixo).HasColumnName("prefixo").HasMaxLength(16);

            config.Property(c => c.CanalDeLog).HasColumnName("canal_de_log").HasConversion<long?>();
            config.Property(c => c.MinutosDeEspera).HasColumnName("minutos_de_espera");
            config.Property(c => c.CriadoEm).HasColumnName("criado_em");
            config.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em");
        });
    }
}
