# Product

<!-- impeccable:product-schema 1 -->

## Platform

windows-desktop

> Nota: la enumeración del esquema es `web | ios | android | adaptive`. Este producto es una
> aplicación de escritorio nativa de Windows (WPF), que no encaja en esa lista. Se registra la
> plataforma real en lugar de forzar un valor falso.

## Stack

WPF sobre .NET (`net10.0-windows`), sin dependencias NuGet externas. Decidido y confirmado por
el usuario: el proyecto debe abrir y compilar en Visual Studio 2026 con MSBuild, sin instalar
runtime aparte. La ausencia de paquetes externos es una restricción, no una preferencia.

## Users

Usuario técnico de Windows (perfil: desarrollador o técnico de sistemas) que abre la aplicación
cuando una máquina se queda sin espacio y necesita saber qué se lo está comiendo, o cuando
sigue un disco que se acerca a su límite. Trabaja en su escritorio, con varias ventanas
abiertas y con prisa.

## Product Purpose

Hacer visible y accionable dónde se va el espacio de un disco. Dos trabajos: **diagnóstico**
(encontrar qué carpeta o archivo ocupa) y **prevención** (recibir un aviso antes de que una
carpeta o una unidad rebase su límite). El éxito es llegar a la respuesta en segundos, sin
tener que esperar a un árbol de varios millones de entradas.

## Positioning

Los analizadores de disco gratuitos (*TreeSize Free*, *Everything*) solo **informan**: exigen
estar delante de la pantalla para mirar el resultado. Folder Size Aurora **vigila**: define
límites por carpeta o unidad y avisa por varios canales cuando se superan, con enfriamiento
para no repetir la misma alerta. Esa combinación de árbol denso + límites persistentes es lo
que un producto vecino no puede copiar sin añadir esa función.

## Operating Context

- Uso en escritorio Windows, a menudo en segundo plano mientras se trabaja en otra cosa.
- Se lee en pantalla mucho rato: columnas de números, barras y porcentajes.
- Idiomas de la interfaz: español.
- La persona usuaria abre la app con Visual Studio 2026 para tocarla, no solo para ejecutarla.
- La app debe sobrevivir a carpetas sin permisos, junctions y rutas muy largas.

## Capabilities and Constraints

Confirmado:

- Escaneo recursivo con progreso y cancelación; salto de junctions y symlinks.
- Árbol de carpetas y archivos con tamaño, porcentaje y conteos.
- Filtros (tamaño mínimo, tipo de archivo), búsqueda por nombre y cuatro criterios de orden,
  aplicados sin volver a escanear.
- Exportación a CSV, JSON e informe de texto.
- Monitoreo de límites por carpeta (bytes) y unidad (porcentaje), con intervalo configurable.
- Avisos por cinco canales: en la aplicación, sonido del sistema, correo SMTP, webhook HTTP y
  registro local. Resultado de entrega por canal visible en la interfaz.
- Tema claro y oscuro con conmutación en caliente.

Restricciones y hechos abiertos:

- La contraseña SMTP se guarda en texto plano en `settings.json`. Aceptado con|label| con
  advertencia visible en la propia interfaz. Cifrado: decisión pendiente del usuario.
- El escaneo es de un solo hilo con enumeración asíncrona: prioriza no bloquear la
  interfaz sobre el paralelismo.
- Sin vista de mapa de calor ni comparación entre escaneos.
- Sin datos de telemetría, sin cuentas, sin red salvo el webhook y el SMTP que el usuario
  configure.

## Brand Commitments

- Nombre: **Folder Size Aurora**. established por el usuario.
- Idioma de la interfaz: español.
- Sin marcas ajenas: no se usan logotipos, colores ni activos de TreeSize, JAM Software ni
  ningún otro producto de referencia.

## Evidence on Hand

- Análisis estático del binario de referencia `TreeSizeFree.exe` con REA: PE32+ nativo x64,
  SHA-256 `d72bfe9b…ecf31`, sin directorio CLI, 337 recursos PE. Confirma que el original es
  C/C++ nativo y que no es una base de código .NET reutilizable.
- No hay testimonios, clientes, métricas de uso ni benchmarks del producto. Nada de esto debe
  inventarse en la interfaz.

## Product Principles

1. **El número es la interfaz.** Cada fila debe poder leerse de un vistazo: nombre, tamaño,
   porcentaje, barras. La decoración que estorbe a la lectura se elimina.
2. **Densidad antes que aire.** Mucha información por pantalla; el usuario viene a comparar
   magnitudes, no aAdmiring el diseño.
3. **Ningún dato sin contexto.** Un tamaño sin su porcentaje respecto al total no sirve.
4. **Avisar sin martillear.** Enfriamiento, historial y resultado por canal: una alerta
   confiable vale más que diez alertas ignoradas.
5. **Fallar donde se ve.** Errores de permisos, rutas inaccesibles y fallos de envío se
   muestran en la interfaz, no se esconden en un log.

## Accessibility & Inclusion

Sin requisitos de accesibilidad específicos acordados con el usuario. Pendiente de
confirmar: densidad alta puede chocar con texto pequeño, así que el tamaño de fuente base y
el contraste deben verificarse en la interfaz final.