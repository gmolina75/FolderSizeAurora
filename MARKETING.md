# Folder Size Aurora — descripción comercial

Textos listos para usar: página de producto, redes, presentación o ficha. Todos son
**verificables**: las cifras salen de medir la aplicación real, no de escribir y prometer.

---

## 1. Claim principal (una línea)

> **Sabe dónde se fue tu espacio. Y avisa antes de quedarte sin él.**

---

## 2. Bajada larga (para la portada o una ficha de producto)

Tu disco lleno no es un misterio: es una carpeta que creció sin que nadie la mirara.
**Folder Size Aurora** te dice exactamente qué ocupa qué —carpeta a carpeta, archivo a
archivo— y hace algo que los medidores de espacio no hacen: **vigila tus límites**.

Ponle un tope a una carpeta o a una unidad entera y, cuando se pase, te avisa. Por correo,
por webhook, con un sonido o dentro de la propia aplicación. Así el problema deja de
descubrirse cuando ya es un problema y pasa apreventirse.

Todo en un ejecutable de escritorio para Windows. Sin cuenta, sin nube, sin suscripción y sin
enviar un solo byte de tus archivos a ninguna parte.

---

## 3. Descripción larga (para una tienda o un README)

**Folder Size Aurora** es un analizador de tamaño de carpetas y unidades para Windows con una
peña que los demás no tienen: vigila.

- **Explorador.** Un árbol con carpetas *y* archivos intercalados por tamaño, la barra de
  proporción en cada fila y el porcentaje de uso. Filtra por tamaño, por tipo de archivo o por
  nombre sin volver a escanear.
- **Gráficos.** Las diez carpetas más pesadas, los diez archivos más pesados y el reparto por
  tipo.
- **Vigilancia.** Reglas de límite por carpeta y por unidad, con avisos por correo, webhook,
  sonido o dentro de la app, y un enfriamiento de 30 minutos para no recibir la misma alerta
  cada cinco minutos.
- **Menú contextual.** Clic derecho en cualquier fila: abrir en el Explorador de Windows,
  abrir una terminal ahí, reanalizar solo esa carpeta, vigilarla o copiar su ruta.
- **Exportación** a CSV, JSON e informe de texto.

**No se cuelga.** La mayoría de estos programas se bloquean con las unidades grandes, porque
recorren el disco desde la ventana. Folder Size Aurora hace ese trabajo fuera del hilo de la
interfaz: sobre una unidad real de 293 GB con **926.909 archivos** y 344.617 carpetas, tarda
unos 27 segundos y **la ventana responde en todo momento**. Puedes seguir usando el ordenador
mientras trabaja.

Escrito en C# con WPF sobre .NET 10, sin una sola dependencia externa. Se abre con Visual
Studio y se compila en un minuto.

---

## 4. Micro-descripciones

| Uso | Texto |
|---|---|
| Etiqueta / badge | Sabe dónde se fue tu espacio |
| Texto de un tweet o post | Mide tus carpetas y vigila tus límites. 293 GB en 27 s sin colgarse. |
| Subtítulo de un vídeo | Por qué todos los analizadores de disco se cuelgan, y qué hacer para que no |
| Descriptor de fichero | Folder Size Aurora: mide carpetas y unidades, y avisa antes de quedarte sin espacio. Windows, .NET 10, sin dependencias. |
| Texto alternativo del logo | Folder Size Aurora |

---

## 5. Los tres argumentos que de verdad diferencian

1. **Avisa antes, no después.** No mide y ya: vigila límites y avisa por cuatro canales.
2. **No se congela.** Es la objeción número uno a esta categoría de programa, y está resuelta
   y medida, no prometida.
3. **Se queda en tu equipo.** Sin cuenta, sin nube, sin telemetría. Los ajustes son un archivo
   de texto en tu perfil de Windows.

---

## 6. Cifras que se pueden citar

Todas medidas sobre esta máquina, no estimadas:

| Cifra | Cómo se obtuvo |
|---|---|
| 926.909 archivos y 344.617 carpetas | Análisis de una unidad real de 293 GB |
| ~27 s de duración total | El mismo análisis, de principio a fin |
| 30 ms de peor bloqueo de la interfaz | Sonda que cronometra cuánto tarda la ventana en responder |
| 16 ms con 26.571 archivos | Ídem, árbol pequeño |
| 234 ms → 30 ms con 154.834 archivos | Antes y después de sacar el escaneo del hilo de la interfaz |
| 0 dependencias externas | El proyecto no tiene ningún `PackageReference` |

> Si en algún momento se cambian estas cifras por otras, conviene volver a medirlas. Se miden
> en un minuto y es lo que sostiene el argumento.

---

## 7. Aviso sobre lo que **no** dice esta descripción

A propósito, estas frases **no** aparecen en ningún texto del producto:

- Ningún número de usuarios, de descargas o de estrellas.
- Ningún testimonio ni logo de cliente.
- Ninguna promesa de "gratis para siempre" o de licencia concreta.

No porque el producto no lo valga, sino porque esas cifras no están medidas y una descripción
que promete lo que nadie ha verificado es una deuda: el día que un cliente pregunta, hay que
inventar. Cuando existan datos reales, se añaden aquí y a la landing.

---

## 8. Dónde está cada cosa

| Elemento | Ruta |
|---|---|
| Landing page | `landing/index.html` |
| Logo vectorial | `landing/assets/logo.svg` |
| Logo a 512 / 256 / 128 / 64 / 32 / 16 px | `landing/assets/logo-*.png` |
| Imagen para redes (1200×630) | `landing/assets/og.png` |
| Código fuente para la descarga | `landing/FolderSizeAurora-src.zip` |
| Capturas reales | `docs/capturas/` |