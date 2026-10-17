# Sistema de diseño — Folder Size Aurora

Modo de la superficie: **operate**. Aquí la visitoria completa una tarea: medir, comparar y
vigilar espacio en disco. La escaneabilidad, la consistencia y la expectativa nativa mandan
sobre la expresión; la personalidad vive en los detalles precisos, no en la decoración.

---

## 1. Mundo visual: «registro de sondeo»

La interfaz es una **hoja de registro sobre papel milimetrado**. Un instrumento técnico que
anota mediciones: papel casi blanco, tinta que nunca llega al negro puro, un solo color de
tinta azul para lo que está activo, reglas de un píxel como pauta y las cifras siempre en
tipografía de datos.

De ahí salen las cuatro decisiones que gobiernan todo lo demás:

| Referencia | Cómo se traduce |
|---|---|
| Metro nocturno: anillos de nodo | La marca de 2 px a la izquierda de la fila seleccionada y el chevrón de despliegue |
| Atlas estelar: magnitud = jerarquía | La barra de proporción dentro de cada fila: el tamaño se lee de un vistazo sin sumar columnas |
| Catálogo Saville: el estado va codificado | Azul normal, ámbar al llenarse, rojo al crítico; `OK` / `Fallo` escrito además del color |
| Lámina de anual: crucetas de registro | Cabeceras de columna monoespaciadas en versalitas, con flecha de orden activa |

---

## 2. Color

Definido una sola vez por tema, en `Themes/Light.xaml` y `Themes/Dark.xaml`. Ninguna pantalla
declara un color literal: todo pasa por `{DynamicResource}` para que el cambio de tema sea
instantáneo.

### Claro (por defecto)

| Rol | Valor | Uso |
|---|---|---|
| `Bg.Ground` | `#F7F8FA` | Fondo de la ventana |
| `Bg.Panel` | `#FFFFFF` | Barra superior, carril, pie |
| `Bg.Sunken` | `#F1F3F6` | Cabeceras de columna, campos de solo lectura |
| `Bg.Hover` / `Bg.Selected` | `#EFF4FB` / `#E3EDFD` | Estados de fila |
| `Rule.Hair` / `Rule.Strong` | `#E3E7ED` / `#CBD3DE` | Separadores de 1 px y bordes de control |
| `Ink` / `Ink2` / `Ink3` | `#101418` / `#5B6672` / `#8A94A0` | Tres niveles de tinta, **nunca negro puro** |
| `Accent` | `#1F6FEB` | Único acento: selección, orden activo, barras |
| `Ok` / `Warn` / `Danger` | `#1F8B4C` / `#B26A00` / `#C0392B` | Estado |
| `Cat.0..Cat.5` | seis tonos | **Solo gráficos**. Jamás en chrome |

### Oscuro

Mismos roles con valores oscuros. `Ink` baja a `#E6EAF0`: sobre fondo oscuro, el blanco puro
hace vibrar los bordes.

### Reglas que no se rompen

- **Un solo acento.** El azul significa «esto está activo» y nada más.
- **El rojo se reserva.** Aviso a 95 % o límite superado. Una unidad al 90 % está en ámbar,
  no en rojo: el rojo deja de alarmar cuando significa «lleno» y solo cuando significa «roto».
- **El estado nunca depende solo del color.** Junto al color va siempre una palabra
  (`Pendiente`, `OK`, `Fallo`) o una cifra, para que la interfaz se lea igual sin percepción
  del color.
- **La paleta categórica no sale de los gráficos.** Un icono de una fila no se pinta de `Cat.3`.

---

## 3. Tipografía

Dos voces y ninguna más.

- **`Segoe UI`** para todo lo que se lee como prosa: nombres, etiquetas, botones.
- **`Consolas`** para **toda cifra**, con cifras tabulares para que las columnas encajen en
  vertical. Tamaños, conteos, porcentajes, fechas, puertos, horas de entrega.

| Nivel | Tamaño | Peso | Uso |
|---|---|---|---|
| Wordmark | 13,5 | SemiBold | «Folder Size Aurora» |
| Fila | 12,5 | — | Nombres del árbol y de las listas |
| Campo | 12,5 | — | Texto de entrada |
| Etiqueta | 11,5 | — | Texto secundario |
| Numérico | 11,5 mono | SemiBold en cifras de tamaño | Todas las cifras |
| Sección | 10 mono bold | Bold | Etiquetas de zona en versalitas |
| Micro | 10,5 mono | — | Rutas, pistas, resúmenes |

El `12,5` en lugar de `12` es deliberado: en una tabla densa de números, medio punto evita que
las cifras se apeluren sin resorting a una tipografía más grande.

---

## 4. Espaciado y forma

Escala de **4 px**, declarada como recursos (`M.Row`, `M.Rail`, `M.Top`, `M.Status`…) para que
la interfaz se lea como una sola pieza.

| Medida | Valor |
|---|---|
| Fila de lista | 26 px (25 px de contenido + 1 px de regla) |
| Riel de filtros | 28 px de control, 40 px de carril |
| Barra superior / pie | 48 px / 28 px |
| Riel de unidad | 44 px plegado, 288 px abierto |
| Sangría por nivel | 14 px |
| Grosor de marca de selección | 2 px |

Radios: **4 px** en controles, **6 px** en paneles, **10 px** solo en barras de proporción.
Nada de tarjetas redondeadas por todas partes: una superficie con borde de 1 px, sin sombra.

**Elevación se declara una sola vez.** Los paneles llevan borde de 1 px; los menús y el aviso
flotante llevan además una sombra con desplazamiento y desenfoque reales. Nunca borde y sombra
juntos.

---

## 5. Iconos

Todos dibujados como geometría en `Themes/Controls.xaml`, en una caja de **16×16**, un único
trazo de **1,3 px**, remates y uniones redondeados, sin relleno.

**No se usan glifos Unicode ni emojis como iconos.** El menú es `M2.5,4.5 L13.5,4.5 …`, no
`☰`. Es lo que separa una interfaz construida de una ensamblada, y evita el problema de las
fuentes que sustituyen el glifo por otra cosa.

---

## 6. Estructura de la ventana

```
┌──────────────────────────────────────────────────────────────┐
│ Barra superior 48 px                                          │
│  ≡ │ Folder Size Aurora │ ruta de solo lectura │ Analizar ⏎ ↻ ☀ │
├────────┬─────────────────────────────────────────────────────┤
│ Carril │ Riel de filtros 28 px                               │
│ 288 px │ ┌─────────────────────────────────────────────┐    │
│ (o 44) │ │ Cabecera de columnas (26 px, fondo hundido)  │    │
│        │ ├─────────────────────────────────────────────┤    │
│ UNIDA- │ │ Filas del árbol (26 px, virtualizadas)       │    │
│ DES    │ │  ⌄  foldersizeAurora      293,88 MB ▓▓▓▓ 4 43│  │
│ LECTU- │ │   ⌄  TreeSizeClone       149,16 MB ▓▓     6 38│  │
│ RA     │ │     ›  bin               146,04 MB ▓      0  8│  │
│        │ └─────────────────────────────────────────────┘    │
│        │ Detalle de la selección (ralla azul a la izquierda)  │
├────────┴─────────────────────────────────────────────────────┤
│ Pie 28 px: estado  ·············· atajos de teclado           │
└──────────────────────────────────────────────────────────────┘
```

Tres pestañas: **Explorador** (medir), **Gráficos** (comparar), **Vigilancia** (reaccionar).

### El menú contextual de la fila

Clic derecho en cualquier fila. Tres decisiones que no son obvias:

**Va enganchado al contenedor de la fila, no a la lista.** Un `ContextMenu` se hospeda en un
`Popup`, es decir, en otro árbol visual: desde dentro, `RelativeSource AncestorType=ListView`
no llega a ningún sitio. Por eso el menú se declara como recurso, se asigna al `ListViewItem` y
recibe el ViewModel explícitamente en `RowMenuOpened`. El botón derecho lo prepara antes
(`OnTreeRightButtonDown`) para que el menú no aparezca un instante con todos los elementos
visibles y luego se encoga; `Opened` queda como red de seguridad para abrirlo con el teclado.

**Cada elemento recupera la fila por `PlacementTarget`.** Con el menú compartido entre filas,
`PlacementTarget` es justamente la fila que se pinchó: de ahí sale el parámetro del comando y el
`IsFolder` que decide qué acciones se esconden.

**Los separadores se esconden con las acciones que enmarcan.** Si el separador entre
«Analizar solo esta carpeta» y «Copiar ruta» fuera siempre visible, en un archivo quedarían dos
separadores seguidos al final, que es un descuido que se ve.

### El listado: por qué no es un árbol

La versión anterior era un `TreeView` con una cabecera dibujada aparte por encima. Eso tiene
un fallo estructural: **la cabecera y las filas no comparten medidas**, así que cualquier
divergencia de scroll, padding o ancho los desalinea.

La solución es aplanar el árbol a una lista (`Rows`, un `TreeRow` por fila visible) y usar
`ListView` con un `Grid` de columnas propio. Cabecera y fila declaran **las mismas medidas de
columna**, así que no pueden desfasarse. A cambio se pierde el orden por clic automático, que
se recupera con un comando por cabecera, y se gana virtualización real con reciclado.

### El desfase que sí existía

Con la barra vertical visible el área del listado pierde ese ancho por la derecha y la
cabecera se quedaba corta. `SyncHeaderGutter` no supone cuánto mide la barra: **mide la
geometría real** de la cabecera y del visor con `TranslatePoint` y corrige el margen hasta que
coinciden. El arnés lo comprueba numéricamente: **0 px** a 1320, a 1024 y a 900 px de ancho.

---

## 7. Criterio por sección

| Elemento | Criterio |
|---|---|
| Fila del árbol | Marca de 2 px a la izquierda al seleccionar, cebra al pasar el ratón, regla de 1 px al pie |
| Chevrón | Gira por visibilidad, no por rotación; 16 px de hueco fijo para que la sangría no desplace el texto |
| Columnas de cifra | `Consolas`, alineadas a la derecha, mismo margen interior que su cabecera |
| Barra de proporción | 6 px de alto, esquinas de 3 px, mínimo de 2 px para que cualquier valor no nulo se vea |
| Cabecera de columna | Sin recuadro. Foco y hover se marcan con una raya inferior de 2 px |
| Estado vacío | Dice **qué hacer**: «Elige una unidad en el carril y haz doble clic, o pulsa Analizar carpeta» |
| Aviso flotante | Abajo a la derecha, 380 px, descartable, nunca tapa la zona de datos |
| Deslizadores | Perfil de 11 px en toda la aplicación, incluido el estilo implícito que WPF aplica dentro de sus plantillas |
| Formulario | Etiqueta a la izquierda en columna fija de 96 px, campo a la derecha; los grupos opcionales se ocultan enteros cuando el canal está desactivado |

---

## 8. Anchos

La ventana **quiere** abrir en 1320×800 con **mínimo de 1024×560**. Quieres, porque ninguna de
las dos medidas es un derecho: son el techo y el suelo de lo que se ve cómodo.

Contra el área de trabajo real decide `ScreenGeometry`. La ventana no se centra en la pantalla,
se ajusta a la **zona útil del monitor** que la aloja, leída con `GetMonitorInfo` y convertida a
DIP con el escalado del propio monitor, no con el 96 del sistema. La regla es una sola:

> la ventana no puede medir más que el área de trabajo, ni empezar antes de su esquina superior
> izquierda.

De esa regla sale el resto. Si no cabe, se recorta hasta el área útil y se centra dentro de ella.
Los **mínimos bajan con ella**, porque WPF no deja encoger una ventana por debajo de `MinWidth`:
un `MinWidth="1024"` fijo, sobre un área de trabajo de 1280×720 DIP, produce exactamente el defecto
que se reportó — la barra superior de 48 px y el pie de 28 px fuera de la pantalla y el carril
cortado por la izquierda. El ajuste ocurre en `OnSourceInitialized`, con el HWND ya creado pero
antes del primer dibujado, así que no hay destello fuera de pantalla.

El mínimo tampoco es un número arbitrario: el riel de filtros está dimensionado para caber en los
703 px que quedan con el carril abierto. La exportación son **tres formatos en un botón con
menú emergente**, no tres botones, porque tres botones no cabían. Ningún control del riel tiene
ancho fijo que pueda empujar el resto fuera de la ventana.

---

## 9. Rendimiento: la ventana nunca se cuelga

«Se queda freezeada» es el aviso más grave que puede dar una interfaz, y aquí no significaba
que faltara un `await`. Significaba que **el hilo de la interfaz estaba ocupado**. Con el árbol
vacío la app iba bien; en cuanto se analizaba algo grande, la ventana dejaba de repintar.

Había tres trabajos sin límite, y los tres crecían con el número de archivos:

1. **El recorrido del disco.** `DirectoryScanner.ScanAsync` es `async` pero no tenía `Task.Run`:
   se recorría el sistema de archivos en el hilo de la interfaz. Ahora el escaneo entero corre
   fuera, y el progreso sigue llegando a la interfaz porque `Progress<T>` se crea en ella y encola
   en el dispatcher.

2. **El clonado del árbol para la vista.** Filtrar y ordenar es cálculo puro sobre cientos de
   miles de nodos. `RebuildView` lo calcula con `Task.Run` y publica en la interfaz un resultado
   ya construido. Un contador de generación descarta el resultado si mientras se calculaba cambió
   un filtro o empezó otro escaneo, así que escribir rápido en el filtro no produce vistas
   cruzadas.

3. **El gráfico de archivos mayores.** Ordenaba *todos* los archivos de la unidad para quedarse
   con diez. Ahora mantiene solo los diez mejores; cada archivo nuevo solo displace al peor si de
   verdad es mayor. Y el clasificador por fecha, que era **cuadrático** —cada carpeta comparaba su
   fecha contra todo su subárbol una y otra vez—, se resuelve en una sola pasada de abajo arriba.

**Desplegar una unidad** tenía además dos trampas que no se ven leyendo el XAML:

- La columna de modificación era `Folder.Files.Max(f => f.LastWriteTime)` dentro de una propiedad
  calculada. WPF la relee **en cada repintado de cada fila**: eso es recorrer todos los archivos de
  la carpeta, otra vez, por cada fila visible. La fecha se acumula ahora en `FolderNode` mientras
  el escaneo recorre los archivos, que es donde ya estaba.
- Las filas se insertaban una a una en un `ObservableCollection`. Cada `Add` lanza un aviso y WPF
  reacciona a todos. `ObservableRangeCollection.ReplaceAll` cambia el contenido entero con **un
  único `Reset`**, que es justo lo que la virtualización sabe absorber.

Además hay dos topes: **5 000 filas** dibujadas y **40 ramas** de primer nivel abiertas al terminar
un análisis. El listado virtualiza, así que el tope de filas no es por memoria: es porque construir
y medir cientos de miles de filas en la interfaz sí congela. Cuando se alcanza, el pie lo dice con
números en vez de recortar en silencio.

La medida se hace con una sonda en otro hilo que pide una ejecución inmediata al dispatcher y
cronometra la espera: ese es el tiempo que la ventana lleva sin responder.

| Qué | Peor bloque antes | Peor bloque ahora |
|---|---|---|
| Escanear `C:\Windows\System32` (26 571 archivos) | 25 ms | 16 ms |
| Escanear `C:\Program Files` (154 834 archivos) | **234 ms** | **30 ms** |
| Desplegar todo un árbol | fila a fila, sin tope | **29 ms** |

Lo importante no es el número final: es que **dejó de crecer con el tamaño del árbol**. Comprobado
además sobre `Z:\` real (293 GB, 926 909 archivos, 344 617 carpetas): se analiza en unos 27 s,
ocupa 801 MB y la ventana responde en todo momento.

---

## 10. Movimiento

Prácticamente ninguno, y a propósito. Es un instrumento de medición: las animaciones estorban
al leer cifras. Lo único animado es la barra de progreso durante el escaneo, porque informa de
que algo está avanzando.

Donde sí hay transiciones son estados de control (hover, foco, apertura del menú de exportación),
y son cambios de color, no desplazamientos.

---

## 11. Accesibilidad

- Contraste: tinta `#101418` sobre `#FFFFFF` = **16,1:1**. `Ink2` sobre `Bg.Ground` = **7,3:1**.
  `Ink3` se reserva para texto no esencial y sigue por encima de 4,5:1.
- El estado nunca depende solo del color (§2).
- Foco visible en todos los controles interactivos, con marca propia por tipo de control.
- Todos los iconos tienen `ToolTip` y todos los botones con texto visible llevan `ToolTip` que
  explica la consequence, no repite la etiqueta.
- El tamaño de fila de 26 px y el objetivo de los botones de 28 px por encima del mínimo
  confortável de puntero.

---

## 12. Voz

Español de España, con **«tú»** y sin tuteo administrativo.

- Los controles nombran su acción: «Analizar carpeta», «Comprobar ahora», «Restablecer».
- Los errores nombran el problema y la salida: «No se pudo abrir la ruta», «Se ha superado el
  límite de 100 MB».
- Los estados vacíos son una instrucción, no una disculpa.
- Sin anglicismos innecesarios: «barra de proporción» no «usage bar», «carril» no «rail».

---

## 13. Verificación

El diseño no se da por bueno por compilar. `tools\Program.cs` renderiza la ventana real con
`RenderTargetBitmap` en nueve estados —claro, oscuro, las tres pestañas, el ancho mínimo, el área
de trabajo típica de un portátil, el carril plegado, el aviso y una ventana por debajo del mínimo
de comfort— y además:

- cuenta los errores de binding separando los transitorios de los reales;
- mide el desfase cabecera/filas en píxeles;
- cuenta las reconstrucciones del árbol para detectar realimentación de filtros;
- comprueba la colocación de la ventana en siete áreas de trabajo simuladas y mide el desborde
  de la ventana viva contra el área de trabajo del equipo donde se ejecuta;
- mide el peor bloqueo del hilo de la interfaz durante dos escaneos reales de 26 571 y
  154 834 archivos, y durante el despliegue completo de un árbol (§9).

Última pasada: **0 errores de binding**, **0 px** de desfase a 1320, a 1280, a 1024 y a 900, **0 px**
de desborde de ventana en los siete escenarios y en la ventana real, reconstrucciones estables en
reposo, y **peor bloqueo de interfaz de 30 ms** en el escaneo grande y de 29 ms al desplegar. Las
imágenes están en `docs/capturas/`.