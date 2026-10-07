namespace AlephBot.Threnodian.Api;

/// <summary>
/// A cara da página dos docs: a paleta do console do bot (a do Banner), o ℵ na aba e o que o
/// Discord mostra quando alguém cola o link. Só variáveis de cor do Scalar, e nenhum seletor
/// do HTML dele: o tema sobrevive quando o pacote atualizar.
/// </summary>
public static partial class ApiDocs
{
    // as cores do Banner em hexadecimal: roxo é o que deu certo, laranja o aviso, vermelho o erro
    private const string Roxo = "#af87ff";
    private const string Laranja = "#ffaf00";
    private const string Vermelho = "#ff5f5f";
    private const string Cinza = "#8a8a8a";

    // propriedade, e não campo: a Página mora no outro arquivo da classe, e entre arquivos de
    // uma classe parcial a ordem em que os campos estáticos nascem não é garantida
    private static string Ícone => $"{Página}/aleph.svg";

    // "html .dark-mode", e não só ".dark-mode": o tema padrão do Scalar usa o seletor curto, e o
    // mais específico vence sem depender da ordem em que os dois entram na página. Nem
    // "body.dark-mode": o Scalar repete a classe em blocos de dentro (o do botão de testar), que
    // redeclaram as cores dele e não herdariam as daqui
    private const string Tema = $$"""
        html .dark-mode {
          --scalar-background-1: #110f16;
          --scalar-background-2: #1a1721;
          --scalar-background-3: #26222f;
          --scalar-background-accent: {{Roxo}}1f;
          --scalar-border-color: #2b2734;
          --scalar-color-1: #ece8f5;
          --scalar-color-2: #aaa3b8;
          --scalar-color-3: {{Cinza}};
          --scalar-color-accent: {{Roxo}};
          --scalar-link-color: {{Roxo}};
          --scalar-link-color-hover: #c9adff;

          --scalar-color-purple: {{Roxo}};
          --scalar-color-orange: {{Laranja}};
          --scalar-color-red: {{Vermelho}};
          --scalar-color-yellow: #ffd75f;
          --scalar-color-green: #5fd787;
          --scalar-color-blue: #87afff;

          --scalar-button-1: {{Roxo}};
          --scalar-button-1-hover: #c2a3ff;
          --scalar-button-1-color: #110f16;

          --scalar-sidebar-background-1: #0d0b11;
          --scalar-sidebar-item-active-background: {{Roxo}}1f;
          --scalar-sidebar-color-active: {{Roxo}};
        }

        html .light-mode {
          --scalar-color-accent: #7c4dff;
          --scalar-background-accent: #7c4dff14;
          --scalar-link-color: #7c4dff;
          --scalar-button-1: #7c4dff;
          --scalar-button-1-hover: #6a3df0;
          --scalar-button-1-color: #fff;
          --scalar-sidebar-item-active-background: #7c4dff14;
          --scalar-sidebar-color-active: #7c4dff;
        }
        """;

    /// <summary>O ℵ do nome do bot, no roxo do console, num quadrado escuro como o fundo da página.</summary>
    private const string ÍconeSvg = $"""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
          <rect width="32" height="32" rx="7" fill="#1a1721"/>
          <text x="16" y="23" text-anchor="middle" font-size="21" font-weight="700" fill="{Roxo}"
                font-family="'Segoe UI Symbol','Cambria Math','DejaVu Sans',serif">ℵ</text>
        </svg>
        """;

    /// <summary>
    /// Vai no HTML que o servidor manda, e não pelo JavaScript: quem lê é o robô que monta a
    /// prévia do link no Discord, e ele não roda script. A barra lateral da prévia sai roxa.
    /// </summary>
    private const string Cabeçalho = $"""
        <meta name="theme-color" content="{Roxo}">
        <meta property="og:title" content="{Título}">
        <meta property="og:description" content="O estado, os números e os comandos do AlephBot.">
        """;
}
