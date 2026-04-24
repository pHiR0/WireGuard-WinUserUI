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

+ En la interfaz, para un usuario con el Rol Operador, no debería ni aparecer el botón exportar ni el de eliminar.
> Tampoco debe tener el switch slide para designar o quitar como "Inicio automatico" y tampoco debe tener acceso a "Editar" el tunel
# Implementado: añadido IsVisible="{Binding IsAdvancedOperator}" (via parent ItemsControl) a los botones Eliminar y Exportar en MainWindow.axaml. Los botones Editar y el toggle de Inicio automático ya tenían esta restricción previamente.
+ Tambien vamos a agregar un setting mediante alguna clave de registro, para que aquellos usuarios que no tengan asignado un rol, o sea no estén en ninguno de los grupos de roles, sean tratados como un usuario con rol operador. Esto es si esa clave está presente y habilitada, cualquier usuario , que NO esté en un grupo de rol de Wireguard Manager , se le asignará el rol de operador, pero sin necesidad de meterlo en el grupo.
> Para los usuario con rol administrador, en la configuración quiero que apareza un setting con un switch slide para activar o desactivar esto. Además en la misma debe hacerse notar que es una configuración global, no del usuario.
> Por defecto no está activado.
> Añade esto a la documentación README.md
# Implementado: GlobalSettingsStore.cs lee/escribe HKLM\SOFTWARE\WireGuard-WinUserUI\AllUsersDefaultOperator (DWORD). WindowsGroupRoleStore aplica el fallback a Operator cuando el flag está activo y el usuario no tiene rol. IPC commands GetGlobalSettings y SetGlobalSettings (solo Admin). UI: AllUsersDefaultOperator en MainWindowViewModel, cargado tras conectar, guardado al cambiar. Settings tab muestra sección admin con toggle solo visible para Admin. README.md actualizado con descripción completa.
+ He editado un tunnel, y cuando le he dado a guardar los cambios me ha dado error porque dice que el tunnel ya existe, pero de buenas a primeras se eliminó y entonces si me dejó guardar los cambios.
> Supongo que cuando editamos deberemos agregar algun tiempo de espera o comprobar que el tunnel no existe para commitear los cambios al mismo.
# Implementado: añadido WaitForServiceRemovedAsync() en TunnelManager.cs. Después de /uninstalltunnelservice, el método sondea el SCM hasta 20 veces (250ms entre intentos) para confirmar que la entrada del servicio ha desaparecido. Evita la condición de carrera "ya existe" al reinstalar.
+ Cuando hago scroll en "Acerca de". no baja lo suficiente para mostar todo el cuadro de la licencia.
# Implementado: reemplazado Padding="16" del ScrollViewer por Margin="16,16,16,24" en el StackPanel interior. En Avalonia el padding del ScrollViewer no se incluye en el extent de scroll, causando que el último elemento quedara cortado.
+ No sé si ya estaba así, pero quiero que el "Servicio Principal" por defecto para la resolución de IP Pública sea "DNS (OpenDNS)"
# Implementado: cambiado el proveedor por defecto de "ipinfo" a "dns" en SettingsViewModel.cs (Save(), Load() y SettingsData). Nuevas instalaciones o usuarios sin settings.json previo tendrán DNS (OpenDNS) como primario.
+ Habíamos implementado que no se permiten mas de 1 sesion por usuario, pero en caso de que se lancen 2 simultaneas, y por lo que sea a una no le da tiempo a detectar la otra, quiero que iterativametne cada X segundos compruebe cuantos hay abierto , para el mismo usuario, es decir revise en su mismo espacio de usuario si hay otras sesiones abiertas, y que si detecta mas de una cada proceso por si mismo, mire cual de todas las sesiones abiertas es la que lleva mas tiempo en ejecución o sea la que se ejecutó primero, y si no es la primera de todas se autocierre.
# Implementado: DuplicateGuardLoopAsync() en Program.cs. Cada 30s enumera procesos con el mismo nombre y misma SessionId de Windows (mismo usuario). Si detecta un peer con StartTime anterior al propio, señala a esa instancia (pipe show) y llama Environment.Exit(0). El primer check se hace tras 30s de espera para dejar que la app arranque. Maneja acceso denegado a StartTime con try/catch.

+ Al raro pasa porque aunque todo funciona bien, y se ven los tuneles cargados, detras se sigue viendo (vuelve a aparecer) la animación de "Conectando al servicio" con al barra de animación.
# Implementado: doble fix. (1) AXAML: añadido IsVisible="{Binding !IsLoadingTunnels}" al ScrollViewer para que spinner y lista de túneles sean mutuamente excluyentes. (2) ViewModel: el handler del evento Disconnected ahora tiene un guard "if (!IsConnected) return" para ignorar disparos tardíos que lleguen después de que el BackgroundLoop ya haya reconectado, evitando la race condition que ponía IsLoadingTunnels=true con la lista ya poblada.

+ Tras la ultima correccion, ahora lo que pasa es que al abrir la app , se ve los tuneles pero inmediatametne desaparecen y se ve "Conectando al servicio" con la barra/spinner indefinidamente, y no aparecen los tuneles.
# Implementado: el fix anterior era incompleto. PipeClient auto-reconecta internamente en SendAsync (dispara Disconnected → reconecta → dispara Reconnected), tras lo cual _pipeClient.IsConnected ya es true. BackgroundLoop iba al else-branch y sincronizaba túneles pero NUNCA reseteaba IsLoadingTunnels=false, dejando el spinner permanente. Fix: el else-branch ahora siempre ejecuta IsLoadingTunnels=false en el Dispatcher, independientemente del rol, antes de llamar a SyncTunnelList.