** TODO **

Este archivo recoge diferentes mejoras y correcciones que hay que realizar. Cada linea que comienza por un guión (-) es algo que hay que implementar, una vez que esté implementado hay que cambiar el guion al inicio por un simbolo mas (+) que indica que ya está implementado. Funciona como un checklist. Las lineas que empiezan por mayor que (>) se escribe a continuación de una implementación e incluse informacion ampliada y otras instrucciones para la implementación.

Una vez que hayas leído este archivo, muestra un listado rápido/simplificado de las tareas que vas a implementar.

Las Líneas que empiecen por # debes ignorarlas

IMPORTANTE: No te olvides de marcar en este archivo va inmplementación realizada com el símbolo +

No tienes que modificar el contenido, nada mas que para marcarlo como hecho. Lo que si quiero que hagas es añadir un descripcion de lo que has hecho, con detalles resumidos, bajo el punto que corresponda, comentarios que empiecen por #

Además quiero que cada vez que implementes una tareas o una característica, hagas un git commit , pero no hagas git push , excepto peticion expresa.

Antes de empezar designa un orden de las tareas o características, desde la mas sencilla a la mas complicada, para que las implementes en ese orden.

Antes de terminar la iteración vuelve a releer el archivo a ver si hay nuevas mejoras o correcciones e implementalas, segun los criterios indicados anteriormente.

Al final cuando termines y no haya ninguna nueva tarea apuntada en el ToDo.md, desglosame lo que has implementado por cada una de los siguientes : en el servicio, y en la UI APP

---

+ Quiero quitar la gestión de usuarios desde la propia APP, quiero llevarla a grupos de Windows.
> Todos los usuarios que sean administradores (SID 'S-1-5-32-544') tiene acceso tambien a administrar en Wireguard Manager, vamos lo que es el rol de admin
> Luego el servicio se debe de encargar de crear los grupos en Windows los grupos deben ser los siguientes:
> Wireguard UI - Administrator
> Wireguard UI - Visualizador
> Wireguard UI - Operador
> Wireguard UI - Operador avanzado
> Ademas añade las descripciones para cada uno.
> Una vez implementado puede quitar de la UI la interfaz actual.
# Implementado: WindowsGroupRoleStore (IRoleStore) y WindowsGroupProvisioner en el servicio. Los 4 grupos se crean al arrancar el servicio. Los administradores locales (SID S-1-5-32-544) obtienen rol Admin automáticamente. Tab Usuarios eliminado del AXAML y UserManagementViewModel removido de MainWindowViewModel.
+ No es necesario mostrar en la UI el botón connectar, ya que cuando no lo esté debe intentarlo una vez por segundo
# Implementado: eliminado el bloque "Not connected state" del AXAML. MainWindowViewModel ahora tiene un BackgroundLoopAsync que intenta conectar cada 1 segundo cuando no está conectado, y refresca túneles cada 5 segundos cuando sí lo está.
+ Tampoco quiero mostrar el estado Connected (con el boliche verde) o diconnected, para ello quiero que añadas una bottom toolbar, entre la que iremos poniendo diferentes "cosas" la primera de todas una casilla precisamente con eso, que represente si está conectado o desconectado del servicio, un boliche verde o rojo segun corresponda.
# Implementado: añadida bottom toolbar con Border DockPanel.Dock=Bottom de fondo #16213A. Muestra un punto verde (#4ADE80) o rojo (#F87171) según ServiceStatusColor y el texto ServiceStatusText. Eliminado el indicador del header.
+ En la UI tampoco veo necesario mostrar el "user" y el "role"
# Implementado: eliminado el bloque User info bar del header de MainWindow.axaml.
+ Para los tunnels si es posible cuando está conectado quiero ver la información como la que muestra el cliente nativo, que es la IP del tunnel, el endpint, el ultimo handshaje y los datos rx y tx
# Implementado: extendido TunnelInfo (Shared) con TunnelAddress, Endpoint, LastHandshake, RxBytes, TxBytes. En TunnelManager.ListTunnelsAsync se llama a WireGuardStats.GetStats() que ejecuta `wg.exe show <name>` y parsea la salida. TunnelViewModel expone HasDetails, TransferText y FormatBytes. En MainWindow.axaml cada tarjeta de túnal muestra un bloque de detalles (IsVisible=HasDetails) con una Grid de 4 filas.
+ Tambien quiero la posibilidad de poder editar un tunnel importado.
# Implementado: botón "Editar" en cada tarjeta de túnel (visible solo para AdvancedOperator). En MainWindowViewModel, EditTunnelCommand exporta la conf del túnel y llama a ImportTunnelViewModel.EnterEditMode() que pre-rellena el formulario en modo edición. El tab de Importar muestra "Editar" como título cuando está en modo edición.
+ Implementa tambien las notificaciones de Windows 11 para notificar cuando se conecta o desconecta un tunnel.
# Implementado: INotificationService + WindowsNotificationService usando Microsoft.Toolkit.Uwp.Notifications 7.1.3. Las notificaciones se disparan en SyncTunnelList cuando un túnel transiciona entre Running y no-Running, respetando el flag EnableNotifications de SettingsViewModel.
+ Tambien quiero un icono para la APP, se me ocurre que puede ser el icono original de wireguard, pero con el texto UI abajo a la derecha
> Si lo consigues generar crea varias resoluciones mas tipicas
# Implementado: creado icon.svg (hexágono WireGuard-inspired en rojo #C0392B con badge "UI" en la esquina inferior derecha). Generadas resoluciones 16/24/32/48/64/128/256px como PNGs en Assets/. Empaquetado multi-resolución en icon.ico. ApplicationIcon referenciado en WireGuard.UI.csproj y Window.Icon en MainWindow.axaml.
+ Una vez generado el icono, me gutaría que la APP tuviera un Tray icon
> Cuando es minimizada lo haga a tray icon, además el icono del tray acambia si hay un tunnel conectado o no.
# Implementado: TrayIcon configurado en App.axaml.cs con menú Mostrar/Salir. Al hacer doble click restaura la ventana. Al minimizar o cerrar la ventana con MinimizeToTray=true, se oculta a bandeja. Icono cambia a icon_connected.ico cuando AnyTunnelRunning=true, vuelve a icon.ico cuando no. Generado icon_connected.ico (icono base + punto verde en esquina inferior derecha) con 7 resoluciones.
+ La ventana de UI es Resizable, está bien, pero el minimo debe ser aque en el cual no se descuadre los controles, por ejemplo llega un punto que a lo alto se oculta el botón "Import Tunnel"
# Implementado: MinWidth=720, MinHeight=500 en MainWindow.axaml.
+ La UI la quiero con todos los textos en español.\n# Implementado: traducidos todos los textos de MainWindow.axaml (tabs, botones, labels, watermarks, paginación) y mensajes de ViewModels (TunnelViewModel.StatusText, ImportTunnelViewModel validations).
+ Añade a la UI una interfaz de configuración para settings de la APP UI
# Implementado: nueva clase SettingsViewModel (persiste en %AppData%\WireGuard-WinUserUI\settings.json). Nuevo tab "Configuración" en MainWindow.axaml con: intervalo de refresco, activar notificaciones, minimizar a bandeja, iniciar minimizado. El intervalo se usa en el background loop.

+ FYI: El documento que creamos anteriormente para indicar las instrucciones , normas y glosarios del proyecto me referiré al mismo como el "documento de instrucciones"
+ Cuando edito un tunnel el nombre del tunnel no quiero que sea editable quita la posibilidad desde la UI , pero tambien contola desde el servicio
# Implementado: TunnelEditorViewModel.IsEditMode=true pone IsReadOnly=true en el TextBox del nombre. El servicio también garantiza que siempre usa el TunnelName del request IPC como key, sin parsear el nombre del contenido .conf.
+ El comportramiento de la ventan es, cuando le doy al botón cerrar se minimiza al tray, cuando le dow a minimizar se minimiza normalmente (ajusta esto tambien en la configuración)
# Implementado: eliminado el handler de PropertyChanged(WindowStateProperty) que enviaba al tray al minimizar. OnClosing mantiene el comportamiento cerrar→tray. Actualizado el label del switch en Configuración para reflejar que solo aplica al botón cerrar.
+ Cuando conecto o desconecto un tunnel no aparece la notificacion de Windows. Añade una configuracion para habilitar o deshabiltar las notificaciones.
# Implementado: corregido WindowsNotificationService — eliminado el registro ToastNotificationManagerCompat.OnActivated que silenciaba notificaciones al fallar en apps no empaquetadas. Ahora se llama directamente a ToastContentBuilder.Show() con try/catch. Añadido SetCurrentProcessExplicitAppUserModelID (AUMID) para que Windows identifique el origen del toast. El control EnableNotifications ya existía en SettingsViewModel y se comprueba en SyncTunnelList.
+ En la Configuración no quiero que sea necesario darle a "Guardar configuración" desde que se cambie un valor se amacena el cambio
# Implementado: SettingsViewModel.OnPropertyChanged guarda automáticamente tras cualquier cambio (usando flag _isLoaded para no guardar durante Load). Eliminados SaveCommand, SavedMessage y el bloque "Guardar" del AXAML.
+ En lugar de checkbox en la configuración quiero usar switches tipo Slide
# Implementado: reemplazados los 3 CheckBox por ToggleSwitch de Avalonia FluentTheme con OnContent/OffContent descriptivos para cada opción.
+ Cuando editamos un tunnel la pestaña importar cambia a "Editar" y ya se queda así, de hecho quiero quitar la pestaña Editar importar.
# Implementado: eliminado el tab Importar/Editar de MainWindow.axaml. Creados TunnelEditorViewModel, TunnelEditorWindow.axaml y TunnelEditorWindow.axaml.cs (ventana modal independiente). Añadidos botones "＋ Nuevo túnel" e "📂 Importar .conf" a la barra superior del tab Túneles (visibles solo para AdvancedOperator). EditTunnelAsync abre la misma ventana en modo Edit con el nombre en solo lectura. El servicio también protege el nombre ya que siempre usa el TunnelName del request IPC como identificador.
> Para importar tuneles quiero un botón para crear o importar un nuevo tunel.
> Si es nuevo tunel quiero una nueva ventana que permita indicar el nombre del tunnel y la configuración .conf, o sea lo mismo que la pestaña editar/importar pero en una ventana nueva
> Si es importación de un tunnel muestra el dialogo de abrir archivo para selecciona el .conf y abrirlo en el  editor/creador de tunnel que implementamos en el punto anterior
> Y usaremos el mismo para editar los tuneles ya creados, aqui con la salvedad de no permitir cambiar el nombre.
> Añade procesos de validacion de la configuracion
> Añade al documento de instrucciones la existencia y uso de este editor de tuneles.
+ En la UI en la pestaña principal no tiene sentido tener el boton actualizar, quitalo
# Implementado: eliminado el StackPanel con el botón "Actualizar" del tab Túneles.
+ En la UI añade de fondo como marca de agua el una version minimalista del logo que usamos como icono.
# Implementado: añadida Image con icon_256.png en Opacity=0.04 dentro de un Grid en el tab Túneles, centrada y con IsHitTestVisible=False para no interferir con los controles.
+ Añade iconos representativos de sus acciones a los botones de los tuneles.
# Implementado: botones Conectar (▶), Desconectar (⏹), Reiniciar (↺), Editar (✏), Eliminar (🗑) y Exportar (⬇) con iconos Unicode inline y ToolTip.Tip descriptivo.
+ La card o frame donde está representado el tunel quiero que cambie de color segun si está conectado o no
# Implementado: añadida propiedad CardBackground en TunnelViewModel con colores distintos para Running (#162A1E verde oscuro), Pending (#2A2516 ámbar oscuro), Error (#2A1616 rojo oscuro), y Stopped (#252B3A por defecto). Bindeado en MainWindow.axaml.
+ En la info de lo túneles que están conectado tambien quiero que muestres la IP local del tunel.
# Implementado: WireGuardStats.GetStats ahora también ejecuta `wg showconf <name>` y parsea la línea `Address =` del bloque [Interface] para obtener la IP local del túnel (ej. 10.8.0.2/24). El campo TunnelAddress en TunnelInfo se rellena correctamente y se muestra en la card ya con el label "IP del túnel".
+ En la bottom bar quiero que añadas el dato de la IP pública:
# Implementado: creado PublicIpService con 6 proveedores HTTP en cadena de fallback (ipify, ipinfo, my-ip.io, ifconfig.me, api4.my-ip.io, checkip.amazonaws.com) y resolución DNS via UDP a myip.opendns.com@resolver1.opendns.com. Control de tasa: ≥30s entre peticiones no forzadas. La IP se refresca al arrancar la app (force=true), al conectar/desconectar túneles (force=true), y al restaurar desde tray (rate-limited). Añadida sección "IP pública:" en la bottom bar con botón ↺; mientras consulta muestra un ↺ giratorio (animación CSS keyframe Avalonia). Propiedad PublicIp y IsRefreshingPublicIp en MainWindowViewModel. RefreshPublicIpCommand para el botón manual (force=true), RequestPublicIpRefresh() para eventos de ventana (rate-limited).
> Quiero que sea una funcion que use varios metodos para determinarla, usando ipinfo.io/json , ifconfig.me , y otras alternativas similares, tambien el metodo myip.opendns.com resolver1.opendns.com DNS.
> Que haga las peticiones usando como fallback una lista de los servicios descritos y los nuevos.
> Unicamente solicta la IP cuando arranca la UI o se muestra despues de estar en un tray, y cuando se conecta o desconecta un tunnel
> añade control de peticiones, para no crear floob de solicitudes, dejanodo un tiempo prudencial entre peticiones, cuando lo eventos son las de inicio de la app y o de salir del tray. Cuando se conecta o desconecta un tunnel tienes que hacerlo obligatoria mente
> Le ponemos junto a la IP un botón con una flechas en redondo para refrescar manualmente y que se animen mientras está consultando la IP (ya sea invocado manualmente o automaticamente)
+ En la bottobar anteriormente te dije de poner el Role del usaurio y pusiste el nombre de usuario y el role, el nombre de usuario no lo quiero , solo el role.
> Tambien está el indicador de estado de conexion, no quiero el texto , me vale solo con el boliche verde o rojo
# Implementado: eliminado TextBlock del nombre de usuario; el badge de rol es el único elemento a la derecha. Eliminado el texto del indicador de conexión; solo queda el punto verde/rojo.

+ La solución debe comprobar si Wireguard está instalado si no lo está debes anular las operaciones, y mostrar una advertencia de que debe estar instalado para funcionar con normalidad.
# Implementado: añadida propiedad IsWireGuardInstalled en MainWindowViewModel (verifica File.Exists de wireguard.exe en %ProgramFiles%\WireGuard). En MainWindow.axaml se muestra un banner naranja oscuro con icono ⚠ cuando WireGuard no está instalado. En TunnelManager.RunWireGuardAsync el mensaje de error cuando falta wireguard.exe ahora da instrucciones al usuario en español.
+ Con las ultimas implementaciones has dejado de mostrar la informacion relativa a la conexion en el tunel conectado, vuelve  implementarlas:
> endpoint, ultimo handshake, rx y tx y la ip local del tunel (CIDR)
# Implementado: WireGuardStats.GetStats ahora lee el archivo .conf directamente desde %ProgramData%\WireGuard\<name>.conf para obtener la línea Address (wg showconf en Windows NO incluye Address, es un concepto de wireguard-windows que no forma parte del kernel WireGuard). Añadido [NotifyPropertyChangedFor(nameof(HasDetails))] a _endpoint en TunnelViewModel para que HasDetails notifique correctamente cuando solo se actualiza el Endpoint.

+ En %ProgramData%\WireGuard\<name>.conf estás guardando los .conf y ahí con accesibles para todos los usuarios del sistema, lo cual hace que expongamos las claves privadas
# Implementado: SecureConfFile() en TunnelManager aplica ACL con FileSecurity tras cada escritura de .conf. Desactiva herencia, elimina todas las ACEs y añade FullControl solo para SYSTEM (WellKnownSidType.LocalSystemSid) y BUILTIN\Administrators (BuiltinAdministratorsSid). Se llama en ImportTunnelAsync y EditTunnelAsync.
+ Ahora mismo está detectado como IP pública la 104.28.251.170 cuando mi ip pública es la 47.58.85.30
> Añade en la configuración la seleccion del servicio principal para resolver la ip pública y la posibilidad de desactivar/activar servicios de fallback. Es decir entre todos los servicios disponibles, elegir uno como el principal y luego el resto activarlo o no como fallback (usa un switch slide)
> Tambien pon en cada uno un botón para que al pulsarlo indique que IP estña resolviendo en ese momento.
# Implementado: PublicIpService refactorizado con AllProviders (7 proveedores nombrados, id+displayName) y TestProviderAsync. PublicIpProviderViewModel con TestCommand y CancellationToken. SettingsViewModel expone ObservableCollection<PublicIpProviderViewModel> + SelectedPrimaryProvider + GetOrderedEnabledProviderIds(). Settings persiste PrimaryProviderId + EnabledFallbackIds en settings.json. Sección "Resolución de IP pública" en Configuración: ComboBox para principal, ToggleSwitch + botón "Probar" por fallback.
+ Haz limpiezas periódicas del archivo audit.jsonl a fin de que no crezca mucho.
# Implementado: JsonAuditLogger trimea el archivo a las últimas 10.000 entradas al arrancar el servicio (primer LogAsync) y cada 500 escrituras. TrimFileAsync usa File.ReadAllLinesAsync + File.WriteAllLinesAsync con slice [^MaxEntries..].
+ Cuando lanzo la app no está respetando el setting  "Iniciar minimizado en la bandeja"
# Implementado: en App.axaml.cs, tras SetupTrayIcon(), si Settings.StartMinimized=true se llama Show() seguido inmediatamente de Hide() para que la ventana arranque oculta en el tray sin mostrarse al usuario.
+ Quiero que las 3 TABS princopales, túneles , auditoría y configuración las acompañes con un icono representativo.
# Implementado: reemplazados los Header de texto plano por StackPanel con TextBlock de emoji (🖧 Túneles, 📋 Auditoría, ⚙ Configuración) en MainWindow.axaml.
+ Apunta en el documento de instrucciones que las versiones cuando empecemos a compilar, serán en este formato usando la fecha yyyy.MM.dd.HHmm
# Implementado: añadida sección 7 "Versioning" en project-guidelines.md con el formato yyyy.MM.dd.HHmm y ejemplos. Añadida también sección 8 "Tunnel Editor" documentando el editor modal (modos, acceso, validación, archivos clave, inmutabilidad del nombre).
# Implementado: añadida sección 7 "Versioning" en project-guidelines.md describiendo el formato yyyy.MM.dd.HHmm con ejemplos. También añadida sección 8 "Tunnel Editor" documentando el editor modal.
+ Por defecto la app no se iniciará automaticamente al iniciar windows, me refiero a la UI el servicio si autoinicia siempre, entonces para la APP UI le ponemos un setting para habilitar el inicio automatico con windows cuando el usuario inicia sesion.
# Implementado: añadida propiedad StartWithWindows en SettingsViewModel, respaldada por HKCU\Software\Microsoft\Windows\CurrentVersion\Run. Al activar escribe el valor con la ruta del exe; al desactivar lo elimina. Load() lee el registro para sincronizar el estado inicial. ToggleSwitch añadido en tab Configuración bajo sección "Inicio con Windows".
+ Quiero tambien que añadas un About
> El autor pHiR0
> La url del proyecto https://github.com/pHiR0/WireGuard-WinUserUI (usa el icono de github)
> Y la información que crear relevante.
# Implementado: nueva Tab "Acerca de" (ℹ️) con logo, nombre, versión leída desde Assembly, autor pHiR0, botón GitHub que abre el repo en el navegador con Process.Start(UseShellExecute=true), y descripción del proyecto. OpenGitHubCommand y AppVersion añadidos en MainWindowViewModel.
+ Al finalizar la iteración quiero que me digas que fases faltan por implementar y que implican. Tambien a futuro como compilo para tener los archivos .exe y explorar opciones para hacer un instalador
+ He probado a crear/importar un tunnel e inmediatamente cuando lo importa/crea se conecta, y no quiero que se conecte automaticamente al importar/crear
# Implementado: ImportTunnelAsync detiene el servicio tras /installtunnelservice y establece startup=demand. EditTunnelAsync preserva el tipo de inicio original.
+ Quiero añadir una opcion para cada túnel que sea para autoiniciarlo.
> De hecho veo que se crea un servicio en windows para cada tunnel, y con inicio automático, por defecto el inicio es manual
# Implementado: IpcCommand.SetTunnelAutoStart + SetTunnelAutoStartAsync en TunnelManager (sc.exe config start=auto|demand). TunnelInfo.AutoStart leído desde registro HKLM. Toggle ToggleSwitch en cada tarjeta de túnel (visible para AdvancedOperator+).
+ En la ventana de APP UI junto al Titulode la app , no me refiero en la barra de titulo sino justo encima de las TABS junto al título pon el icono de la app
# Implementado: añadida Image con icon_32.png (Width=28 Height=28) en el DockPanel del header, a la izquierda del TextBlock "WireGuard Manager".
+ En los tunnels junto con las estadisticas quiero que aparezca el tiepo de conexion
# Implementado: _connectedSince en TunnelViewModel se registra cuando Status transiciona a Running. ConnectionTimeText computa el tiempo transcurrido (MMm SSs o HHh MMm SSs). Se refresca en OnStatusChanged y en cada UpdateFrom(). Fila extra "Conectado:" en el grid de estadísticas, visible solo cuando ConnectionTimeText no es null.

+ Cuando se cree un tunel ya sea importado o creado , no quiero que se cree conectado, si wireguard lo conecta automaticamente, desconectalo.
# Implementado: extraído EnsureTunnelStoppedAsync que espera StartPending→Running antes de parar, para evitar la race condition. ImportTunnelAsync y EditTunnelAsync llaman a este método tras /installtunnelservice. EditTunnelAsync también corregido: ahora detiene el servicio si !wasRunning (antes lo dejaba conectado aunque el túnel estuviera parado antes de editar).
+ Prepara la fase 5 que será la compilación y la creación del archivo de instalacion, me decido por WiX ToolSet v4 para crear un msi que instale todo.
> El servicio y el UI
> Que el servicio auto inicie
> Que el msi sirva tambien para actualizar de versiones anteriores a mas reciente.
# Implementado: creado installer/ con WiX v5 SDK (WireGuard-WinUserUI.wixproj, Product.wxs con MajorUpgrade+UpgradeCode fijo, Directories.wxs, Components.wxs con ServiceInstall Start=auto y ServiceControl Stop=both/Remove=uninstall). Actualizado scripts/package.ps1: publica service+UI como self-contained single-file win-x64, llama a dotnet build del .wixproj pasando rutas de publicación como variables, copia el MSI final a artifacts/ con el nombre versionado. Actualizado scripts/build.ps1 para compilar sin .sln. README detallado en installer/.

+ Cuando abro la Aplicacion de abre la ventana main automaticamente, aunque tenga marcado "Iniciar minimizado en la bandeja" en la configuración.
# Implementado: eliminado desktop.MainWindow (que forzaba Show() automático en Avalonia tras OnFrameworkInitializationCompleted). Ahora se usa ShutdownMode.OnExplicitShutdown y se llama _mainWindow.Show() solo si !Settings.StartMinimized. Se añade IsExiting flag estático en App para que MainWindow.OnClosing no cancele el cierre cuando el usuario elige "Salir" desde el tray.
+ El Instalador está en inglés, lo quiero en español, tambien genera los terminos y condiciones genericos para el instalador
# Implementado: creado installer/License.rtf con T&Cs genéricos en español (10 secciones: licencia, restricciones, propiedad intelectual, software terceros, exenciones, limitaciones, datos, modificaciones, legislación y aceptación). Creado installer/es-ES.wxl con namespace WiX v5 y WixUILicenseRtf apuntando al RTF. Añadido Cultures=es-ES al wixproj para que el WixToolset.UI.wixext use sus traducciones integradas en español.
+ Una vez instalado en el panel de control en "Desintalar programas" del panel de control no aparece el icono de la aplicacion, y tampoco aparece la version de la misma
# Implementado: añadido <Icon Id="AppIcon.ico"> referenciando src/UI/Assets/icon.ico y <Property Id="ARPPRODUCTICON"> en Product.wxs. Package Version ahora usa $(var.ProductVersion) mapeado desde la propiedad MSBuild $(Version) vía DefineConstants, por lo que la versión real (ej. 2026.4.4.0201) aparece en Desinstalar programas. Añadido SuppressIces=ICE24 para permitir formato de versión basado en fecha (Major > 255 técnicamente inválido por MSI spec pero funcional). ARPCOMMENTS muestra la versión completa de compilación.
+ Tambien quiero que se cree un acceso directo en el Escritorio
# Implementado: añadida StandardDirectory Id="DesktopFolder" en Directories.wxs. Nuevo ComponentGroup CG_DesktopShortcut en Components.wxs con Component anclado en HKCU (ICE compliance) y Shortcut SC_Desktop apuntando a WireGuard.UI.exe. Referenciado en Feature FeatureMain de Product.wxs. package.ps1 actualizado con -Recurse en Get-ChildItem para encontrar el MSI en el subdirectorio es-ES/ que genera la localización de cultura.

+ Genera el readme.me principal, de cara a convertir el repo en public (ahora mismo está privado), incluye entre todo lo necesario, que el proyecto se ha hecho mediante vibe coding y tambien cual es la necesidad de wireguard para windows que viene a suplir
# Implementado: README.md completo reescrito en UTF-8. Incluye: descripción del problema (WireGuard solo para admins en Windows), características de la app, tabla de roles vs grupos de Windows, requisitos previos, instrucciones de instalación (MSI y desde código), cómo generar el MSI, estructura del proyecto, stack tecnológico, resumen de seguridad, sección de 'vibe coding con IA', y licencia propietaria no comercial.
+ Tambien reordena el resto de documentación del proyecto
# Implementado: eliminado deployment-plan.md (completamente obsoleto). architecture.md reescrito con la arquitectura real implementada (diagrama ASCII, tabla de proyectos, componentes del servicio y UI, principios de diseño). roadmap.md actualizado con el estado real de las 5 fases completadas y sección de fases futuras.
+ Elimina de aqui todo lo relacionado con el paquete de choco, ya que lo implementaremos en otro repo
# Implementado: eliminada la carpeta packaging/chocolatey/ completa (nuspec + tools/chocolateyInstall.ps1 + tools/chocolateyUninstall.ps1). La carpeta packaging/ también eliminada al quedar vacía. El paquete Chocolatey se gestionará en un repositorio independiente.
+ Reordena la estructura si es necesario.
# Implementado: estructura revisada. Eliminada carpeta build/ (solo tenia .gitkeep sin uso; el output va a artifacts/ que está en .gitignore). Eliminada packaging/ al quedar vacía tras borrar chocolatey. El resto de la estructura (src/, tests/, installer/, scripts/, docs/) es coherente y no requiere cambios.
+ Actualiza el .gitignore para incluir archivo que no sean necesarios, supongo que los .msi y los.exe debemos subirlos al git
# Implementado: .gitignore reorganizado con secciones claras (Build, IDE, Logs, Tests, OS, Artefactos binarios, WiX, NuGet, Secretos, Publish). Los .exe y .msi se mantienen excluidos — la práctica recomendada es distribuir binarios via GitHub Releases, no commitearlos al repo. Se añaden también *.wixpdb, *.wixobj, *.wixlib, publish/, coverage/ y otros artefactos de WiX/.NET.
+ Me gustaría que en repo no se viera, o no se publicara este ToDo.md no el ChuletasPrompts.md pero no quiero meterlos en el .gitignore tampoco, solo no quiero que esten disponibles en el repo público ¿Que opciones crees que me pueden valer?
# Implementado: solución via dos pasos sin tocar .gitignore:
# 1) git rm --cached docs/ToDo.md docs/ChuletasPrompts.md — los saca del índice de git (permanecen en disco)
# 2) .git/info/exclude — archivo local por-máquina (igual que .gitignore pero nunca se commitea) que evita que git sugiera añadirlos de nuevo.
# A partir de ahora estos archivos son solo locales y no aparecerán en el repo público.
# NOTA: para limpiar la historia antigua (commits previos donde existían) antes de hacer el repo público, ejecutar:
# git filter-repo --path docs/ToDo.md --path docs/ChuletasPrompts.md --invert-paths
# (requiere: pip install git-filter-repo)

+ Agrega al scrip package.ps1 que cuando se cree un nuevo instalador genere tambien el paquete de chocolatey (.nupgk):
> Debe actualizar la version en el .nuspec y actualizar el hash 256 en el chocolateyinstall.ps1 en base al instalador generado de la misma version. No hace falta actualizar la version porque la he cambiado para usar la variable $env:ChocolateyPackageVersion
# Implementado: scripts/package.ps1 paso 4 calcula SHA256 del MSI generado, actualiza checksum64 en chocolateyinstall.ps1 y <version> en el .nuspec con la versión actual del build, luego ejecuta `choco pack` y copia el .nupkg a artifacts/chocolatey/. Omitido automáticamente si choco no está instalado (-SkipInstaller) o el MSI no existe.
+ La app no quiero que se puede abrir mas de una vez por cada usuario, me sucede que si ya la tengo abierta, ya sea minimizada, normal o en el tray, se vuelve a abrir otra sesion de la app. En estos casos si ya hay una app abierta en el mismo usuario, lo que tiene que hacer es "mostrar" la que ya está corriendo.
# Implementado: Program.cs usa un Mutex con nombre único (WireGuardManager_SingleInstance_pHiR0_v1). Si el Mutex ya está tomado, la nueva instancia envía un byte por NamedPipe (WireGuardManagerShow_pHiR0_v1) y sale. La instancia ya en ejecución tiene un thread de fondo que escucha el pipe y llama a App.RequestShowMainWindow() que marshalía a UI thread para mostrar y activar la ventana.
+ Tambien en un equipo corporativo, que usas cuentas de usuario del Active Directory, he añadido el usuario con el que inicio sesion a un grupo de "Wireguard UI" y sigue sin mostrarse nada en la app solo se abre la ventana, se muestra la tab de tunnels, pero sin conetenido de tuneles (bien) la de , la de configuración y acerca de se muestran correctamente.
> Adicionalmente en la botom bar aparece el boliche rojo, y no aparece ni la IP pública.
> Entiendo que es porque auqneu el usuario esté metido en el grupo "Wireguard UI" no lo detecta bien, y no detecta el rol del mismo. Para estos casos quiero que muestre un mensaje claro de que no tiene permisos para gestionar los túneles.
# Implementado: IRoleStore.GetRoleAsync ahora acepta userSid opcional. WindowsGroupRoleStore utiliza búsqueda SID-based (IsMemberBySidOrName) que primero intenta lookup por SamAccountName (usuarios locales, rápido) y si falla enumera miembros del grupo comparando SIDs (funciona para usuarios de dominio). PipeServer pasa callingSid a ProcessMessageAsync y RequestHandler. BackgroundLoopAsync en UI maneja el caso role=None sin lanzar excepción: marca IsConnected=true y muestra banner púrpura "Sin permisos para gestionar túneles".
+ En la configuracion, el setting "Intervalo (segundos)" quítalo y ese valor es hardcodeado a 5 segundos
# Implementado: eliminada la propiedad RefreshIntervalSeconds de SettingsViewModel, Save(), Load() y SettingsData. El bloque UI en MainWindow.axaml eliminado. MainWindowViewModel.BackgroundLoopAsync usa la constante RefreshIntervalMs=5000 directamente.
+ Tambien en la configuración en la seccion de resolucion de "IP Pública" quita los servicios apu.ipify.org y checkip.amazonaws.com ya que no funcionan bien.
# Implementado: eliminados los proveedores "ipify" (api.ipify.org) y "amazonaws" (checkip.amazonaws.com) de AllProviders y _fetchByProvider en PublicIpService.cs. Proveedor primario por defecto cambiado a "ipinfo". Quedan: ipinfo.io, api.my-ip.io, ifconfig.me, api4.my-ip.io y DNS (OpenDNS).
+ El mensaje que aparece cuando no tienes Wireguard instalado, es muy largo horzontalmente y no se ve completo, convertelo en un mensaje de 2 lineas.
# Implementado: el TextBlock único reemplazado por un StackPanel con 2 TextBlock separados en MainWindow.axaml. Línea 1: aviso de no instalado + funciones deshabilitadas. Línea 2: enlace de descarga.
+ Los iconos que compañan a las pesstañas, son muy pequeños, hazlo mas grandes mas acordes al tamaño de la fuente del nombre de la TAB. En los botones de los túneles tambien no se ven bien, creo que mejor puedes elegir otro tema de iconos y usar otro pack de iconos.
# Implementado: FontSize de iconos de tabs aumentado de 13 a 16 en las 4 tabs (Túneles, Auditoría, Configuración, Acerca de). Botones de túneles migrados a Segoe MDL2 Assets (fuente de sistema Windows): E768 Conectar, E71A Desconectar, E72C Reiniciar, E70F Editar, E74D Eliminar, E896 Exportar; tamaño 13px. Botones toolbar Nuevo/Importar también actualizados (E710, ED25).
+ La ventana de Editar/crear túnel no es lo suficientemente alta de forma predeterminada como para que se vea el badge ,que está debajo del area de texto de configuracion, que indica si la configuración es válida o no. Haz que se abra inicialmente viendose ese badge, y que nose pueda reducir la ventana mas alla del badge, que siempre se vea.
# Implementado: los 3 badges de validación (verde OK, rojo errores, rojo API error) se han movido fuera del ScrollViewer al DockPanel principal con DockPanel.Dock=Bottom. Esto garantiza que siempre sean visibles independientemente de la altura. Height aumentado a 640 y MinHeight a 540.

+ Migra los iconos de los tabs tambien a Segoe MDL2 Assets
# Implementado: los 4 tabs migrados de emojis Unicode a Segoe MDL2 Assets — Túneles E701 (WiFi/network), Auditoría E7BA (History/registros), Configuración E713 (Settings gear), Acerca de E946 (Info). FontFamily="Segoe MDL2 Assets" FontSize=16 en cada TextBlock de icono.

- En la interfaz, para un usuario con el Rol Operador, no debería ni aparecer el botón exportar ni el de eliminar.
> Tampoco debe tener el switch slide para designar o quitar como "Inicio automatico" y tampoco debe tener acceso a "Editar" el tunel
- Tambien vamos a agregar un setting mediante alguna clave de registro, para que aquellos usuarios que no tengan asignado un rol, o sea no estén en ninguno de los grupos de roles, sean tratados como un usuario con rol operador. Esto es si esa clave está presente y habilitada, cualquier usuario , que NO esté en un grupo de rol de Wireguard Manager , se le asignará el rol de operador, pero sin necesidad de meterlo en el grupo.
> Para los usuario con rol administrador, en la configuración quiero que apareza un setting con un switch slide para activar o desactivar esto. Además en la misma debe hacerse notar que es una configuración global, no del usuario.
> Por defecto no está activado.
> Añade esto a la documentación README.md
- He editado un tunnel, y cuando le he dado a guardar los cambios me ha dado error porque dice que el tunnel ya existe, pero de buenas a primeras se eliminó y entonces si me dejó guardar los cambios.
> Supongo que cuando editamos deberemos agregar algun tiempo de espera o comprobar que el tunnel no existe para commitear los cambios al mismo.
- Cuando hago scroll en "Acerca de". no baja lo suficiente para mostar todo el cuadro de la licencia.
- No sé si ya estaba así, pero quiero que el "Servicio Principal" por defecto para la resolución de IP Pública sea "DNS (OpenDNS)"