
# EnigmaToOculus

## Español

### Objetivo de la Aplicación

**EnigmaToOculus** es una aplicación diseñada para facilitar la reproducción de listas IPTV en las Oculus Quest (u otros dispositivos de realidad virtual) a través de cualquier programa de reproducción compatible con streams IPTV. La aplicación obtiene una lista de canales IPTV desde el OpenWebif de un receptor Enigma2 (como un decodificador de satélite) y permite a los usuarios seleccionar un canal para iniciar su streaming. El stream se genera como un archivo `.ts` (MPEG-TS) que puede ser reproducido en tiempo real en las Oculus Quest mediante un reproductor compatible.

Características principales:
- **Obtención de Canales**: Extrae la lista de canales IPTV desde el OpenWebif de un receptor Enigma2 a través de un archivo M3U.
- **Interfaz Web Intuitiva**: Proporciona un frontend web donde los usuarios pueden ver la lista de canales, buscar canales por nombre y gestionar el streaming.
- **Búsqueda Local**: Permite buscar canales por nombre directamente en el frontend, sin necesidad de recargar la página.
- **Streaming para Oculus Quest**: Inicia el streaming de un canal seleccionado, generando un archivo `.ts` que puede ser reproducido en tiempo real en las Oculus Quest usando un reproductor compatible.
- **Persistencia del Estado**: Al recargar la página, el frontend recuerda si hay un streaming activo y actualiza el estado de los botones.
- **Botonera Fija**: Muestra una barra fija en la parte inferior de la pantalla cuando un canal está en reproducción, indicando el nombre del canal y ofreciendo un botón para detener el streaming.

Esta aplicación es ideal para usuarios que desean disfrutar de su lista IPTV en realidad virtual, aprovechando las capacidades de las Oculus Quest.

### Requerimientos

#### Software
- **Docker**: Necesario para construir y ejecutar los contenedores de la aplicación.
- **Docker Compose**: Para gestionar los servicios (backend y frontend).
- **GStreamer**: Utilizado por el backend para procesar los streams IPTV. Se incluye en la imagen de Docker del backend.

#### Variables de Entorno
Debes configurar las siguientes variables de entorno en el archivo `docker-compose.yml` o en tu entorno:

- **M3U_URL**: URL del archivo M3U proporcionado por el OpenWebif del receptor Enigma2 (por ejemplo, la URL de la lista de canales de tu decodificador, como `http://<IP_DEL_DECODIFICADOR>/web/services.m3u`).
- **OUTPUT_DIR**: Directorio donde se guardarán los archivos `.ts` generados (por ejemplo, `/output`).

#### Hardware
- Un receptor Enigma2 (como un decodificador de satélite) con OpenWebif habilitado, que proporcione un archivo M3U con los canales IPTV.
- Una red local donde el receptor Enigma2 y el host de la aplicación puedan comunicarse.
- Un dispositivo Oculus Quest con un reproductor compatible con streams IPTV (por ejemplo, Bigscreen o MoonVR).

#### Rutas
- La aplicación espera que los archivos del proyecto estén descomprimidos en una ruta específica. En las instrucciones, usaremos `/path/to/enigmaToOculus` como ejemplo.
- Los archivos `.ts` generados se almacenarán en una carpeta compartida accesible por las Oculus Quest. En las instrucciones, usaremos `/path/to/DecoChannels` como ejemplo.

### Cómo Ejecutar la Aplicación

1. **Descomprimir el Proyecto**:
   - Descarga y descomprime el archivo del proyecto en una ruta de tu sistema. Por ejemplo:
     ```
     /path/to/enigmaToOculus
     ```
   - Asegúrate de que los archivos del proyecto (como `Dockerfile`, `frontend/`, etc.) estén dentro de esta carpeta.

2. **Configurar las Variables de Entorno y Rutas**:
   Edita el archivo `docker-compose.yml` para incluir las variables de entorno y rutas necesarias. A continuación, se muestra un ejemplo:
   ```yaml
   services:
     iptv-recorder:
       image: iptv-recorder:latest
       build:
         context: /path/to/enigmaToOculus
         dockerfile: Dockerfile
       network_mode: host
       volumes:
         - /path/to/DecoChannels:/output  # Salida de videos
         - /path/to/enigmaToOculus/cache:/cache  # Caché persistente
       environment:
         - M3U_URL=http://<IP_DEL_DECODIFICADOR>/web/services.m3u  # URL de la lista de canales de tu decodificador
         - OUTPUT_DIR=/output
       restart: unless-stopped
       container_name: enigmaToOculus

     frontend:
       build:
         context: /path/to/enigmaToOculus/frontend
         dockerfile: Dockerfile
       network_mode: host
       depends_on:
         - iptv-recorder
       restart: unless-stopped
       container_name: enigmaToOculus-frontend
   ```
   - Sustituye `/path/to/enigmaToOculus` por la ruta donde descomprimiste el proyecto.
   - Sustituye `/path/to/DecoChannels` por la ruta donde deseas que se generen los archivos `.ts`. Esta carpeta debe ser una carpeta compartida accesible por las Oculus Quest (por ejemplo, mediante SMB o un servidor DLNA).
   - Sustituye `http://<IP_DEL_DECODIFICADOR>/web/services.m3u` por la URL real de la lista de canales de tu decodificador Enigma2.

3. **Construir y Ejecutar los Contenedores**:
   Desde el directorio donde está el archivo `docker-compose.yml`, ejecuta:
   ```bash
   docker-compose up --build -d
   ```

4. **Acceder a la Aplicación**:
   - Abre un navegador en un dispositivo dentro de la misma red (por ejemplo, tu PC o teléfono) y ve a `http://<IP_DEL_HOST>:6678` (por ejemplo, `http://192.168.1.25:6678` si tu host es `192.168.1.25`).
   - Verás la interfaz web con la lista de canales.

5. **Usar la Aplicación**:
   - Usa el campo de búsqueda para filtrar canales por nombre.
   - Haz clic en "Iniciar" para comenzar el streaming de un canal. El archivo `.ts` se generará en la carpeta compartida (por ejemplo, `/path/to/DecoChannels/LA_1.ts`).
   - Mientras un canal se está reproduciendo, aparecerá una barra fija en la parte inferior del frontend con el nombre del canal y un botón "Parar".
   - Haz clic en "Parar" (en la barra o en el canal) para detener el streaming.
   - Si recargas la página, el estado del streaming se mantendrá.

6. **Reproducir el Stream en las Oculus Quest**:
   - Asegúrate de que la carpeta `/path/to/DecoChannels` sea una carpeta compartida accesible por las Oculus Quest. Esto puede lograrse mediante:
     - Un servidor DLNA (como MiniDLNA) para que las Oculus Quest puedan acceder a los archivos.
     - Un recurso compartido de red (SMB) al que las Oculus Quest puedan conectarse.
   - Abre un reproductor compatible en las Oculus Quest. Esta aplicación ha sido probada con:
     - **Bigscreen**: Usando un contenedor MiniDLNA para compartir los archivos `.ts`.
     - **MoonVR**: Accediendo al archivo `.ts` directamente desde un recurso compartido de red (SMB).
   - Selecciona el archivo `.ts` generado (por ejemplo, `LA_1.ts`) desde el reproductor.
   - ¡Disfruta del canal en tus Oculus Quest!

---

## English

### Application Objective

**EnigmaToOculus** is an application designed to enable the playback of IPTV playlists on the Oculus Quest (or other virtual reality devices) using any compatible IPTV streaming player. The application retrieves an IPTV channel list from the OpenWebif of an Enigma2 receiver (such as a satellite decoder) and allows users to select a channel to start streaming. The stream is generated as a `.ts` (MPEG-TS) file, which can be played in real-time on the Oculus Quest using a compatible player.

Main features:
- **Channel Retrieval**: Extracts the IPTV channel list from the OpenWebif of an Enigma2 receiver via an M3U file.
- **Intuitive Web Interface**: Provides a web frontend where users can view the channel list, search for channels by name, and manage streaming.
- **Local Search**: Allows searching for channels by name directly in the frontend without reloading the page.
- **Streaming for Oculus Quest**: Starts streaming a selected channel, generating a `.ts` file that can be played in real-time on the Oculus Quest using a compatible player.
- **State Persistence**: Upon page reload, the frontend remembers if a stream is active and updates the button states accordingly.
- **Fixed Control Bar**: Displays a fixed bar at the bottom of the screen when a channel is streaming, showing the channel name and offering a "Stop" button.

This application is ideal for users who want to enjoy their IPTV playlist in virtual reality, leveraging the capabilities of the Oculus Quest.

### Requirements

#### Software
- **Docker**: Required to build and run the application containers.
- **Docker Compose**: To manage the services (backend and frontend).
- **GStreamer**: Used by the backend to process IPTV streams. It is included in the backend Docker image.

#### Environment Variables
You must configure the following environment variables in the `docker-compose.yml` file or in your environment:

- **M3U_URL**: URL of the M3U file provided by the OpenWebif of the Enigma2 receiver (e.g., the URL of your decoder's channel list, such as `http://<DECODER_IP>/web/services.m3u`).
- **OUTPUT_DIR**: Directory where the generated `.ts` files will be saved (e.g., `/output`).

#### Hardware
- An Enigma2 receiver (such as a satellite decoder) with OpenWebif enabled, providing an M3U file with the IPTV channels.
- A local network where the Enigma2 receiver and the application host can communicate.
- An Oculus Quest device with a player compatible with IPTV streams (e.g., Bigscreen or MoonVR).

#### Paths
- The application expects the project files to be extracted to a specific path. In the instructions, we will use `/path/to/enigmaToOculus` as an example.
- The generated `.ts` files will be stored in a shared folder accessible by the Oculus Quest. In the instructions, we will use `/path/to/DecoChannels` as an example.

### How to Run the Application

1. **Extract the Project**:
   - Download and extract the project files to a path on your system. For example:
     ```
     /path/to/enigmaToOculus
     ```
   - Ensure the project files (such as `Dockerfile`, `frontend/`, etc.) are inside this folder.

2. **Configure Environment Variables and Paths**:
   Edit the `docker-compose.yml` file to include the necessary environment variables and paths. Below is an example:
   ```yaml
   services:
     iptv-recorder:
       image: iptv-recorder:latest
       build:
         context: /path/to/enigmaToOculus
         dockerfile: Dockerfile
       network_mode: host
       volumes:
         - /path/to/DecoChannels:/output  # Output directory for videos
         - /path/to/enigmaToOculus/cache:/cache  # Persistent cache
       environment:
         - M3U_URL=http://<DECODER_IP>/web/services.m3u  # URL of your decoder's channel list
         - OUTPUT_DIR=/output
       restart: unless-stopped
       container_name: enigmaToOculus

     frontend:
       build:
         context: /path/to/enigmaToOculus/frontend
         dockerfile: Dockerfile
       network_mode: host
       depends_on:
         - iptv-recorder
       restart: unless-stopped
       container_name: enigmaToOculus-frontend
   ```
   - Replace `/path/to/enigmaToOculus` with the path where you extracted the project.
   - Replace `/path/to/DecoChannels` with the path where you want the `.ts` files to be generated. This folder must be a shared folder accessible by the Oculus Quest (e.g., via SMB or a DLNA server).
   - Replace `http://<DECODER_IP>/web/services.m3u` with the actual URL of your Enigma2 decoder's channel list.

3. **Build and Run the Containers**:
   From the directory containing the `docker-compose.yml` file, run:
   ```bash
   docker-compose up --build -d
   ```

4. **Access the Application**:
   - Open a browser on a device within the same network (e.g., your PC or phone) and go to `http://<HOST_IP>:6678` (e.g., `http://192.168.1.25:6678` if your host is `192.168.1.25`).
   - You will see the web interface with the list of channels.

5. **Use the Application**:
   - Use the search field to filter channels by name.
   - Click "Start" to begin streaming a channel. The `.ts` file will be generated in the shared folder (e.g., `/path/to/DecoChannels/LA_1.ts`).
   - While a channel is streaming, a fixed bar will appear at the bottom of the frontend with the channel name and a "Stop" button.
   - Click "Stop" (either in the bar or on the channel) to stop the streaming.
   - If you reload the page, the streaming state will be preserved.

6. **Play the Stream on the Oculus Quest**:
   - Ensure the folder `/path/to/DecoChannels` is a shared folder accessible by the Oculus Quest. This can be achieved using:
     - A DLNA server (e.g., MiniDLNA) to make the `.ts` files available.
     - A network share (SMB) that the Oculus Quest can access.
   - Open a compatible player on the Oculus Quest. This application has been tested with:
     - **Bigscreen**: Using a MiniDLNA container to share the `.ts` files.
     - **MoonVR**: Accessing the `.ts` file directly from a network share (SMB).
   - Select the generated `.ts` file (e.g., `LA_1.ts`) from the player.
   - Enjoy the channel on your Oculus Quest!

---

## Notas Adicionales / Additional Notes

- Asegúrate de que el OpenWebif del receptor Enigma2 esté habilitado y que los canales no estén cifrados (o que el receptor los descifre correctamente).
- Los archivos `.ts` generados se almacenan en la carpeta especificada (`/path/to/DecoChannels` en el ejemplo). Asegúrate de que esta carpeta sea accesible desde las Oculus Quest (por ejemplo, mediante un servidor DLNA o SMB).
- Si encuentras problemas con el streaming (por ejemplo, el archivo `.ts` no se reproduce), verifica los logs del backend para depurar el problema con GStreamer.

- Ensure that the OpenWebif of the Enigma2 receiver is enabled and that the channels are not encrypted (or that the receiver decrypts them correctly).
- The generated `.ts` files are stored in the specified folder (`/path/to/DecoChannels` in the example). Ensure this folder is accessible from the Oculus Quest (e.g., via a DLNA or SMB server).
- If you encounter issues with streaming (e.g., the `.ts` file does not play), check the backend logs to debug the issue with GStreamer.
