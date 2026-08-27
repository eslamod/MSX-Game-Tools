# Guía de git para este proyecto

Notas para trabajar con git aquí. Dos repos en juego:

- **`eslamod/MSX-Game-Tools`** — este repo. Es tuyo, eres el único que escribe en él.
- **`Libertium/sass-MSX`** — el ensamblador de las ROMs de prueba, incluido como
  submódulo en `tools/sass-MSX`. Repo compartido; tienes permiso de escritura.

---

## ¿Hace falta un pull request?

Un **pull request (PR)** es una propuesta: "quiero que estos commits de mi rama entren en
otra rama". Sirve para tener un sitio donde ver el diff junto, pasar CI y comentar antes
de fusionar. No es una orden de git, es una función de GitHub.

**`MSX-Game-Tools` (tu repo).** Técnicamente no lo necesitas: eres el dueño y podrías
empujar directo a `main`. Pero adoptar el flujo de rama + PR vale la pena igualmente:

- El diff queda revisable de un vistazo antes de mover `main`.
- CI (los tests) corre sobre la rama; `main` sólo avanza si está en verde.
- Si una idea no cuaja, se abandona la rama y `main` ni se entera.
- Se practica el flujo que se usa en cualquier proyecto con más gente.

**`sass-MSX` (repo compartido).** Aquí el PR sí es la vía natural: estás proponiendo
cambiar una rama (`master`) que usa más gente, así que se propone con un PR y se revisa.

---

## El flujo de rama + PR, paso a paso

Para cada asunto (un arreglo, una función, un cambio de docs):

```bash
# 1. Partir de main al día
git switch main
git pull

# 2. Rama nueva para el asunto, nombre en kebab-case
git switch -c arreglo-scroll-dialogo

# 3. Trabajar y commitear en la rama (un commit por asunto, el porqué en el cuerpo)
#    ... editar ...
git add -A
git commit

# 4. Correr la suite entera antes de empujar
dotnet test tests/MSX_GameTools.Tests/MSX_GameTools.Tests.csproj \
  --nologo -v q --blame-hang-timeout 120s

# 5. Empujar la rama
git push -u origin arreglo-scroll-dialogo
```

Git imprime una URL al empujar. Ábrela, o ve a:

```
https://github.com/eslamod/MSX-Game-Tools/compare/main...arreglo-scroll-dialogo?expand=1
```

y pulsa **Create pull request**. Cuando CI esté en verde, **Merge**. Después:

```bash
git switch main
git pull                                   # trae el merge
git branch -d arreglo-scroll-dialogo       # borra la rama local
git push origin --delete arreglo-scroll-dialogo   # y la remota
```

### Método de merge en GitHub

- **Squash and merge**: junta todos los commits de la rama en uno. Bien si la rama son
  varios commits de tanteo.
- **Rebase and merge**: pone los commits de la rama encima de `main` sin commit de
  merge. Historial lineal, limpio. Bien cuando los commits ya están bien hechos.
- **Create a merge commit**: deja un commit de merge. Aquí no hace falta.

Para este proyecto, con "un commit por asunto" ya cuidado, **Rebase and merge** deja el
historial más parecido al que hay ahora.

---

## El submódulo `tools/sass-MSX`

Un submódulo es un repo dentro de otro. `MSX-Game-Tools` no guarda los ficheros de
`sass-MSX`, sólo **un puntero a un commit** suyo y, en `.gitmodules`, de qué rama sale.

```bash
# Al clonar MSX-Game-Tools por primera vez:
git clone --recurse-submodules git@github.com:eslamod/MSX-Game-Tools.git
# o, si ya está clonado sin el submódulo:
git submodule update --init
```

### Cambiar algo dentro del submódulo

Se trabaja **dentro de `tools/sass-MSX`**, que es un repo con su propio `main`/`master`,
sus ramas y su `origin` (apunta a `Libertium/sass-MSX`):

```bash
cd tools/sass-MSX
git switch -c mi-cambio
# ... editar, commitear ...
git push -u origin mi-cambio
```

Abres el PR en `Libertium/sass-MSX` como cualquier otro. Cuando se fusione, vuelves al
repo padre a **actualizar el puntero**:

```bash
cd ../..                       # raíz de MSX-Game-Tools
cd tools/sass-MSX && git switch master && git pull && cd ../..
git add tools/sass-MSX         # esto graba el nuevo commit apuntado
git commit -m "Actualizar sass-MSX"
```

Ese `git add tools/sass-MSX` en el repo padre es lo que mueve el puntero. Sin él, el
padre sigue clavado en el commit viejo aunque el submódulo esté actualizado en disco.

---

## Situación actual (agosto 2026)

Trabajo hecho, pendiente de convertir en PRs:

| Rama | Repo | Qué lleva | Siguiente paso |
| --- | --- | --- | --- |
| `net10` | `sass-MSX` | Port del ensamblador a proyecto SDK-style .NET 10 | Ya empujada. PR `net10 → master` en GitHub. |
| `submodulo-sasSX` | `MSX-Game-Tools` | Traer `tools/sass-MSX` como submódulo (rama `net10`) | Empujar y abrir PR `→ main`. |
| `guia-git` | `MSX-Game-Tools` | Este fichero | Empujar y abrir PR `→ main`. |

URLs para abrir los PRs (tras empujar cada rama):

```
https://github.com/Libertium/sass-MSX/compare/master...net10?expand=1
https://github.com/eslamod/MSX-Game-Tools/compare/main...submodulo-sasSX?expand=1
https://github.com/eslamod/MSX-Game-Tools/compare/main...guia-git?expand=1
```

Cuando el PR de `net10` esté fusionado en `master`, conviene repuntar el submódulo de
`net10` a `master` (en otra rama + PR): editar `.gitmodules` (`branch = master`), hacer
`git -C tools/sass-MSX switch master && git -C tools/sass-MSX pull`, y
`git add .gitmodules tools/sass-MSX`.

---

## Chuleta

```bash
git switch main && git pull              # ponerse al día
git switch -c <asunto>                   # rama nueva
git switch <asunto>                      # cambiar de rama
git branch                               # ver ramas locales
git status                               # qué hay tocado
git add -A && git commit                 # commitear
git push -u origin <asunto>              # empujar rama nueva (la 1ª vez)
git push                                 # empujar más commits de una rama ya empujada
git log --oneline --graph --all -15      # ver el árbol de commits
git branch -d <asunto>                   # borrar rama local ya fusionada
git switch -c <x> && git branch -f main origin/main
#   ^ mover a una rama commits que se hicieron en main local sin querer,
#     y devolver main a donde está el remoto (sólo si main local no se empujó)
```

Reglas de la casa (ver `CLAUDE.md`): un commit por asunto con el porqué en el cuerpo; la
suite entera antes de cada commit; el historial ya empujado no se reescribe nunca; los
`git push` los haces tú.
