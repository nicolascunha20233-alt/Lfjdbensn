# JoJo Stands v1.5.0 (Hollow Knight mobile)

## Stands (dependem dos amuletos equipados)
- Sombra do Cavaleiro: padrao
- Star Platinum: Heavy Blow + Quick Slash
- Gold Experience: Lifeblood Heart + Hiveblood
- Purple Haze: Fury of the Fallen + Spore Shroom (passivo: socos/barragem mais rapidos)
(Para mudar as combinacoes, edite StarCharmA/B, GoldCharmA/B e HazeCharmA/B no inicio do .cs)

## Controles
- Toque rapido na magia rapida: Stand aparece/some atras do Cavaleiro
- Botao do ferrao (toques): murro; clicando varias vezes faz combo e termina com soco forte
- Segurar o ferrao: barragem
- Segurar a cura (33 Soul): habilidade especial, depende da direcao mirada

| Stand | Neutro | Mirando para CIMA | Mirando para BAIXO |
|---|---|---|---|
| Sombra | 3 orbes de alma | leque de 5 orbes para cima | explosao de alma no chao |
| Star Platinum | The World (5 s) | Star Finger (dedo longo para cima) | soco no chao: joga inimigos para cima e barragem no ar |
| Gold Experience | cura 2 mascaras | cura 3 mascaras (66 Soul) | igual ao neutro |
| Purple Haze | nuvem de veneno a frente (5 s) | 3 capsulas de virus em arco que explodem em nuvens | surto: grande nuvem de veneno ao redor (6 s) |

Soco no chao e Star Finger tambem quebram objetos quebraveis proximos (melhor esforco; pisos
especiais que so a magia Desolate Dive quebra podem nao quebrar).

## Compilar pelo celular (sem PC)
1. Crie uma conta no github.com e um repositorio novo.
2. Envie o conteudo desta pasta (inclusive .github e Managed) para o repositorio.
3. Aba Actions > build > espere ficar verde > baixe o artefato JoJoStandMod (zip com o JoJoStandMod.dll).

## Instalar
Coloque o JoJoStandMod.dll em Mods/JoJoStandMod/ do jogo
(a mesma pasta dos mods instalados pelo gerenciador de mods do port).

## Sprites
O Star Platinum usa sprites reais extraidos dos personagens MUGEN enviados (Jotaro-SPR), embutidos em StandSprites.cs.
Sombra, Gold Experience e Purple Haze ainda usam silhuetas desenhadas por codigo.
Use os sprites apenas no seu mod pessoal; os creditos sao dos criadores dos personagens originais.

## Nome e versao (reconhecimento no jogo)
- Nome do mod: "JoJo Stands"; versao 1.5.0 (constantes ModName/ModVersion no .cs, e Version no .csproj).
- Ao carregar, o mod aparece na lista de mods do Modding API com esse nome e versao.
- Para aparecer no instalador de mods do jogo, use o ModLinks-exemplo.xml como modelo (precisa hospedar o zip).
- Ao mudar de versao, altere ModVersion no .cs e Version/AssemblyVersion/FileVersion no .csproj.
