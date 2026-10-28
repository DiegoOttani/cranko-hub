# CrankoHub — próximos updates

Lista do que ainda vamos fazer no CrankoHub. Cada item tem detalhes suficientes para começar sem precisar redescobrir o projeto.

## Como o projeto funciona (referência rápida)

- **Tecnologia:** WPF + Blazor Hybrid (`BlazorWebView`), .NET 10. As telas são `.razor` em `Pages/`, a lógica fica em C# em `Services/` e os modelos em `Models/`.
- **Dados:** um único arquivo JSON em `%LocalAppData%\CrankoHub\dados.json`, gerenciado por `Services/ArmazenamentoService.cs`.
  - Raiz: `EstadoApp` (`Models/EstadoApp.cs`).
  - Campo novo exige três passos:
    - adicionar no modelo;
    - tratar no `Normalizar` (`??= []`, remover nulos, gerar ids faltando);
    - se fizer sentido, incluir nos dados de exemplo (`Exemplo`) e nos contadores de `Pages/Dados.razor`.
  - Campos novos devem ser opcionais, para backups antigos continuarem importando.
- **Gráficos:** `Components/GraficoLinha.cs` e `Components/GraficoBarras.cs` (dados em `Models/Graficos.cs`: `BarraDado`, `SerieLinha`, `FormatoNumero`).
- **Formatação pt-BR:** `Services/Formatacao.cs` (`Money`, `Num`, `Pf` para ler "1.234,56", `DataBR`, `MesLabel`, `UltimosMeses`, `Plural`, `Hoje`).
- **Alertas:** `Services/Alertas.cs` é a fonte única do card "Precisa de atenção" do Início e das notificações do Windows (`Services/AvisosService.cs`).
- **Menu:** `Layout/LayoutPrincipal.razor` (NavLink + `TituloDaPagina`). Ícones em `Components/Icones.cs`.
- **Publicar o .exe:** `dotnet publish -c Release -p:PublishProfile=win-x64` gera `publicar\CrankoHub.exe`.
- **Lançar uma versão:** `git tag crankohub-v1.2.0` e `git push origin crankohub-v1.2.0`. O workflow `.github/workflows/crankohub-release.yml` compila e cria o Release com o `CrankoHub.exe`; a versão vem da tag.
- **Atualizar os outros PCs:** Configurações → Atualizações → "Verificar atualizações" → "Atualizar agora" (`Services/AtualizacaoService.cs`). Sem token: os Releases vêm do repositório público `DiegoOttani/cranko-hub`. PC novo: baixar o `CrankoHub.exe` do Releases.

---

## 1. Finanças (só despesas) — próximo da fila

**Objetivo:** uma tela que mostra para onde vai o dinheiro no mês. Ela junta os gastos lançados à mão com os que o app já conhece (combustível, manutenção, documentos do carro e assinaturas), sem precisar digitar nada duas vezes.

**Decisões já tomadas:**
- Só **despesas**: sem receitas e sem saldo.
- Os gastos das outras áreas entram **sozinhos**, como lançamentos automáticos, só para leitura.

### Modelo
- `Models/Despesa.cs`: `Id`, `Data` (DateOnly), `Descricao`, `Valor` (double), `Categoria`, `Pagamento?` (cartão, Pix...), `Obs?`.
- `Models/EstadoApp.cs`: nova propriedade `Financas` (classe `DadosFinancas`) com `List<Despesa> Despesas` e `double? OrcamentoMensal` (limite de gastos do mês, opcional).
- `ArmazenamentoService`:
  - no `Normalizar`: `s.Financas ??= new()`, `Despesas ??= []`, remover nulos e gerar ids;
  - no `Exemplo`: ~20 despesas espalhadas nos últimos 6 meses (Mercado, Restaurante, Farmácia, Lazer...) e um orçamento de exemplo.

### Cálculos — `Services/FinancasCalculos.cs`
- `record Lancamento(DateOnly Data, string Descricao, double Valor, string Categoria, string Origem, string? Link, Despesa? Despesa)`.
  - Valores de `Origem`: `"manual"`, `"combustivel"`, `"manutencao"`, `"documento"`, `"assinatura"`.
  - `Link` é a tela de onde o lançamento vem: `carro?aba=abastecimentos&veiculo=...`, `carro?aba=manutencao&veiculo=...`, `carro?aba=documentos&veiculo=...` ou `assinaturas`.
- `DoMes(EstadoApp d, int ano, int mes)` junta:
  - despesas manuais do mês;
  - abastecimentos com `Valor` → categoria **"Carro · Combustível"**, descrição tipo "Gasolina · 36,5 L · {veículo}";
  - manutenções com `Valor` → **"Carro · Manutenção"** (descrição: nome do tipo + descrição);
  - documentos **pagos** com `Valor` → **"Carro · Documentos"**, usando o `Vencimento` como data;
  - cobranças das assinaturas **ativas** no mês → **"Assinaturas"**.
- **Cobranças de assinatura no mês:** cuidado aqui.
  - Hoje `AssinaturasCalculos` só calcula para frente (`ProximaCobranca`, `CobrancasAte`). É preciso uma função nova `CobrancasNoPeriodo(Assinatura a, DateOnly inicio, DateOnly fim)`.
  - Ela parte de `a.Vencimento` e anda para trás e para frente pelo ciclo, com `CicloCobranca.Buscar(a.Ciclo).Avancar(a.Vencimento, n)` e `n` negativo/positivo.
  - O modelo `Assinatura` não tem data de início, então meses passados são **estimados**. A tela deve mostrar "estimado pelo ciclo" nesses lançamentos.
  - Assinaturas canceladas (`Ativa = false`) ficam de fora.
- `PorCategoria(List<Lancamento>)` → `List<BarraDado>` ordenada do maior para o menor.
- `Mensal(EstadoApp d, int meses = 12)` → total por mês (`BarraDado`, rótulo com `Formatacao.MesLabel`).
- `CategoriasSugeridas`: Mercado, Restaurante, Delivery, Farmácia, Saúde, Casa, Contas, Educação, Lazer, Roupas, Presentes, Pets, Transporte, Outros.

### Tela — `Pages/Financas.razor` (`@page "/financas"`)
- **Menu:** item "Finanças" entre Assinaturas e o separador, com ícone novo em `Icones.cs` (carteira ou cifrão). Adicionar `"financas" => "Finanças"` no `TituloDaPagina` do layout.
- **Seletor de mês:** `‹ outubro 2026 ›`. O mês escolhido fica guardado num campo `static`, como `_periodo` em `Pages/CarroAbas/AbaConsumo.razor`.
- **KPIs** (classe `kpis`):
  - total do mês;
  - comparação com o mês anterior (componente `Delta`, com `BomQuandoCai = true`);
  - média dos últimos 6 meses;
  - orçamento restante com barra `progress` (se `OrcamentoMensal` definido; senão, um link "definir orçamento");
  - maior categoria.
- **Formulário "Nova despesa"**, seguindo o padrão do formulário de `Pages/Assinaturas.razor`:
  - Data, Descrição*, Valor* (texto lido com `Pf`), Categoria (input com `datalist` de `CategoriasSugeridas` + categorias já usadas), Forma de pagamento, Observação;
  - edição e cancelar, como nas outras telas;
  - campo para definir o orçamento mensal, numa linha separada ou num pequeno diálogo.
- **Gráficos:**
  - "Gastos por categoria" (`GraficoBarras` com `PorCategoria`);
  - "Gasto mensal" dos últimos 12 meses (`Mensal`).
- **Tabela do mês:**
  - filtro em chips: Todos / Manuais / Automáticos;
  - colunas: Data, Descrição, Categoria, Origem (badge), Pagamento, Valor;
  - lançamentos manuais têm editar/excluir, com confirmação via `IDialogoService.Confirmar`;
  - os automáticos têm um link "ver" que abre a tela de origem (`<a href="@Link">`), sem editar/excluir.

### Integrações
- **Início (`Pages/Inicio.razor`):** link-card "Finanças" com gasto do mês, comparação com o mês anterior e maior categoria, junto dos cards de Assinaturas/Dieta/Carro.
- **Alertas (opcional):** em `Services/Alertas.cs`, avisar quando o gasto do mês passar de 90% do `OrcamentoMensal`. A chave deve ser `orcamento:{ano}-{mes}`, para notificar uma vez por dia.
- **`Pages/Dados.razor`:**
  - contador "Despesas";
  - incluir `d.Financas.Despesas.Count` no `temDados` do "Carregar exemplo";
  - texto do "Apagar tudo" citando as despesas.

### Como testar
1. `dotnet build` sem erros.
2. Carregar os dados de exemplo e conferir que o total do mês bate com a soma de: manuais + abastecimentos com valor + manutenções + documentos pagos + cobranças de assinaturas do mês.
3. Trocar de mês e conferir o comparativo com o mês anterior.
4. Criar, editar e excluir uma despesa manual.

---

## 2. Contas a pagar

- **O que é:** luz, água, internet, aluguel, cartão de crédito. Parecido com Assinaturas, mas o **valor muda todo mês** e cada mês precisa ser marcado como **pago**.
- **Modelo:**
  - `ContaFixa` (nome, dia de vencimento, categoria, valor médio);
  - `PagamentoConta` (conta, mês/ano, valor, pago em).
- **Integrações:**
  - entrar nos alertas ("Conta de luz vence amanhã");
  - entrar nas Finanças como origem `"conta"`.

## 3. Exportar para Excel/CSV

- Botões "Exportar CSV" nas tabelas deç abastecimentos, manutenões, documentos, assinaturas e (depois) despesas.
- Usar `;` como separador e números no formato pt-BR, para abrir certo no Excel brasileiro. Salvar via `SaveFileDialog`, como em `DialogoService.SalvarBackup`.

## 4. Treinos

- Registro de academia: exercício, carga, séries e repetições, por dia.
- Gráfico de evolução da carga por exercício e volume semanal. Conversa com a Dieta (peso × força).

## 5. Hábitos

- Checklist diário (água, leitura, treino, dormir cedo...), com sequência de dias e um mapa de calor do ano.
- Alerta à noite se o checklist do dia não foi preenchido.

## 6. Saúde -> IMPORTANTE POREM AINDA MUITO CEDO PARA FAZER

- Remédios com horário (notificação pela bandeja), consultas e exames com data.
- Resultados de exames ao longo do tempo (ex.: colesterol, glicemia) em gráfico de linha.

## 8. Lista de tarefas ✅ (feito)

- Página **Tarefas** com quadros estilo Kanban (Trabalho, Faculdade, Pessoal, Setup... o usuário cria os seus, com emoji).
- Cada quadro tem colunas (padrão *A fazer / Fazendo / Feito*; a última conta como concluída) e cards arrastáveis com prazo, prioridade, etiquetas, descrição e checklist.
- Visão **Hoje** junta, de todos os quadros, o que vence hoje, está atrasado ou foi marcado como foco (☆).
- Resumo do dia no Início, tarefas atrasadas nos alertas e, a partir das 20h, uma notificação com o que foi concluído e o que ficou pendente.

## 9. Melhoria na configurações ✅ (feito)

- Card **Módulos** em Configurações: um interruptor para Dieta, Carro, Assinaturas, Entretenimento e Tarefas.
- Um módulo desligado some do menu, dos cards do Início, do "Precisa de atenção" e das notificações (inclusive o resumo noturno das Tarefas).
- **Os dados não são apagados**: ligar de novo traz tudo de volta, e o backup continua com tudo. A página ainda abre pelo endereço.
- Fica salvo em `config.modulosOcultos`, uma lista dos **escondidos**, para que módulos novos já apareçam ligados.
- No Início, quando só um card de uma dupla está visível, ele ocupa a linha inteira.

## 10. Versão para Linux (Linux Mint) ✅ (feito e testado no Mint)

**Abordagem:** um **segundo projeto**, `Linux/CrankoHub.Linux.csproj`, com [Photino.Blazor](https://github.com/tryphotino/photino.Blazor) (janela nativa com WebKitGTK). O projeto Windows (WPF) continua exatamente como era. O projeto Linux compila **os mesmos arquivos** por link (`Pages/`, `Components/`, `Layout/`, `Models/`, `Services/`, `wwwroot/`) e só troca as partes exclusivas do Windows, que ficam em `Linux/Plataforma/` com o mesmo nome e API:

| Windows | Linux (`Linux/Plataforma/`) |
|---|---|
| Janela WPF (`App.xaml`, `MainWindow.xaml`) | `Linux/Program.cs` com `PhotinoBlazorAppBuilder` |
| `BandejaService` (ícone na bandeja + balões) | Sem bandeja (fechar a janela encerra o app); notificações pelo `notify-send` |
| `DialogoService` (diálogos do Windows) | Diálogos GTK do Photino |
| `InicializacaoWindows` (Registro) | `~/.config/autostart/crankohub.desktop` |
| `AtualizacaoService` (troca o `.exe`) | Baixa `CrankoHub-linux-x64.tar.gz`, extrai ao lado e troca a pasta |
| `DispatcherTimer` do WPF (usado pelo `AvisosService`) | Substituto mínimo com `System.Threading.Timer` |
| Mutex de instância única | Arquivo de trava em `~/.local/share/CrankoHub/instancia.lock` |
| Capas em `https://capas.crankohub/` (WebView2) | Pasta das capas servida em `capas/` (`CapasService.Prefixo`) |

Os dados ficam em `~/.local/share/CrankoHub/dados.json`, e os backups do Windows importam direto.

### Publicação e instalação
- O workflow `crankohub-release.yml` ganhou o job `linux`, que anexa o `CrankoHub-linux-x64.tar.gz` ao mesmo Release. O pacote é autossuficiente e não precisa instalar o .NET.
- No Mint: extrair o `.tar.gz` e rodar `./instalar.sh`. Ele copia o app para `~/.local/share/CrankoHub/app` e cria o atalho no menu do Cinnamon.
- Precisa do `libwebkit2gtk-4.1-0`, que já vem no Mint 21/22. O instalador avisa se faltar.
- Para gerar o pacote manualmente: `dotnet publish Linux/CrankoHub.Linux.csproj -c Release -o out`.

### Falta / próximos passos
- Testado no Mint com Cinnamon: instalação pelo Release e funcionamento geral OK.
- Ícone na bandeja (AppIndicator), se fizer falta.
- Os textos "Windows" em Configurações aparecem também no Linux.

## 11. Repositório público e atualização automática sem token ✅ (feito)

- O CrankoHub agora mora no repositório público [DiegoOttani/cranko-hub](https://github.com/DiegoOttani/cranko-hub), com o histórico trazido do `MyProjs_`. Na raiz ficam o `cranko-hub.slnx` (Windows + Linux), o `.gitignore` e o workflow `crankohub-release.yml`.
- O `AtualizacaoService` (Windows e Linux) consulta os Releases **sem token**. O GitHub permite 60 consultas por hora por IP, de sobra para verificar uma vez por dia.
- "Verificar sozinho" funciona sem configuração nenhuma, e o campo do token saiu de Configurações. `TokenGitHub` continua no modelo, sem uso, para não quebrar backups antigos.
- O `UserAgent` da busca do Entretenimento aponta para o repositório novo.
- **Atenção:** quem já tem o app instalado de antes continua olhando o `MyProjs_`. Nesses PCs, instale uma vez manualmente o `.exe` do primeiro Release do repositório novo; daí em diante ele se atualiza sozinho.
- Ideia futura, para ficar como o ai-usagebar: item "Verificar atualizações" no menu da bandeja (`BandejaService`).

## 12. Usar em mais de um computador ✅ (feito, falta testar com dois PCs)

**Abordagem:** pasta sincronizada (OneDrive, Google Drive, Dropbox), sem servidor e sem login. Cada pessoa usa a própria nuvem, então funciona igual no app público, sem custo nem dados de terceiros com a gente.

- Configurações → Armazenamento → **"Usar pasta sincronizada…"**. Se a pasta já tem um `dados.json` (o outro PC), o app usa os dados de lá e guarda os daqui como `dados.antes-sincronizar-*.json`. Se não tem, copia os dados e as `capas/` para lá. Depois o app reinicia.
- A pasta escolhida fica em `%LocalAppData%\CrankoHub\local.json`, que é de cada PC. "Iniciar com o Windows" também passou para lá, para não ligar sozinho no outro PC. WebView2 e a trava de instância continuam na pasta local.
- Com o app aberto, um `FileSystemWatcher` percebe quando o outro PC sincronizou. O app recarrega os dados, recria a página aberta e mostra um toast.
- **Conflito:** se o arquivo mudou por fora e o app vai salvar antes de recarregar, a versão de fora vira `dados.conflito-*.json` e nada se perde. Configurações lista essas cópias, inclusive as do OneDrive (`dados-NOMEDOPC.json`).
- Se a pasta sincronizada não existir ao abrir (ex.: OneDrive desconectado), o app usa os dados locais e avisa em Configurações.
- "Voltar para a pasta local" copia os dados de volta e não apaga a pasta sincronizada.
- Limitação: usar os dois PCs **ao mesmo tempo** não junta as mudanças. A última gravação vale, e a outra fica como cópia de conflito.

### Futuro (só se tiver versão celular/web): servidor com login
- Ex.: Supabase (Postgres + autenticação pronta), com a senha guardada com hash (Argon2/bcrypt) pelo próprio serviço, nunca no app.
- Ganhos: sincronização de verdade, inclusive no celular, e login com e-mail.
- Riscos e custos:
  - Os dados sairiam do JSON único para tabelas, e cada tela teria que salvar no servidor.
  - Precisaria de cópia offline e de juntar as mudanças.
  - Os dados de saúde, gastos e documentos ficariam sob nossa responsabilidade (LGPD, vazamentos, regras de acesso).
  - Custo se crescer.

## Pendências de teste do que já existe

- **Iniciar com o Windows** (Configurações → Aplicativo): conferir se a entrada aparece em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` e se o app abre na bandeja ao ligar o PC.
- **Notificações:** conferir a aparência no Windows e se o clique abre a tela certa.
- **Diálogos:** "Restaurar" um backup automático e "Marcar como pago" em Documentos (incluindo a pergunta de criar o do próximo ano).
