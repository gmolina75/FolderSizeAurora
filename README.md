# Folder Size Aurora

Analizador de espacio en disco para Windows, escrito en **.NET (WPF)** sin dependencias
de terceros. Sustituye a TreeSize Free en las funciones que se usan en la practica y añade
un **monitoreo de límites de tamaño** con avisos por varios canales.

> Estado verificado: compila con el **MSBuild de Visual Studio Community 2026 (18.10)** en las
> cuatro combinaciones Debug/Release × Any CPU/x64, con **0 errores y 0 advertencias**, tanto en
> el proyecto como descomprimiendo este ZIP en una carpeta limpia. El ejecutable autocontenido
> arranca y se mantiene en ejecución (probado en esta máquina).

---

## Qué hace

### Explorador de carpetas
- Escaneo recursivo de cualquier carpeta o unidad, con **progreso en vivo** y cancelación.
- Árbol con carpetas **y** archivos intercalados por tamaño, con columna de tamaño, porcentaje,
  barra de uso y contenido (`archivos / carpetas`).
- Panel de detalle con la ficha completa de lo seleccionado, y ruta completa como tooltip.
- Recorrido **iterativo con pila**, así que no se desborda la pila en árboles muy profundos.
- Salta junctions, symlinks y puntos de repetición, de modo que los ciclos no cuelgan el escaneo.
- Tolera carpetas sin permisos: las cuenta como "omitidas" y sigue.

### Unidades
- Lista de todas las unidades con espacio usado, libre y total, y barra de ocupación que
  cambia de color según el margen (azul normal, ámbar al llenarse, rojo al crítico).
- **Doble clic** en una unidad analiza la unidad completa desde la raíz.
- El carril se puede plegar a una tira de 44 px con solo los iconos (`Ctrl+B`).

### Filtros y orden (sin volver a escanear)
- **Tamaño mínimo**: sin mínimo, 1 KB, 100 KB, 1 MB, 10 MB, 100 MB, 1 GB.
- **Tipo de archivo**: todos, imágenes, vídeo, audio, documentos, comprimidos, código,
  imágenes de disco, ejecutables.
- **Orden**: tamaño, nombre, cantidad de archivos, última modificación (ascendente/descendente).
  También se ordena **pulsando la cabecera** de la columna.
- **Búsqueda** por nombre de archivo o carpeta en todo el árbol, agrupada a 220 ms para que
  teclear rápido no congele la ventana.
- Incluir/excluir archivos ocultos y de sistema.

Los filtros se aplican sobre clones del resultado original, así que el escaneo se conserva
intacto y se puede volver a la vista completa en cualquier momento.

### Gráficos
- Top 10 de carpetas más pesadas.
- Top 10 de archivos más pesados.
- Reparto por tipo de archivo.
- Barras proporcionales al total, con paleta coherente y tooltips con la ruta completa.

### Exportación
- **CSV** con BOM UTF-8 (se abre bien en Excel con acentos), una fila por carpeta y por archivo.
- **JSON** con el árbol completo y metadatos.
- **Informe de texto** con los 15 mayores y un resumen.

### Monitoreo de límites  ← la diferencia frente a TreeSize Free
- Reglas sobre **carpetas** (límite en bytes) y **unidades** (límite en porcentaje).
- Comprobación periódica configurable (cada minuto a cada día), con botón **Comprobar ahora**.
- Barra de uso por regla, en azul mientras está en rango y en rojo al superarse, con el estado
  escrito al lado para no depender solo del color.
- **Enfriamiento de 30 minutos**: una alerta repetida no vuelve a notificarse.
- Las reglas se guardan y se restauran al abrir la aplicación.

### Avisos por varios canales
| Canal | Qué hace |
|---|---|
| En la aplicación | Aviso flotante en la esquina + historial de alertas |
| Sonido | `SystemSounds.Hand` |
| Correo | SMTP con TLS opcional, remitente y destinatario configurables |
| Webhook | `POST` JSON a una URL (Slack, Teams, n8n, Home Assistant, script propio…) |
| Registro local | Siempre a `%LOCALAPPDATA%\FolderSizeAurora\alertas.log` |

Cada canal es independiente: si el correo falla, el resto se entrega igual, y la sección
**Entregas** de la pestaña Vigilancia muestra el resultado (`OK` / `Fallo`) de cada intento.
Hay botón de **notificación de prueba** para validar la configuración.

### Interfaz
Diseñada para leerse de un vistazo, no para impresionar:

- **Tema claro y oscuro** con conmutación instantánea (sin reiniciar), claro por defecto.
- **Listado plano** (`ListView` con columnas reales) en lugar de un árbol con cabecera imitada:
  las cabeceras no pueden desfasarse de las filas porque comparten las medidas de columna.
- La cabecera se ajusta sola al ancho real del área visible, así que las cifras caen siempre
  debajo de su columna aunque aparezca o desaparezca la barra de desplazamiento.
- **Iconos dibujados** en un único trazo de 1,3 px: ningún glifo Unicode haciendo de icono.
- Tipografía a dos voces: `Segoe UI` para leer, `Consolas` con cifras tabulares para todo número.
- Barras de proporción dentro de cada fila: dan la magnitud sin sumar una columna de números.
- Estados vacíos que explican **qué hacer**, no solo que no hay nada.
- Aviso flotante al superar un límite, descartable, que no tapa los datos.
- Deslizadores de perfil estrecho y discretos, incluidos los que WPF crea internamente.
- La ventana admite de 1024 px de ancho en adelante: el riel de filtros está dimensionado para
  caber en ese mínimo sin recortarse.
- **La ventana se coloca contra el área de trabajo real del monitor, no contra la pantalla.**
  Abre a 1320×800 si cabe; si no cabe —1366×768 con barra de tareas, o cualquier pantalla con
  escalado DPI por encima del 100 %— se recorta hasta el área útil y se centra dentro de ella.
  Los mínimos también bajan con ella, porque WPF no deja encoger una ventana por debajo de
  `MinWidth`: un mínimo de 1024 fijo volvería a empujar la ventana fuera de la pantalla.

Atajos: `Ctrl+O` elegir carpeta, `Ctrl+R` repetir análisis, `Ctrl+B` plegar el carril,
`Ctrl+D` cambiar de tema, `Ctrl+E` desplegar todo, `Ctrl+L` recoger todo.

### Menú contextual de la fila

Clic derecho sobre cualquier fila del árbol. Lo que no aplica a una carpeta se esconde, y los
separadores se ocultan con ellas para que no queden dos seguidos:

| Acción | Carpeta | Archivo | Qué hace |
|---|:--:|:--:|---|
| Abrir en el Explorador de archivos | ✅ | | Abre la carpeta en el explorador del sistema |
| Abrir el archivo | | ✅ | Lo abre con el programa asociado |
| Mostrar en el Explorador de archivos | | ✅ | Abre el explorador con el archivo seleccionado |
| Abrir en PowerShell | ✅ | ✅ | Consola en esa carpeta (PowerShell 7 si está, si no el 5) |
| Analizar solo esta carpeta | ✅ | | Vuelve a analizar desde ahí, sin el resto de la unidad |
| Vigilar esta carpeta | ✅ | | Crea la regla de límite en la pestaña Vigilancia |
| Copiar ruta | ✅ | ✅ | Al portapapeles, con reintentos si está ocupado |

Si algo falla al abrirse (por ejemplo, no hay PowerShell), el pie lo dice en vez de dejar la
aplicación en silencio.

---

## Por qué no se cuelga

«Se queda freezeada» casi nunca significa que falte un `await`: significa que **el hilo de la
interfaz está ocupado**. Con el árbol vacío todo iba bien; al analizar algo grande, la ventana
dejaba de repintar. Había tres trabajos sin límite, y los tres crecían con el número de archivos:

- **El recorrido del disco** se hacía en el hilo de la interfaz. Ahora el escaneo entero corre
  fuera y el progreso sigue llegando a la ventana.
- **Filtrar y ordenar** clona y ordena cientos de miles de nodos. Eso también corre fuera; a la
  interfaz solo se le publica el resultado ya construido. Un contador de generations descarta el
  resultado si mientras se calculaba cambió un filtro, así que escribir rápido no cruza vistas.
- **El gráfico de archivos mayores** ordenaba *todos* los archivos de la unidad para quedarse con
  diez. Ahora solo conserva los diez mejores. Y ordenar por «Última modificación» era **cuadrático**;
  se resuelve en una sola pasada.
- **Desplegar una unidad** era el peor caso de todos, por dos razones que no se ven leyendo el
  XAML: la columna de modificación recalculaba `Files.Max(...)` **en cada repintado de cada fila**
  (recorrer todos los archivos de la carpeta, otra vez, por fila), y las filas se insertaban una a
  una, lanzando un aviso de colección por fila. Ahora la fecha se acumula durante el escaneo y el
  listado se rellena con un único aviso.
- Además, el listado dibuja como máximo **5 000 filas** y solo abre **40 ramas** de primer nivel al
  terminar un análisis. Si se supera el tope, el pie lo dice con cifras en lugar de recortar en
  silencio.

Medido con una sonda que cronometra cuánto tarda la interfaz en responder:

| Qué | Peor bloque antes | Peor bloque ahora |
|---|---|---|
| Escanear 26 571 archivos | 25 ms | 16 ms |
| Escanear 154 834 archivos | **234 ms** | **30 ms** |
| Desplegar todo un árbol | miles de filas, fila a fila | **29 ms** |

Lo importante es que **dejó de crecer con el tamaño del árbol**.

Comprobado además sobre la unidad real de trabajo: `Z:\` (293 GB, **926 909 archivos y 344 617
carpetas**) se analiza en ~27 s, ocupa 801 MB y **la ventana responde en todo momento**, con el
árbol desplegado.

---

## Capturas

Las once imágenes de esta sección están **renderizadas por el arnés** desde la ventana real,
no son maquetas (las dos del menú, como se indica, están capturadas del escritorio).

**Explorador, tema claro**

![Explorador](docs/capturas/01-explorador-claro.png)

**Explorador, tema oscuro**

![Explorador oscuro](docs/capturas/02-explorador-oscuro.png)

**Gráficos**

![Gráficos](docs/capturas/03-graficos.png)

**Vigilancia**

![Vigilancia](docs/capturas/04-vigilancia.png)

**Ancho mínimo de la ventana (1024 px): nada se sale ni se recorta**

![Ancho mínimo](docs/capturas/05-ancho-minimo.png)

**Área de trabajo típica de un portátil a 125 % (1280×720): la ventana se ajusta y se centra**

![Área de trabajo 1280×720](docs/capturas/06-area-de-trabajo-1280.png)

**Carril de unidades plegado**

![Carril plegado](docs/capturas/07-carril-plegado.png)

**Aviso flotante al superar un límite**

![Aviso](docs/capturas/08-aviso.png)

**Ventana de 900×600, por debajo del mínimo de comfort: la barra superior y el pie siguen en su sitio**

![Pantalla pequeña](docs/capturas/09-pantalla-pequena.png)

**Menú contextual sobre una carpeta**

![Menú contextual de carpeta](docs/capturas/10-menu-contextual-carpeta.png)

**El mismo menú sobre un archivo: solo las acciones que aplican**

![Menú contextual de archivo](docs/capturas/11-menu-contextual-archivo.png)

> Las dos últimas son capturas de la aplicación real en su escritorio, no del arnés: un menú
> contextual vive en una ventana emergente y el renderizador del arnés no la alcanza.

---

## Cómo abrirlo y compilarlo en Visual Studio

1. Descomprime el ZIP (por ejemplo en `C:\dev\FolderSizeAurora`).
2. Doble clic en **`FolderSizeAurora.sln`**. Se abre con Visual Studio.
3. Menú **Build → Restore NuGet Packages** (o `Ctrl+Shift+Q`).
4. Menú **Build → Build Solution** (`Ctrl+Shift+B`).
5. Menú **Debug → Start Debugging** (`F5`) para ejecutar.

Requisitos: **Visual Studio 2026 (18.x)** con la carga de trabajo **".NET desktop development"**
(SDK .NET 10). No hay ningún paquete NuGet externo que instalar.

Desde consola, en la carpeta del proyecto:

```powershell
cd .\TreeSizeClone
dotnet restore
dotnet build
dotnet run
```

Y para generar el ejecutable:

```powershell
# Carpeta normal (necesita el runtime .NET 8 Desktop instalado)
dotnet publish .\TreeSizeClone\TreeSizeClone.csproj -c Release -o .\dist

# Autocontenido: NO necesita instalar nada, se lleva el runtime dentro (≈145 MB)
dotnet publish .\TreeSizeClone\TreeSizeClone.csproj -c Release -r win-x64 --self-contained true -o .\dist-standalone
```

> Ya hay una publicación autocontenida en la carpeta `dist/` de este proyecto.

**Si te aparece `NETSDK1004` ("Assets file not found")**: es que falta el restore. Ejecuta
`dotnet restore` una vez, o en Visual Studio usa **Build → Restore NuGet Packages**.

**Si te aparece que falta el tipo de proyecto**: asegúrate de tener instalada la carga
".NET desktop development"; es la que aporta el soporte de WPF.

### Nota sobre versiones de .NET

El proyecto apunta a **`net10.0-windows`**, que es lo natural para **Visual Studio 2026**
(18.x, con el SDK .NET 10). Si algún día necesitas abrirlo en Visual Studio 2022, cambia una
sola línea en `TreeSizeClone\TreeSizeClone.csproj`:

```xml
<TargetFramework>net8.0-windows</TargetFramework>
```

Ambos targeting packs vienen instalados, así que el cambio es inmediato y sin descargas.

### Nota sobre la solución

La solución se genera con `dotnet new sln --format sln` y **no debe editarse a mano**: el SDK 10
crea por defecto el formato nuevo `.slnx`, que Visual Studio 2022 no abre. Si en la carpeta
apareciera un archivo `.slnx` en lugar de `.sln`, es que se regeneró con el valor por defecto.

---

## Estructura

```
TreeSizeClone/
├─ App.xaml / App.xaml.cs          Application, convertidores y manejo de excepciones
├─ MainWindow.xaml(.cs)            Ventana principal: panel lateral + 3 pestañas
├─ Models/
│  ├─ FileEntry.cs                 Archivo (nombre, tamaño, extensión, fecha)
│  ├─ FolderNode.cs                Carpeta con totales, porcentajes e items unificados
│  ├─ ScanResult.cs                Resultado del escaneo + ChartItem + ScanProgress
│  ├─ DriveItem.cs                 Unidad (usado/libre/total, porcentaje)
│  ├─ MonitorTarget.cs             Regla de límite y alerta
│  └─ AppSettings.cs               Ajustes persistidos + enums de filtro y orden
├─ Services/
│  ├─ DirectoryScanner.cs          Recorrido iterativo, progreso, cancelación
│  ├─ TreeTransformer.cs           Filtro + búsqueda + orden sobre clones
│  ├─ DriveService.cs              Enumeración de unidades y espacio
│  ├─ SizeMonitorService.cs        Temporizador de límites con enfriamiento
│  ├─ NotificationService.cs       In-app, sonido, SMTP, webhook, log
│  ├─ ExportService.cs             CSV / JSON / informe
│  ├─ SettingsService.cs           JSON en %APPDATA%
│  ├─ ThemeService.cs              Cambio de tema en caliente
│  └─ Formatter.cs                 Bytes, conteos, duraciones
├─ ViewModels/
│  ├─ ObservableObject.cs          Base INPC
│  ├─ RelayCommand.cs              Comando síncrono y asíncrono
│  └─ MainViewModel.cs             Coordinación de toda la aplicación
├─ Helpers/Converters.cs           Bool→Visibility, fracción→ancho, paleta, estado
└─ Themes/
   ├─ Light.xaml / Dark.xaml       Paletas
   └─ Controls.xaml                Estilos de todos los controles
```

Ajustes: `%APPDATA%\FolderSizeAurora\settings.json`
Registro de alertas: `%LOCALAPPDATA%\FolderSizeAurora\alertas.log`

---

## Verificación de la interfaz (arnés)

`tools\Program.cs` carga la ventana real de la aplicación, ejecuta un análisis de verdad,
recorre las tres pestañas, alterna el tema y **renderiza nueve capturas** con
`RenderTargetBitmap`. Además:

- cuenta los errores de binding de WPF y separa los transitorios (los que ocurren antes de que
  exista el `DataContext`) de los reales;
- mide en píxeles el **desfase entre la cabecera y las filas** del listado;
- cuenta cuántas veces se reconstruye el árbol para detectar filtros realimentados en bucle;
- comprueba la **colocación de la ventana** en siete áreas de trabajo simuladas (pantallas
  pequeñas, escalado al 150 %, monitores secundarios en negativo) y mide el desborde real
  de la ventana viva contra el área de trabajo de este equipo;
- mide el **peor bloqueo del hilo de la interfaz** durante dos escaneos reales, que es lo que
  separa una ventana ágil de una que parece colgada.

Sirve para detectar fallos que **compilan bien pero revientan al abrir la ventana**, que es
justo lo que un `dotnet build` nunca enseña.

Estado de la última pasada: **0 errores de binding**, desfase de cabecera **0 px** a 1320, a
1280, a 1024 y a 900 px, **desborde de ventana 0 px** en los siete escenarios simulados y en la
ventana real, **peor bloqueo de interfaz 28 ms** en el escaneo de 154 834 archivos, y
reconstrucciones estables en reposo.

Para usarlo, copia el archivo a una carpeta aparte y crea un proyecto de consola WPF que lo
referencie:

```xml
<!-- <carpeta>\UiCheck.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <AssemblyName>UiCheck</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\TreeSizeClone\TreeSizeClone.csproj" />
  </ItemGroup>
  <!-- App.xaml usa rutas pack absolutas, asi que el arnés necesita los XAML de tema -->
  <ItemGroup>
    <Page Include="..\..\TreeSizeClone\Themes\*.xaml" Link="Themes\%(Filename)%(Extension)" />
  </ItemGroup>
</Project>
```

```powershell
dotnet run -c Debug -p:OutDir=C:\temp\fsa-out\
# Debe terminar en 0 con "sin errores de binding".
```

Tiene que ejecutarse en **Debug**: WPF solo emite los avisos de binding en compilaciones de
depuración. Las capturas se dejan en `%LOCALAPPDATA%\fsa-shots`.

> El arnés restaura los ajustes del usuario al terminar, así que no deja la configuración
> de pruebas en `%APPDATA%`.

---

## Limitaciones conocidas

- La contraseña SMTP se guarda en **texto plano** en el JSON de ajustes. Si eso no es
  aceptable en tu entorno, conviene cifrarla o usar las credenciales de la aplicación de Windows.
- El escaneo se ejecuta en un único hilo con enumeración asíncrona: es rápido para el uso
  diario, pero en volúmenes de millones de archivos se nota; se prioriza no bloquear la
  interfaz.
- No hay vista de mapa de calor (heatmap) ni comparación entre dos escaneos.
- Los datos de unidades son los que informa Windows en ese momento; las carpetas montadas en
  red pueden tardar en refrescar.

---

## Nota sobre el análisis inverso (REA)

El ejecutable original, `TreeSizeFree.exe`, se analysó con REA:

- `inspect-managed-artifact` → **no es una aplicación gestionada**: PE32+ nativo x64,
  60.945.496 bytes, SHA-256 `d72bfe9b…ecf31`, **sin directorio CLI**, sin metadatos .NET.
  Evidencia `ev_7a639814e774236731303f56b47a837e1d8e59a9095cd5e0fe91c94b5ba112cc`.
- `inspect-pe-resources` → 337 recursos: íconos (`MAINICON`, `TSP_ICON`), cursores
  (`CURSOR_HAND`, `RZCOMMON_HANDCURSOR`), diálogos y cadenas.
  Evidencia `ev_4be43139788d5615a64ddda3142d64d52147954d31eb768e34ec69a857d82f52`.

Es decir: TreeSize Free es C/C++ nativo, no .NET. Para reconstruirlo harían falta Ghidra con
JDK 21+, IDA o Hopper, que no están configurados en esta máquina. Por eso esta aplicación se
implementó desde cero en .NET replicating su comportamiento observable, en lugar de copiar
código.