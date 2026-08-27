SocketChat P2P - Trabalho Pratico 01 - Sistemas Distribuidos - FURB
Grupo: Gabriel Labes, Pedro Valle Mafessolli, Vinícius Mannerich Dalmonico, João Pedro Erhardt

Chat entre N participantes em malha completa, sem servidor central, usando apenas a classe Socket
sobre TCP.

Como executar:

    dotnet build
    dotnet run --project SocketChat -- 9001 alice
    dotnet run --project SocketChat -- 9002 bob 9001
    dotnet run --project SocketChat -- 9003 carol 9001

Comandos: /list, /msg apelido texto, /quit.


REQUISITO 9 - POLITICA PARA PARTICIPANTE QUE PARA DE CONSUMIR MENSAGENS

Politica adotada: enfileirar com limite por par (200 mensagens), descartando a mensagem mais nova
quando a fila enche, e desconectar o par se um envio nao completar em 5 segundos.

Cada conexao tem a sua propria fila de saida. Quem digita nunca escreve direto no socket: a mensagem
e colocada na fila de cada par e o remetente segue imediatamente. Um participante lento enche apenas
a fila dele e nao bloqueia quem enviou nem os outros participantes, que tem filas independentes.

Quando a fila de um par enche, a mensagem nova e descartada e um aviso aparece uma unica vez, ate
aquela fila voltar a aceitar mensagens. Se o par parar de ler de vez, a janela do TCP fecha e o
envio deixa de completar; passados 5 segundos o prazo estoura, a conexao e encerrada e o par e
removido da lista de participantes.

Justificativa

Enfileirar sem limite nao trava o remetente, mas faz a memoria crescer sem controle por causa de um
par que talvez nunca volte a ler.

Desconectar no primeiro sinal de lentidao e agressivo demais. Uma lentidao momentanea, como o
usuario segurando a rolagem do terminal, derrubaria um participante saudavel.

Descartar a mensagem mais antiga preservaria o texto recente, mas entregaria a conversa fora de
ordem.

Por isso a politica tem dois niveis: descarte com limite para lentidao passageira, e desconexao por
prazo para o par que travou de vez. O descarte e sempre local ao par lento, os demais continuam
recebendo todas as mensagens.

Implementacao: SocketChat/Peer/PeerOutbox.cs (fila limitada e descarte) e
SocketChat/Peer/PeerConnection.cs (prazo de envio de 5 segundos).
