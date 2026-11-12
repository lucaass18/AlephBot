# As tabelas

As entidades que viram tabela no Postgres. Ficam separadas dos repositórios porque os nomes
andam em pares que diferem por uma letra — `ColetaDeStats` e `ColetorDeStats`,
`ConfigDoServidor` e `ConfigDosServidores`, `PlaylistSalva` e `PlaylistsSalvas` — e lado a lado
na mesma pasta isso se lê errado. Singular é a tabela; plural é quem fala com ela.

## O namespace de propósito não acompanha a pasta

Todo o resto do projeto segue pasta igual a namespace. Aqui não: estes arquivos estão em
`Dados/Tabelas/` e continuam em `namespace AlephBot.Threnodian.Dados`.

O motivo é o EF. O snapshot do modelo e todos os arquivos de migração guardam a entidade pelo
**nome completo em texto**:

```csharp
modelBuilder.Entity("AlephBot.Threnodian.Dados.ObraGuardada", b => ...)
```

Mudar o namespace faria o EF enxergar as tabelas antigas como removidas e outras tantas como
novas. A próxima migração gerada sairia com `DROP` e `CREATE` em cima de um banco com dados
dentro.

Se um dia valer a pena mesmo assim, o caminho é mover, regenerar o snapshot e conferir que a
migração resultante está **vazia** antes de aplicar em qualquer lugar.
