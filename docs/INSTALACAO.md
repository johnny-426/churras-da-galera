# Instalação e execução do CdG

Este guia corresponde aos arquivos da revisão documental de 19/10/2026. Todos os comandos partem da raiz que contém `CdG.slnx`, salvo indicação diferente. Os exemplos usam PowerShell e marcadores de credenciais, não senhas reais.

## 1. Ambiente necessário

- SDK .NET 10; `global.json` parte de `10.0.100` e permite avançar para faixas de recursos posteriores da família 10.0.
- PostgreSQL acessível e uma conta com permissões para criar o esquema no banco escolhido.
- Acesso aos pacotes NuGet no primeiro restore.
- Visual Studio com suporte ao projeto e carga ASP.NET/web, se utilizada a IDE. Também é possível usar terminal e editor.

```powershell
dotnet --list-sdks
```

O `.csproj` referencia Identity/EF Tools 10.0.12, Npgsql EF Core 10.0.3, Web CodeGeneration Design 10.0.2 e NuGet.Packaging/Protocol 6.12.5. Estes são os números do projeto analisado; não são uma recomendação para atualizar dependências sem verificação.

## 2. Abrir o projeto e preparar o banco

Extraia ou clone a versão pública escolhida e localize `CdG.slnx`. No Visual Studio, abra a solução; se necessário, abra diretamente `CdG/CdG.csproj`. O programa de testes é um projeto separado e não está listado nessa solução.

Crie um banco exclusivo de desenvolvimento, como `cdg_demo`, no servidor PostgreSQL. O pgAdmin é opcional. A conta de banco é diferente das contas cadastradas no site; não use a senha do site como configuração de conexão por confusão entre esses papéis.

Este guia não exige reutilizar nem apagar bancos anteriores. Aplique migrations somente depois de conferir servidor, porta e nome do banco. Em bancos com dados relevantes, a preparação inclui backup e uma estratégia de atualização.

## 3. Configurar a conexão local

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=cdg_demo;Username=SEU_USUARIO;Password=SUA_SENHA" --project CdG/CdG.csproj
```

Substitua os valores pelo ambiente local. Caracteres especiais em senhas precisam ser tratados conforme a sintaxe do shell e da connection string; não publique a linha preenchida em prints, logs ou documentação. No Visual Studio, a alternativa é **Gerenciar Segredos do Usuário** no projeto CdG:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=cdg_demo;Username=SEU_USUARIO;Password=SUA_SENHA"
  }
}
```

O `UserSecretsId` já existe no `.csproj`; não é necessário executar `user-secrets init`. Cópias desse projeto no mesmo computador podem compartilhar os mesmos segredos. Alterar a conexão pode afetar outra cópia local que use esse identificador.

O perfil `https` define `ASPNETCORE_ENVIRONMENT=Development`, permitindo carregar os segredos locais pelo builder padrão. Uma variável `ConnectionStrings__DefaultConnection` no ambiente pode substituir a configuração: confira se não aponta para outro banco. Não é necessário imprimir a senha para verificar o destino.

## 4. Restaurar, compilar e aplicar migrations

```powershell
dotnet restore CdG/CdG.csproj
dotnet build CdG/CdG.csproj
dotnet run --project CdG/CdG.csproj --launch-profile https -- --migrate
```

O último comando executa `Database.Migrate()` e encerra. O `--` separa argumentos do `dotnet run` dos argumentos da aplicação. As migrations já acompanham o código; não gere uma nova apenas para instalar. A última é `20261018123556_AdicionarFinanceiro`.

Alternativa no **Console do Gerenciador de Pacotes** do Visual Studio, com projeto padrão CdG:

```powershell
Get-Migration
Update-Database
```

Escolha um caminho de aplicação das migrations; não é preciso usar os dois. Para conferir o banco aberto no pgAdmin:

```sql
SELECT current_database();
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```

Essa consulta confirma o banco da sessão do pgAdmin; ele precisa ser o mesmo configurado para a aplicação. A migration de preenchimento de aceites antigos bloqueia sua própria reversão, portanto não use rollback como procedimento genérico de instalação.

## 5. Iniciar a aplicação

```powershell
dotnet dev-certs https --trust
dotnet run --project CdG/CdG.csproj --launch-profile https
```

O perfil define `https://localhost:7103` e `http://localhost:5171`. O procedimento de confiança do certificado pode depender do sistema operacional e navegador. O perfil `http` também existe, mas o pipeline contém redirecionamento HTTPS.

Crie contas fictícias em sessões diferentes para demonstração. Em Development, confirmação de e-mail não é exigida. Sem SMTP, o serviço de e-mail retorna sem enviar: recuperação de senha por e-mail não funcionará só porque o cadastro local funcionou.

## 6. Executar as verificações

```powershell
dotnet run --project tests/CdG.Verificacao/CdG.Verificacao.csproj
```

O executável cria SQLite em memória e não precisa da conexão do PostgreSQL para seus cenários. O projeto referencia o assembly web, mas não inicializa o servidor por `Program.Main`. Veja [TESTES.md](TESTES.md) para escopo, cenário esperado e verificações manuais.

## 7. Configuração de uma futura hospedagem

O projeto pode ser executado como aplicação .NET ou imagem Docker. A hospedagem não é criada por este guia. O ambiente externo precisa fornecer banco, HTTPS e SMTP, além das configurações:

| Chave de ambiente | Finalidade |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` para o ambiente externo. |
| `ConnectionStrings__DefaultConnection` | Conexão Npgsql para o banco hospedado, com TLS conforme a infraestrutura. |
| `AllowedHosts` | Hostnames autorizados para a instância. |
| `Smtp__Host` | Servidor SMTP. |
| `Smtp__Port` | Porta com STARTTLS; padrão do código: 587. |
| `Smtp__Username` / `Smtp__Password` | Credenciais configuradas fora do repositório. |
| `Smtp__From` | Remetente autorizado pelo serviço de e-mail. |
| `ReverseProxy__KnownProxy` | IP confiável, quando necessário à topologia da hospedagem. |
| `DataProtection__KeysPath` | Diretório persistente e gravável para chaves, com acesso restrito. |

Fora de Development, o cadastro exige confirmação. O serviço SMTP implementado usa `EnableSsl=true` no `SmtpClient`, com STARTTLS; não pressupõe compatibilidade com SMTP que exija TLS implícito na porta 465.

O arquivo de proxy aceita um IP pela configuração indicada. A configuração precisa corresponder à plataforma; não se deve habilitar confiança indiscriminada em cabeçalhos recebidos. Persistir chaves de Data Protection mantém a continuidade de tokens/cookies entre reinicializações, mas não resolve sozinho sua proteção em repouso.

### Docker

```powershell
docker build -t cdg:demo .
```

O Dockerfile usa SDK 10.0 para compilar e runtime ASP.NET 10.0 para executar, com usuário definido por `$APP_UID`. A porta interna é 8080. A plataforma fornece as variáveis e termina HTTPS conforme sua configuração. O repositório não inclui orquestração automática de PostgreSQL nem volume de chaves.

Em uma tarefa de implantação com acesso ao banco e configurações adequadas:

```powershell
dotnet CdG.dll --migrate
```

Esse comando é executado na pasta publicada ou no ambiente equivalente do container. Migrations não são aplicadas automaticamente a cada inicialização normal. Publicação em pasta por `dotnet publish` também não disponibiliza o site na internet.

## 8. Diagnóstico de problemas

| Sintoma | Verificação |
|---|---|
| `net10.0` não suportado | SDK instalado e seleção por `global.json`. |
| Conexão ausente | Nome exato `DefaultConnection`, projeto e ambiente de execução. |
| Conexão recusada | Serviço PostgreSQL, host, porta e acesso de rede. |
| Autenticação do banco falha | Credenciais PostgreSQL, não as do site. |
| Banco ou tabela inexistente | Destino correto e migrations aplicadas nele. |
| Cadastro externo falha | SMTP configurado e confirmação de conta. A conta pode já ter sido criada antes da falha de envio. |
| Erro de certificado local | Certificado de desenvolvimento e confiança no navegador. |
| Rateio não fecha | Evento encerrado, confirmados presentes e todos os pagadores ativos incluídos. |
| Reagendamento seguido de cancelamento impede rateio | Presenças podem estar pendentes; consultar a limitação no manual, sem contornar a regra editando o banco. |
| Saída bloqueada | Despesas próprias ativas ou ausência de sucessor para o último organizador. |
| Acerto não altera saldo | Falta confirmação do recebedor. |
| Evento oculto | Filtro Ocultos; restaurar não reativa quem saiu. |

## 9. Limites da verificação desta documentação

Os comandos foram confrontados com `Program.cs`, os arquivos de projeto, perfis e migrations. Não houve execução de build, migrations, Docker ou SMTP nesta revisão. A validação de uso externo requer os cenários descritos em [TESTES.md](TESTES.md), além de backup, configuração de contato e política de dados da instância.

[README](../README.md) · [Guia do código](GUIA_DO_CODIGO.md)
