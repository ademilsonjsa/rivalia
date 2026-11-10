# Rivalia (nome provisório)

Jogo mobile de ação top-down em arena 3D com conquista territorial. Unity 6 + C#.
O planejamento completo (GDD v1.0) está no documento do projeto.

## Estrutura

```
core/                 Regras da partida em C# puro (sem UnityEngine) + testes xUnit
  Rivalia.Core/        Match, economia, estruturas, captura, renascimento, vitória, IA estratégica
  Rivalia.Core.Tests/  36 testes, incluindo partidas completas simuladas
  sync-to-unity.sh     Copia o núcleo para game/Assets/_Rivalia/Scripts/Core
game/                 Projeto Unity 6 (Input System, Cinemachine 3, AI Navigation)
  Assets/_Rivalia/Scripts/Core     Cópia do núcleo (asmdef sem engine)
  Assets/_Rivalia/Scripts/Game     Arena, herói, projéteis, câmera, joysticks, bots, HUD
  Assets/_Rivalia/Scripts/Editor   Menu "Rivalia > Criar cena MVP"
  Assets/_Rivalia/Tests/PlayMode   Testes no Unity (arena real, física, NavMesh)
docs/evidencias/      Saídas reais dos testes
```

## Como rodar o MVP no Unity

1. Instale o Unity **6000.0 LTS** pelo Unity Hub (módulos Android e/ou iOS para celular).
2. Unity Hub → Add → selecione a pasta `game/`. Na primeira abertura o Unity baixa os pacotes do `Packages/manifest.json`.
3. Se perguntar sobre o novo Input System, aceite (ou em *Project Settings → Player → Active Input Handling* escolha **Input System Package (New)** ou **Both**).
4. Menu **Rivalia → Criar cena MVP** (cria e salva `Assets/_Rivalia/Scenes/Arena_ValeQuatroCoroas.unity`).
5. Aperte **Play**.

Controles no PC: **WASD** move, **mouse** mira, **clique** ataca, **Q** avanço, **E** super, **F** poção, **roda do mouse** zoom.
No celular: joystick esquerdo flutuante move; joystick direito mira e dispara ao soltar (toque rápido = mira assistida);
botões HAB (avanço), SUPER e Poção. Perto das suas estruturas aparecem os botões de melhoria e da loja.

Testes no Unity: *Window → General → Test Runner → PlayMode → Run All*.

## Como rodar os testes do núcleo (sem Unity)

```bash
cd core
dotnet test                                   # .NET 8
RIVALIA_EVIDENCE_DIR=../docs/evidencias dotnet test   # também grava os relatórios das simulações
```

## Regras implementadas (Fase 1)

- Movimento 360° e mira independentes; ataques seguem a direção escolhida (dual joystick, teclado/mouse, gamepad).
- Combate: projéteis com colisão, munição com recarga, intervalo entre disparos, avanço com recarga,
  super em área com dano contínuo, fórmula de dano com bônus e resistência (tetos 45% e 50%), regeneração fora de combate.
- Arena 3D: muros bloqueiam movimento, visão e projéteis; caixas bloqueiam só movimento.
- 4 regiões com Mina (produção, capacidade, coleta, sabotagem), Loja (5 itens), Torre (captura progressiva
  e contestável) e Base Militar (torreta automática, pode ser desativada); região central com bônus.
- Morte e renascimento na torre (original ou conquistada), eliminação sem torres, regra checada de novo no renascimento.
- Vitória por domínio (5 torres), último sobrevivente, limite de 8 min com desempate, empate, e encerramento quando
  nenhum humano pode mais vencer.
- 3 bots com perfis (Econômico, Defensivo, Agressivo, Expansionista, Equilibrado) e dificuldades, usando o mesmo
  HeroActor do jogador, NavMesh, mira com erro/antecipação, esquiva, compra e melhorias.

Validação de segurança já no núcleo: um acerto só vale se existir um disparo autorizado em voo e o alvo estiver ao
alcance do ponto de onde o tiro saiu; compras e melhorias exigem ser dono, estar perto e ter Cristais.

## Arte

Arte provisória com primitivas (cartunesca, cores de time). Personagens, nomes e mapa são originais.
Modelos, animações com Animator e efeitos finais entram nos prefabs sem mudar as regras.
