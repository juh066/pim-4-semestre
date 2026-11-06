# Notificações da Galeria Aurora

O componente compartilhado `Avisos`, em `site.js`, apresenta o resultado das operações de cadastro, login, carrinho, ingressos, visitas e cancelamentos. Todas as telas usam o mesmo container definido em `_Layout.cshtml`:

```html
<div id="notificationContainer" class="notification-container"></div>
```

## Tipos

```javascript
avisos.success('Ingresso reservado.');
avisos.error('Não há vagas suficientes nesta sessão.');
avisos.warning('O preço mudou. Revise o carrinho.');
avisos.info('Você saiu da sua conta.');
```

- `success`: fundo verde-sálvia claro e `role="status"`.
- `error`: fundo terracota claro, `role="alert"` e permanência até fechamento manual.
- `warning`: creme com detalhe dourado e duração padrão de 7 segundos.
- `info`: creme neutro com detalhe dourado.

Sucessos e informações desaparecem após aproximadamente 5 segundos. O temporizador pausa enquanto o ponteiro ou o foco estiver na notificação. Mensagens repetidas do mesmo tipo não são empilhadas.

## Fechamento e acessibilidade

O controle textual **Fechar** é um botão acessível por teclado. A exibição do aviso não desloca o foco. Não há ícones decorativos: o texto deve sempre explicar claramente o resultado. Erros devem preservar a mensagem específica recebida do servidor quando disponível.

As transições são curtas e ficam praticamente instantâneas quando o navegador indica `prefers-reduced-motion: reduce`.

## Responsividade e estilos

Os estilos ficam em `site.css`, nas classes `.notification-container`, `.notification-banner`, suas variações por tipo, `.notification-content` e `.notification-close`.

No computador, o componente permanece no canto superior direito. Em telas de até 600 px, respeita margens laterais de 12 px e permite quebra de linha, inclusive para a confirmação acadêmica mais longa do checkout.

## Duração personalizada

Quando uma duração diferente for realmente necessária, use `show` com milissegundos. O valor `0` mantém a mensagem até o fechamento:

```javascript
avisos.show('Mensagem informativa.', 'info', 6000);
avisos.show('Mensagem persistente.', 'error', 0);
```
