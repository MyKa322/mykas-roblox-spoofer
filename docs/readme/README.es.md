# MyKa's Roblox Spoofer

[English](../../README.md) | [العربية](README.ar.md) | [Português (Brasil)](README.pt-BR.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Türkçe](README.tr.md) | [Русский](README.ru.md) | [Tiếng Việt](README.vi.md) | [ไทย](README.th.md) | [Deutsch](README.de.md) | [Español](README.es.md) | [Українська](README.uk.md) | [Polski](README.pl.md) | [বাংলা](README.bn.md)

MyKa's Roblox Spoofer es una aplicación de terminal para Windows. La versión 0.3 permite añadir cuentas Roblox en ventanas de inicio de sesión separadas, guardar sus datos de identidad confirmados y consultar diagnósticos del sistema. La interfaz incluye las pestañas Accounts, Diagnostics, Adapters e Identity, controles por teclado y un logotipo 3D giratorio. Pulse 4 para abrir Identity y después Enter u O para acceder a las herramientas de identidad del sistema.

En la versión 0.3, P en Accounts comprueba la cuenta del perfil guardado y abre la página de una experiencia. La solicitud de inicio no confirma que RobloxPlayer esté funcionando ni qué cuenta se ha unido. `identity manage`, o S en Adapters, abre capturas antes/después, comparación, copia de seguridad y cambios revisados de cadenas GUID existentes y ajustes MAC admitidos por el controlador. También permite renovar DHCP, vaciar la caché DNS y limpiar partes concretas de Roblox. Estas operaciones no modifican identificadores de firmware, crean equipos independientes simultáneos ni configuran una IP pública nueva. Los cambios reales con permisos de administrador aún deben probarse en una instalación desechable de Windows. Guías en inglés: [alcance y recuperación](../system-identity.md), [abrir una experiencia](../desktop-launch.md).

Este README está en español. La interfaz y los comandos de la aplicación siguen en inglés.

## Requisitos

La aplicación compilada requiere Windows x64 y [.NET Desktop Runtime 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0). Para ejecutar el código fuente se necesita .NET SDK 10. El inicio de sesión también requiere [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) y conexión a internet.

## Primer inicio

Abre PowerShell en la carpeta raíz del proyecto y ejecuta la aplicación desde el código fuente:

~~~powershell
dotnet run --project src/MyKa.Spoofer -c Release
~~~

Si la compilación local ya está lista, puedes iniciarla así. Conserva los archivos auxiliares en la misma carpeta que myka.exe:

~~~powershell
.\.artifacts\publish\myka.exe
.\.artifacts\publish\myka.exe --help
~~~

## Añadir una cuenta

1. Pulsa A en la pestaña Accounts. Roblox se abrirá en una ventana con su propio perfil de WebView2.
2. Introduce la contraseña y los códigos de verificación solo en la página de Roblox. Después de iniciar sesión, pulsa Verify account.
3. La aplicación comprueba la respuesta de `https://users.roblox.com/v1/users/authenticated`. Revisa el nombre y el ID numérico y pulsa Save account.

Las cuentas se distinguen por su ID numérico de Roblox. No se puede guardar la misma cuenta dos veces. Al volver a iniciar sesión se usa un perfil nuevo; una cuenta distinta o la cancelación no reemplazan la cuenta guardada. `not checked` significa que no se ha comprobado en ese momento si la sesión sigue siendo válida.

## Comandos

Añade estos comandos después de `.\.artifacts\publish\myka.exe`. Sustituye `LOCAL_ACCOUNT_ID` por el ID local de `accounts list`, no por el ID numérico de Roblox. Añadir, volver a iniciar sesión y eliminar cuentas requiere un terminal interactivo.

| Comando | Función |
| --- | --- |
| `accounts list` | Muestra las cuentas guardadas y sus IDs locales. |
| `accounts add` | Abre el inicio de sesión para una cuenta nueva. |
| `accounts login --account LOCAL_ACCOUNT_ID` | Vuelve a iniciar sesión en la cuenta seleccionada. |
| `accounts remove --account LOCAL_ACCOUNT_ID` | Pide confirmación para eliminar la cuenta y su perfil local. |
| `accounts cleanup` | Reintenta la limpieza de perfiles pendiente. |
| `doctor` | Comprueba Windows, .NET, WebView2 y la carpeta predeterminada de Roblox. |
| `identity inspect` | Muestra los adaptadores y las direcciones que informa Windows. |
| `logo` | Muestra el logotipo. |
| `--help` | Muestra la ayuda de los comandos. |

## Controles

| Teclas | Acción |
| --- | --- |
| `← / →, Tab / Shift+Tab, 1-4` | Cambiar de pestaña. |
| `↑ / ↓, Home / End, PgUp / PgDn` | Seleccionar una cuenta o desplazarse por listas e informes. |
| `A, S / Enter, D / Delete, C` | En Accounts: añadir, iniciar sesión de nuevo, eliminar y limpiar perfiles. |
| `R` | Recargar la lista de cuentas o actualizar el informe. |
| `F1 / ?, Esc` | Abrir la guía de teclas; Esc la cierra. Sin guía ni confirmación abierta, Esc o Q cierra la aplicación. |
| `Space` | Pausar o reanudar el giro del logotipo. |
| `Y` | Confirmar la eliminación. Cualquier otra tecla conserva la cuenta. |
| `Ctrl+C` | Cancelar y salir con el código 130. |

Las pestañas necesitan al menos 50 columnas y 12 filas. `--plain` muestra un menú numerado sin colores ni animación; `--still` inicia con el logotipo quieto. La salida redirigida, `NO_COLOR` y `TERM=dumb` también usan texto sencillo.

## Datos guardados

Los datos se guardan en `%LOCALAPPDATA%\MyKa\RobloxSpoofer`. Cada cuenta tiene un perfil WebView2 independiente con los datos de sesión del navegador. `accounts.json` guarda IDs, nombres y fechas de verificación, sin contraseñas ni cookies. La aplicación no lee cookies ni campos del formulario de acceso.

La carpeta permite acceso al usuario actual de Windows, SYSTEM y administradores. Los metadatos se reemplazan de forma atómica con una copia `.bak`; los datos inválidos o con una versión de esquema desconocida se conservan sin cambios. Eliminar una cuenta borra su perfil local, pero no cierra sesiones Roblox en otros dispositivos.

## Si algo no funciona

- Si aparece `WebView2 unavailable`, instala o repara WebView2 Runtime. La ventana permite solo las páginas Roblox previstas mediante HTTPS. No se admiten métodos de acceso de otros proveedores que redirijan a sitios externos.
- Si Accounts muestra `unavailable`, cierra otras operaciones con cuentas y pulsa R. No sustituyas un `accounts.json` dañado por un archivo vacío.
- Si la limpieza queda pendiente, cierra las ventanas de acceso y ejecuta `accounts cleanup`. Una comprobación incompleta de `doctor` no demuestra que Roblox no esté instalado. No se detectan instalaciones de Store ni carpetas no estándar.

En la última revisión del código pasaron 691 pruebas. El acceso en la ventana real de Roblox, las pruebas con dos cuentas y el comportamiento en terminales reales todavía requieren comprobación manual. La presencia de archivos del cliente no confirma compatibilidad ni funcionamiento simultáneo de varios clientes.

## Más información

- [Arquitectura (inglés)](../architecture.md)
- [Investigación y limitaciones (inglés)](../research.md)
- [Interfaz del terminal (inglés)](../terminal-ui.md)
- [README completo en inglés, con compilación y pruebas](../../README.md)
