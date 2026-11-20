# Compilar JoJoStandMod usando somente o celular

1. Extraia este ZIP no celular.
2. No navegador, entre em https://github.com e crie um repositorio novo (pode ser privado).
3. Abra o repositorio e use **Add file > Upload files**.
4. Envie o CONTEUDO da pasta `JoJoStandMod` extraida. Confira que foram enviados tambem a pasta oculta `.github` e a pasta `Managed`. O arquivo `.github/workflows/build.yml` precisa estar exatamente nesse caminho na raiz do repositorio.
5. Toque em **Commit changes** para enviar os arquivos.
6. Abra a aba **Actions** do repositorio. O workflow **Build JoJoStandMod** deve iniciar automaticamente depois do commit. Se nao iniciar, abra o workflow e use **Run workflow**.
7. Quando a execucao terminar com marca verde, abra a execucao e procure **Artifacts**. Baixe `JoJoStandMod`.
8. Extraia o artefato baixado: dentro dele deve estar `JoJoStandMod.dll`.

Se a opcao de enviar pastas nao aparecer no navegador do celular, use o seletor de arquivos para selecionar todos os arquivos e pastas visiveis. Se a pasta `.github` estiver oculta pelo gerenciador, ative a exibicao de arquivos ocultos antes de enviar. Nao envie apenas o ZIP como um arquivo unico: o GitHub nao o descompacta automaticamente no repositorio.

A compilacao ainda pode revelar erros no codigo ou dependencias. Se acontecer, abra a execucao vermelha em Actions e envie uma captura da parte final do log.
