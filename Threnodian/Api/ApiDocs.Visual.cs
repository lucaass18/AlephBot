namespace AlephBot.Threnodian.Api;

/// <summary>
/// A cara da página dos docs: o jeito do Cypress (docs.cypress.io e o app), com as cores do
/// bot no lugar do índigo e do verde dele. Página clara com os cinzas azulados do Cypress, a
/// barra lateral no azul-marinho do app dele, e o roxo do Banner onde o Cypress põe destaque.
/// Mais o ℵ na aba e o que o Discord mostra quando alguém cola o link.
///
/// Quase tudo é variável de cor do Scalar; o único seletor do HTML dele é o da barra lateral
/// (t-doc__sidebar), o gancho que ele mesmo deixa pra tema. O tema sobrevive quando o pacote
/// atualizar.
/// </summary>
public static partial class ApiDocs
{
    // as cores do Banner em hexadecimal: roxo é o que deu certo, laranja o aviso, vermelho o erro
    private const string Roxo = "#af87ff";
    private const string Laranja = "#ffaf00";
    private const string Vermelho = "#ff5f5f";

    // o mesmo roxo, mais escuro: o do Banner some no branco (2,6:1), este dá 6:1 e serve pra
    // texto e botão na página clara. O laranja e o vermelho escurecem pelo mesmo motivo
    private const string RoxoNoClaro = "#713cdd";
    private const string LaranjaNoClaro = "#a86400";
    private const string VermelhoNoClaro = "#d63c3c";

    // o azul-marinho do Cypress (gray-1000), da barra lateral e do fundo do ícone
    private const string Marinho = "#1b1e2e";

    // propriedade, e não campo: a Página mora no outro arquivo da classe, e entre arquivos de
    // uma classe parcial a ordem em que os campos estáticos nascem não é garantida
    private static string Ícone => $"{Página}/aleph.svg";

    // "html .light-mode", e não só ".light-mode": o tema padrão do Scalar usa o seletor curto, e
    // o mais específico vence sem depender da ordem em que os dois entram na página. Nem
    // "body.light-mode": o Scalar repete a classe em blocos de dentro (o do botão de testar),
    // que redeclaram as cores dele e não herdariam as daqui. Os nomes nos comentários são os
    // tokens da paleta do Cypress (github.com/cypress-io/cypress-design)
    private const string Tema = $$"""
        html .light-mode, html .dark-mode {
          --scalar-radius: 4px;
        }

        html .light-mode {
          --scalar-background-1: #ffffff;
          --scalar-background-2: #f3f4fa; /* gray-50 */
          --scalar-background-3: #e1e3ed; /* gray-100 */
          --scalar-background-accent: {{Roxo}}26;
          --scalar-border-color: #e1e3ed; /* gray-100 */
          --scalar-color-1: {{Marinho}}; /* gray-1000 */
          --scalar-color-2: #434861; /* gray-800 */
          --scalar-color-3: #747994; /* gray-600 */
          --scalar-color-accent: {{RoxoNoClaro}};
          --scalar-link-color: {{RoxoNoClaro}};
          --scalar-link-color-hover: #5a2bb8;

          --scalar-color-blue: {{RoxoNoClaro}};
          --scalar-color-purple: {{RoxoNoClaro}};
          --scalar-color-orange: {{LaranjaNoClaro}};
          --scalar-color-red: {{VermelhoNoClaro}};

          --scalar-button-1: {{RoxoNoClaro}};
          --scalar-button-1-hover: #5a2bb8;
          --scalar-button-1-color: #ffffff;
        }

        html .dark-mode {
          --scalar-background-1: {{Marinho}}; /* gray-1000 */
          --scalar-background-2: #25283c; /* gray-950 */
          --scalar-background-3: #2e3247; /* gray-900 */
          --scalar-background-accent: {{Roxo}}26;
          --scalar-border-color: #2e3247; /* gray-900 */
          --scalar-color-1: #f3f4fa; /* gray-50 */
          --scalar-color-2: #bfc2d4; /* gray-300 */
          --scalar-color-3: #9095ad; /* gray-500 */
          --scalar-color-accent: {{Roxo}};
          --scalar-link-color: {{Roxo}};
          --scalar-link-color-hover: #c9adff;

          --scalar-color-blue: {{Roxo}};
          --scalar-color-purple: {{Roxo}};
          --scalar-color-orange: {{Laranja}};
          --scalar-color-red: {{Vermelho}};

          --scalar-button-1: {{Roxo}};
          --scalar-button-1-hover: #c2a3ff;
          --scalar-button-1-color: {{Marinho}};
        }

        /* a barra lateral no azul-marinho do app do Cypress, nos dois modos. As cores gerais
           mudam aqui dentro também, pra busca, botões e etiquetas de método virem junto */
        html .light-mode .t-doc__sidebar, html .dark-mode .t-doc__sidebar {
          --scalar-background-1: {{Marinho}};
          --scalar-background-2: #2e3247; /* gray-900 */
          --scalar-background-3: #434861; /* gray-800 */
          --scalar-border-color: #2e3247;
          --scalar-color-1: #e1e3ed; /* gray-100 */
          --scalar-color-2: #afb3c7; /* gray-400 */
          --scalar-color-3: #9095ad; /* gray-500 */
          --scalar-color-accent: {{Roxo}};
          --scalar-color-blue: {{Roxo}};

          --scalar-sidebar-background-1: {{Marinho}};
          --scalar-sidebar-color-1: #e1e3ed;
          --scalar-sidebar-color-2: #afb3c7;
          --scalar-sidebar-border-color: #2e3247;
          --scalar-sidebar-item-hover-background: #2e3247;
          --scalar-sidebar-item-hover-color: #ffffff;
          --scalar-sidebar-item-active-background: {{Roxo}}26;
          --scalar-sidebar-color-active: {{Roxo}};
          --scalar-sidebar-indent-border: #2e3247;
          --scalar-sidebar-indent-border-hover: #434861;
          --scalar-sidebar-indent-border-active: {{Roxo}};
          --scalar-sidebar-search-background: #2e3247;
          --scalar-sidebar-search-border-color: #434861;
          --scalar-sidebar-search-color: #9095ad;
        }

        /* no escuro a página já é marinho: a barra desce um tom (gray-1100) pra não sumir */
        html .dark-mode .t-doc__sidebar {
          --scalar-background-1: #161827;
          --scalar-sidebar-background-1: #161827;
        }
        """;

    /// <summary>O ℵ do nome do bot, no roxo do console, no azul-marinho da barra lateral.</summary>
    private const string ÍconeSvg = $"""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
          <rect width="32" height="32" rx="7" fill="{Marinho}"/>
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
