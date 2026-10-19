# Verificações e roteiro de testes

Este documento apresenta as verificações automatizadas e os cenários de testes manuais do CdG. Em 19/10/2026, a compilação com dotnet build foi concluída com sucesso, e as 21 verificações automatizadas passaram, incluindo 700 combinações de rateio. Os testes utilizam SQLite em memória e não substituem os testes manuais e de concorrência no PostgreSQL, que permanecem pendentes.

## 1. Verificações automatizadas existentes

Na raiz:

```powershell
dotnet run --project tests/CdG.Verificacao/CdG.Verificacao.csproj
```

O projeto é console. Uma condição falsa lança exceção e interrompe a execução. Se todos os cenários chegarem ao fim, o código imprime `21 verificações concluídas`, seguido do aviso sobre limites do SQLite.

| Grupo | O que o código verifica |
|---|---|
| Rateio | 700 combinações: totais de 0 a 99 centavos e 1 a 7 pessoas, com preservação da soma e diferença máxima de um centavo. |
| Despesas | Rejeição de usuário estranho e valor negativo, persistência e bloqueio de anulação alheia. |
| Fechamento | Bloqueio em evento ativo, exigência de organizador, cotas e fechamento único. |
| Saldos | Cenário de R$ 100 + R$ 20 dividido entre três pessoas. |
| Pagamentos | Limite da dívida, pendência sem efeito no saldo e conflitos de pendência. |
| Confirmação | Recebedor obrigatório, pagamento parcial e repetição sem duplicidade. |
| Cancelamento e renúncia | Cancelamento mantém dívida, renúncia aguarda pendências e gera registros por beneficiário. |

O banco é SQLite em memória, criado com `EnsureCreatedAsync`. O interceptor `SemForUpdate` retira `FOR UPDATE` dos comandos nesse teste. Logo, não verifica migrations PostgreSQL, bloqueios de linha, concorrência real, navegação, antiforgery, e-mail, Docker ou hospedagem. Os cenários também não cobrem integralmente os handlers de eventos e participação.

## 2. Preparação dos testes manuais

Use PostgreSQL separado e quatro contas fictícias A, B, C e D em perfis de navegador independentes. Não altere diretamente o banco para simular regras de domínio. Quando um cenário exigir que o horário tenha passado, crie evento futuro próximo e aguarde antes de concluir.

Registre para cada cenário: revisão do código, ambiente, dados iniciais, passos, resultado esperado, observado e data. Não inclua senhas ou tokens nas evidências.

## 3. Conta, eventos e participação

| ID | Ação | Resultado esperado pelo código |
|---|---|---|
| C01 | Cadastrar, sair, entrar e alterar nome em Development | Cadastro local sem confirmação obrigatória; nome atualizado na sessão. |
| C02 | Acessar página protegida sem sessão | Desafio de autenticação. |
| E01 | A cria evento futuro com limite três | A é organizador e já ocupa uma vaga confirmada. |
| E02 | A convida B, C e D cadastrados | Convites e vínculos pendentes; avisos internos. Convite não reserva vaga. |
| E03 | B e C aceitam; D tenta aceitar | A/B/C confirmados; D permanece pendente por lotação. |
| E04 | C cancela presença sem despesas; D aceita | Vaga liberada e ocupada por D. |
| E05 | B recusa em outro evento e depois confirma pelos detalhes | Convite permanece historicamente recusado; presença pode mudar para confirmada, respeitadas condições. |
| E06 | Editar em duas abas e salvar a segunda desatualizada | Rejeição da versão antiga. Repetir em Adiar. |
| E07 | Reagendar evento com confirmados | Confirmados ficam pendentes; avisos aos outros vínculos ativos. |
| E08 | Reduzir limite abaixo dos confirmados | Rejeição da capacidade. |
| E09 | Promover alguém sem aceite e depois alguém elegível | Primeiro bloqueado; segundo promovido e notificado. |
| E10 | Último organizador sai com sucessor elegível | Sucessor mais antigo elegível assume; quem saiu perde acesso aos detalhes. |
| E11 | Último organizador tenta sair sem sucessor | Operação bloqueada; evento preservado. |
| E12 | Convidar novamente alguém que saiu | Retorno bloqueado. |
| E13 | Concluir antes e depois do horário | Antes bloqueado; depois permitido se Agendado. |
| E14 | Cancelar como organizador que não criou o evento | Permitido em Agendado/Adiado; não existe exclusividade do criador. |
| H01 | Ocultar e restaurar evento encerrado | Altera apenas o histórico da pessoa; restaurar não desfaz saída. |
| N01 | Ler um aviso e marcar todos como lidos | Contagem atualizada na próxima renderização; nenhuma leitura de aviso alheio. |

## 4. Financeiro ponta a ponta

Em evento independente, mantenha A/B/C confirmados. A registra R$ 100, B registra R$ 20, C não registra despesas.

| ID | Ação | Resultado esperado |
|---|---|---|
| F01 | B tenta anular despesa de A | Bloqueado. |
| F02 | Autor de despesa ativa tenta sair ou cancelar presença | Bloqueado até anulação ou resolução pelo fluxo permitido. |
| F03 | Anular despesa e cadastrar substituta antes de fechar | Linha anulada preservada e excluída do total. |
| F04 | Tentar fechar ativo; depois concluir e fechar como A | Primeiro bloqueado; depois cotas de R$ 40 e saldos A +60, B −20, C −40. |
| F05 | B tenta fechar; A tenta fechar novamente | Ambas as operações bloqueadas. |
| F06 | Registrar/anular despesa após fechar | Bloqueado. |
| F07 | B informa R$ 20 para A | Registro pendente; saldos ainda não mudam. |
| F08 | B tenta confirmar; A confirma | B bloqueado; A confirma; saldos A +40, B 0, C −40. |
| F09 | C informa R$ 40 e cancela; depois informa novamente e A confirma | Cancelamento não quita; confirmação posterior zera saldos. |
| F10 | Em novo cenário, informar valor parcial e confirmar | Redução parcial da dívida/crédito. |
| F11 | Informar zero, negativo, texto, milhar ambíguo, três casas ou valor acima da dívida | Rejeição; nenhuma alteração financeira. |
| F12 | Renunciar com acerto pendente | Bloqueado. |
| F13 | Renunciar sem pendências com saldo positivo | Crédito restante zerado e benefícios registrados em ordem fixa de IDs. |
| F14 | Cancelar evento com confirmados e despesas | Evento cancelado, financeiro separado; fechamento possível sob suas condições. |
| F15 | Repetir confirmação já concluída | Sem duplicar efeito no saldo. |

## 5. Autorização e interações que exigem atenção

| ID | Cenário | Comportamento a conferir |
|---|---|---|
| A01 | D sem vínculo abre financeiro alheio | Recurso não disponível. |
| A02 | POST com IDs de despesa/acerto de outro evento | Nenhuma alteração indevida. |
| A03 | Participante tenta ações de organizador por POST direto | Servidor rejeita, independentemente de botão visível. |
| A04 | Convidado ainda pendente abre financeiro | O código permite leitura por vínculo não saído; decisão de produto a revisar, não um bloqueio já implementado. |
| A05 | Usuário oculta evento e acessa URL financeira | Ausência de saída permite acesso direto; detalhes e permissão de organizador seguem filtros diferentes. |
| A06 | POST sem token antiforgery válido | Deve ser rejeitado pela infraestrutura Razor Pages. |
| R01 | Com despesas ativas, reagendar e cancelar antes de reconfirmação | Pode bloquear fechamento por falta de confirmados/pagador não confirmado. Limitação identificada por leitura; registrar observação real e tratar separadamente. |
| R02 | Tentar excluir conta com histórico | Operação bloqueada e dados preservados. |

Não use uma alteração direta de status no banco para transformar R01 em “aprovado”. O cenário evidencia uma interação de regras que requer decisão e implementação futura.

## 6. Concorrência no PostgreSQL

Execute com sessões separadas e registre o estado final, não apenas as mensagens da interface.

| ID | Operações simultâneas | Resultado a validar |
|---|---|---|
| P01 | Dois aceites para a última vaga | Apenas um confirma; limite preservado. |
| P02 | Saída de dois organizadores | Sucessão ou bloqueio preserva organização. |
| P03 | Fechar rateio e registrar despesa | Despesa entra antes do fechamento ou é rejeitada depois; cotas consistentes. |
| P04 | Dois devedores informam pagamento ao mesmo credor | Apenas pendência permitida pelas regras; segundo aguarda resolução. |
| P05 | Confirmar o mesmo pagamento em duas sessões | Efeito contabilizado uma única vez. |

## 7. Antes de hospedar uma demonstração

Validar cadastro com confirmação real, recuperação de senha, remetente SMTP, tratamento de falha de envio, HTTPS, proxy conhecido, persistência das chaves, restauração de backup e reinício do aplicativo. Construir e executar a imagem Docker com o banco de demonstração. Conferir comportamento de datas/fuso no ambiente escolhido.

Publicar código não constitui evidência desses testes. Relatórios anteriores devem manter sua data e revisão; resultados novos precisam de registro próprio.

[README](../README.md) · [Guia do código](GUIA_DO_CODIGO.md) · [Instalação](INSTALACAO.md)
