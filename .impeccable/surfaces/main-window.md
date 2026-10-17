# Surface: MainWindow (Folder Size Aurora)

## Direction contract

**THESIS.** Un disco es un sondeo: se baja por un pozo y cada estrato es una carpeta cuyo
grosor es su tamaño. La interfaz rechaza el árbol flotante de "tarjetas sobre fondo" y adopta
el papel milimetrado del registro de sondeo: reglas de 1 px, columnas tabulares alineadas y
una rampa de magnitud que hace el trabajo que la tipografía se niega a hacer.

**OWN-WORLD.** Papel casi blanco (#F7F8FA) con paneles blancos y reglas de pelo #E3E7ED.
Tinta #101418, nunca negro puro. Un solo acento azul #1F6FEB para selección y acción
principal. Cifras siempre tabulares en Consolas; etiquetas en versalitas monoespaciadas de
10,5 px con tracking 0,08 em. La paleta categórica de 6 tonos solo aparece en gráficos, nunca
en chrome. Estado en verde/ámbar/rojo y en ningún otro sitio.

**STORY.** La persona abre la app porque un disco se llenó. En menos de dos segundos debe ver
cuánto ocupa cada cosa, cuánto pesa eso respecto al total, y si alguna ruta ya pasó su límite.
Las alertas se acumulan en un historial y se marcan con un anillo, no con un modal.

**FIRST VIEWPORT.** Barra superior de 48 px con marca, ruta y acciones primarias a la derecha.
Debajo, un carril izquierdo de 288 px (plegable a 44 px) con Unidades, Filtros y Monitoreo. A
la derecha, barra de pestañas de 32 px y debajo el listado de exploración con columnas reales
y ordenables. Tira de estado de 28 px al pie con el resumen del escaneo.

**FORM.** Dirección asignada 14441210, candidata 7 de la lista propia. La lista se compone en
`concept-seed --scope direction --mode operate`.

**RAISES.**

- *Del metro nocturno (challenger winners, ambos ejes):* nodos de intercambio — cada fila
  abierta lleva un anillo y un borde de acento en el margen izquierdo, y la línea de la
  columna "Uso" se dibuja como una línea de ruta continua que separa ramas hermanas.
- *Del atlas estelar (challenger competitivo, producto):* la magnitud carga la jerarquía —
  la barra de uso gana un peldaño mínimo perceptible para que un 0,4 % siga siendo visible,
  y el grosor del trazo se refuerza con el porcentaje numérico al lado.
- *Del catálogo Saville (challenger competitivo, audiencia):* el estado se codifica, no se
  etiqueta dos veces — las reglas en rango llevan una barra verde fina, las que se superan un
  anillo rojo y el texto `LÍMITE SUPERADO`, y nada más.
- *De la lámina de anual de diseño (declinado):* crucetas de registro — una retícula de 1 px
  separa las columnas en todo el listado, con una marca discreta en la esquina del carril
  izquierdo que ancla la lectura.

**FINISH.** unreviewed and undocumented is unfinished; this build ends with the finish review,
the verdict, DESIGN.md, and every shipping raster carrying its provenance.