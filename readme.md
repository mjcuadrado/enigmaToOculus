
# EnigmaToOculus Backend

## Español

### Descripción

Este proyecto es un backend diseñado para descargar listas de canales IPTV en formato M3U, parsearlas y transmitir los canales a través de GStreamer, sirviendo los streams a un frontend a través de un servidor HTTP. Soporta dos fuentes de listas M3U: una lista local (por ejemplo, desde un receptor Enigma2) y una lista abierta (por ejemplo, canales públicos de TDT). Además, incluye un frontend web que permite a los usuarios controlar la API y gestionar los canales de manera sencilla.

### Características

- Descarga y parsea listas M3U desde dos fuentes:
  - `M3U_URL`: Lista de canales desde un receptor Enigma2 (por ejemplo, `http://192.168.1.100/web/services.m3u?...`).
  - `OPEN_M3U_URL`: Lista de canales abiertos (por ejemplo, `https://www.tdtchannels.com/lists/tv.m3u`).
- Combina ambas listas en una sola, marcando los canales de la lista abierta con el sufijo `(open)` en el nombre.
- Transmite canales usando GStreamer y genera archivos `.ts` para el frontend.
- Proporciona un servidor HTTP con los siguientes endpoints:
  - `/start?channel=<nombre_del_canal>`: Inicia el streaming de un canal.
  - `/stop`: Detiene el streaming actual.
  - `/status`: Devuelve el estado del streaming.
  - `/channels`: Devuelve la lista completa de canales disponibles.
- Cachea las listas M3U para usarlas en caso de fallos de red.
- Maneja errores de descarga de listas M3U de manera robusta, permitiendo que el backend continúe funcionando incluso si una de las listas no está disponible.
- Incluye un **frontend web** en el puerto `6678` para controlar la API y gestionar los canales de manera intuitiva.

### Requisitos previos

- **Docker** (opcional, pero recomendado para una configuración más sencilla).
- **GStreamer**: Necesario para la transmisión de los canales. Asegúrate de que `gst-launch-1.0` esté instalado en el sistema o en el contenedor Docker.
  - En Ubuntu/Debian: `sudo apt-get install gstreamer1.0-tools gstreamer1.0-plugins-good gstreamer1.0-plugins-bad gstreamer1.0-plugins-ugly gstreamer1.0-libav`
- **.NET Core SDK** (si no usas Docker): Necesario para compilar y ejecutar el backend.
- Acceso a internet para descargar la lista abierta (`OPEN_M3U_URL`).
- Un receptor Enigma2 (como un decodificador de satélite) accesible en la red local para la lista `M3U_URL`.
- Un navegador web para acceder al frontend en el puerto `6678`.

### Configuración

#### Variables de entorno

El backend se configura mediante las siguientes variables de entorno:

| Variable        | Descripción                                                                 | Valor por defecto                          | Requerido |
|-----------------|-----------------------------------------------------------------------------|--------------------------------------------|-----------|
| `M3U_URL`       | URL de la lista M3U del receptor Enigma2 (por ejemplo, OpenWebif).          | Ninguno                                    | Sí        |
| `OPEN_M3U_URL`  | URL de la lista M3U de canales abiertos.                                    | `https://www.tdtchannels.com/lists/tv.m3u` | No        |
| `OUTPUT_DIR`    | Directorio donde se guardarán los archivos `.ts` generados por GStreamer.   | Ninguno                                    | Sí        |
| `CHANNELS`      | Lista de canales deseados (separados por comas). **Nota**: Actualmente se ignora para incluir todos los canales. | Ninguno                                    | No        |

#### Configuración con Docker

1. **Crea un archivo `docker-compose.yml`**:

   ```yaml
   version: '3.8'
   services:
     enigmaToOculus-backend:
       image: enigmaToOculus-backend:latest
       build:
         context: .
         dockerfile: Dockerfile.backend
       environment:
         - M3U_URL=http://192.168.1.100/web/services.m3u?...
         - OPEN_M3U_URL=https://www.tdtchannels.com/lists/tv.m3u
         - OUTPUT_DIR=/output
         - CHANNELS=  # Dejar vacío para incluir todos los canales
       ports:
         - "6677:6677"
       volumes:
         - ./cache:/cache
         - ./output:/output

     enigmaToOculus-frontend:
       image: enigmaToOculus-frontend:latest
       build:
         context: ./frontend
         dockerfile: Dockerfile.frontend
       ports:
         - "6678:6678"
       depends_on:
         - enigmaToOculus-backend
   ```

2. **Crea un archivo `Dockerfile.backend`** para el backend (si no lo tienes):

   ```dockerfile
   # Usa una imagen base con .NET Core y GStreamer
   FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
   WORKDIR /app

   # Instala GStreamer y dependencias
   RUN apt-get update && apt-get install -y \
       gstreamer1.0-tools \
       gstreamer1.0-plugins-good \
       gstreamer1.0-plugins-bad \
       gstreamer1.0-plugins-ugly \
       gstreamer1.0-libav \
       && rm -rf /var/lib/apt/lists/*

   # Copia el código fuente del backend
   COPY . .

   # Compila el proyecto
   RUN dotnet publish -c Release -o out

   # Crea la imagen final
   FROM mcr.microsoft.com/dotnet/aspnet:6.0
   WORKDIR /app
   COPY --from=build /app/out .

   # Instala GStreamer en la imagen final
   RUN apt-get update && apt-get install -y \
       gstreamer1.0-tools \
       gstreamer1.0-plugins-good \
       gstreamer1.0-plugins-bad \
       gstreamer1.0-plugins-ugly \
       gstreamer1.0-libav \
       && rm -rf /var/lib/apt/lists/*

   # Expone el puerto del servidor HTTP
   EXPOSE 6677

   # Inicia el backend
   ENTRYPOINT ["dotnet", "enigmaToOculus.dll"]
   ```

3. **Crea un archivo `Dockerfile.frontend`** para el frontend (ajusta según tu tecnología de frontend, por ejemplo, Node.js):

   ```dockerfile
   # Usa una imagen base para Node.js (ajusta según tu frontend)
   FROM node:18-alpine
   WORKDIR /app

   # Copia los archivos del frontend
   COPY ./frontend .

   # Instala las dependencias
   RUN npm install

   # Construye el frontend (si es necesario)
   RUN npm run build

   # Expone el puerto del frontend
   EXPOSE 6678

   # Inicia el servidor del frontend
   CMD ["npm", "start"]
   ```

   **Nota**: Este `Dockerfile.frontend` es un ejemplo genérico para un frontend basado en Node.js. Ajusta los comandos (`npm run build`, `npm start`, etc.) según la configuración específica de tu frontend.

4. **Construye y ejecuta los contenedores**:

   ```bash
   docker-compose up --build -d
   ```

5. **Verifica los logs**:

   - Para el backend:
     ```bash
     docker logs enigmaToOculus-backend
     ```
     Deberías ver mensajes como:
     ```
     Descargando lista M3U desde http://192.168.1.100/web/services.m3u?...
     Lista M3U descargada y cacheada.
     Total de canales parseados desde OpenWebif: 191
     Descargando lista M3U desde https://www.tdtchannels.com/lists/tv.m3u...
     Lista M3U descargada y cacheada.
     Total de canales parseados desde la lista abierta: X
     Total de canales combinados: Y
     Total de canales (sin filtrar): Y
     Canales parseados:
     - LA 1
     - LA 2
     - ANTENA 3
     ...
     - Channel 1 (open)
     - Channel 2 (open)
     ...
     Servidor HTTP iniciado. Escuchando en http://*:6677/
     ```

   - Para el frontend:
     ```bash
     docker logs enigmaToOculus-frontend
     ```
     Deberías ver mensajes indicando que el frontend está sirviendo en el puerto `6678`.

#### Configuración sin Docker

1. **Clona o copia el código fuente**:

   Asegúrate de tener el código del backend y del frontend en directorios locales.

2. **Instala GStreamer** (para el backend):

   En Ubuntu/Debian:

   ```bash
   sudo apt-get update
   sudo apt-get install gstreamer1.0-tools gstreamer1.0-plugins-good gstreamer1.0-plugins-bad gstreamer1.0-plugins-ugly gstreamer1.0-libav
   ```

3. **Configura las variables de entorno para el backend**:

   En Linux:

   ```bash
   export M3U_URL="http://192.168.1.100/web/services.m3u?..."
   export OPEN_M3U_URL="https://www.tdtchannels.com/lists/tv.m3u"
   export OUTPUT_DIR="/path/to/output"
   export CHANNELS=""
   ```

4. **Compila y ejecuta el backend**:

   ```bash
   cd backend
   dotnet build -c Release
   cd bin/Release/net6.0
   dotnet enigmaToOculus.dll
   ```

5. **Instala y ejecuta el frontend** (ajusta según tu tecnología de frontend, por ejemplo, Node.js):

   ```bash
   cd frontend
   npm install
   npm start
   ```

   Asegúrate de que el frontend esté configurado para escuchar en el puerto `6678`.

6. **Verifica los logs**:

   Revisa la salida en la consola del backend y del frontend para confirmar que ambos servicios están funcionando correctamente.

### Uso

#### Acceso al Frontend

El frontend web está disponible en el puerto `6678`. Puedes acceder a él desde un navegador web para controlar la API y gestionar los canales de manera intuitiva.

- **URL del frontend**: `http://<tu-ip>:6678`  
  - Ejemplo: `http://192.168.1.165:6678`

El frontend proporciona una interfaz gráfica donde puedes:
- Ver la lista completa de canales disponibles (de ambas listas M3U).
- Iniciar el streaming de un canal con un solo clic.
- Detener el streaming actual.
- Ver el estado del streaming en tiempo real.

#### Endpoints del servidor HTTP (Backend)

El backend inicia un servidor HTTP en el puerto `6677`. Los siguientes endpoints están disponibles para interactuar directamente con la API (el frontend los utiliza internamente):

- **GET `/start?channel=<nombre_del_canal>`**  
  Inicia el streaming de un canal específico.  
  - Ejemplo: `http://192.168.1.165:6677/start?channel=LA%201`  
  - Respuesta: Ruta del archivo `.ts` generado (por ejemplo, `/output/LA_1.ts`).

- **GET `/stop`**  
  Detiene el streaming actual y elimina los archivos `.ts` generados.  
  - Ejemplo: `http://192.168.1.165:6677/stop`  
  - Respuesta: Mensaje de confirmación.

- **GET `/status`**  
  Devuelve el estado del streaming actual.  
  - Ejemplo: `http://192.168.1.165:6677/status`  
  - Respuesta (JSON):  
    ```json
    {
      "isStreaming": true,
      "channelName": "LA 1",
      "startTime": "2025-03-23T12:34:56.789Z"
    }
    ```

- **GET `/channels`**  
  Devuelve la lista completa de canales disponibles (de ambas listas M3U).  
  - Ejemplo: `http://192.168.1.165:6677/channels`  
  - Respuesta (JSON):  
    ```json
    [
      { "tvgName": "LA 1", "tvgLogo": "http://example.com/la1.png" },
      { "tvgName": "ANTENA 3", "tvgLogo": "http://example.com/antena3.png" },
      { "tvgName": "Channel 1 (open)", "tvgLogo": "http://example.com/channel1.png" },
      ...
    ]
    ```

#### Notas sobre las listas abiertas

- Los canales de la lista `OPEN_M3U_URL` se identifican con el sufijo `(open)` en el nombre (por ejemplo, `Channel 1 (open)`).
- Si la descarga de `OPEN_M3U_URL` falla, el backend continuará funcionando con los canales de `M3U_URL` y mostrará un mensaje de error en los logs.
- Asegúrate de que la URL especificada en `OPEN_M3U_URL` sea accesible y contenga una lista M3U válida con el formato esperado:
  ```
  #EXTM3U
  #EXTINF:-1 tvg-name="Channel 1" tvg-logo="http://example.com/logo.png",Channel 1
  http://example.com/stream1
  ```

### Depuración

Si encuentras problemas, revisa los logs del backend para identificar el error:

- **Lista no descargada**:
  ```
  Error HTTP al descargar la lista M3U: The remote server returned an error: (404) Not Found.
  Status Code: 404
  ```
  - Solución: Verifica que la URL (`M3U_URL` o `OPEN_M3U_URL`) sea correcta y accesible.

- **Problemas de red**:
  ```
  Error general al descargar la lista M3U: A connection attempt failed...
  ```
  - Solución: Asegúrate de que el contenedor o máquina tenga acceso a internet.

- **Lista vacía**:
  ```
  Total de canales parseados desde la lista abierta: 0
  ```
  - Solución: Descarga manualmente la lista M3U y verifica que tenga el formato correcto (`#EXTINF` con `tvg-name` y URLs válidas).

### Contribuciones

Si deseas contribuir al proyecto, por favor crea un *pull request* con tus cambios. Asegúrate de incluir pruebas y documentación para cualquier nueva funcionalidad.

### Licencia

Este proyecto está licenciado bajo la [Licencia MIT](LICENSE).

---

## English

### Description

This project is a backend designed to download IPTV channel lists in M3U format, parse them, and stream the channels using GStreamer, serving the streams to a frontend via an HTTP server. It supports two M3U list sources: a local list (e.g., from an Enigma2 receiver) and an open list (e.g., public TDT channels). Additionally, it includes a web frontend that allows users to control the API and manage channels easily.

### Features

- Downloads and parses M3U lists from two sources:
  - `M3U_URL`: Channel list from an Enigma2 receiver (e.g., `http://192.168.1.100/web/services.m3u?...`).
  - `OPEN_M3U_URL`: Open channel list (e.g., `https://www.tdtchannels.com/lists/tv.m3u`).
- Combines both lists into a single one, marking channels from the open list with the suffix `(open)` in their names.
- Streams channels using GStreamer and generates `.ts` files for the frontend.
- Provides an HTTP server with the following endpoints:
  - `/start?channel=<channel_name>`: Starts streaming a specific channel.
  - `/stop`: Stops the current stream.
  - `/status`: Returns the streaming status.
  - `/channels`: Returns the full list of available channels.
- Caches M3U lists for use in case of network failures.
- Robustly handles M3U list download errors, allowing the backend to continue running even if one of the lists is unavailable.
- Includes a **web frontend** on port `6678` to control the API and manage channels intuitively.

### Prerequisites

- **Docker** (optional, but recommended for easier setup).
- **GStreamer**: Required for streaming channels. Ensure `gst-launch-1.0` is installed on the system or in the Docker container.
  - On Ubuntu/Debian: `sudo apt-get install gstreamer1.0-tools gstreamer1.0-plugins-good gstreamer1.0-plugins-bad gstreamer1.0-plugins-ugly gstreamer1.0-libav`
- **.NET Core SDK** (if not using Docker): Required to build and run the backend.
- Internet access to download the open list (`OPEN_M3U_URL`).
- An Enigma2 receiver (such as a satellite decoder) accessible on the local network for the `M3U_URL` list.
- A web browser to access the frontend on port `6678`.

### Setup

#### Environment Variables

The backend is configured using the following environment variables:

| Variable        | Description                                                                 | Default Value                              | Required |
|-----------------|-----------------------------------------------------------------------------|--------------------------------------------|----------|
| `M3U_URL`       | URL of the M3U list from the Enigma2 receiver (e.g., OpenWebif).            | None                                       | Yes      |
| `OPEN_M3U_URL`  | URL of the open M3U channel list.                                           | `https://www.tdtchannels.com/lists/tv.m3u` | No       |
| `OUTPUT_DIR`    | Directory where GStreamer-generated `.ts` files will be saved.              | None                                       | Yes      |
| `CHANNELS`      | List of desired channels (comma-separated). **Note**: Currently ignored to include all channels. | None                                       | No       |

#### Setup with Docker

1. **Create a `docker-compose.yml` file**:

   ```yaml
   version: '3.8'
   services:
     enigmaToOculus-backend:
       image: enigmaToOculus-backend:latest
       build:
         context: .
         dockerfile: Dockerfile.backend
       environment:
         - M3U_URL=http://192.168.1.100/web/services.m3u?...
         - OPEN_M3U_URL=https://www.tdtchannels.com/lists/tv.m3u
         - OUTPUT_DIR=/output
         - CHANNELS=  # Leave empty to include all channels
       ports:
         - "6677:6677"
       volumes:
         - ./cache:/cache
         - ./output:/output

     enigmaToOculus-frontend:
       image: enigmaToOculus-frontend:latest
       build:
         context: ./frontend
         dockerfile: Dockerfile.frontend
       ports:
         - "6678:6678"
       depends_on:
         - enigmaToOculus-backend
   ```

2. **Create a `Dockerfile.backend` for the backend** (if you don’t already have one):

   ```dockerfile
   # Use a base image with .NET Core and GStreamer
   FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
   WORKDIR /app

   # Install GStreamer and dependencies
   RUN apt-get update && apt-get install -y \
       gstreamer1.0-tools \
       gstreamer1.0-plugins-good \
       gstreamer1.0-plugins-bad \
       gstreamer1.0-plugins-ugly \
       gstreamer1.0-libav \
       && rm -rf /var/lib/apt/lists/*

   # Copy the backend source code
   COPY . .

   # Build the project
   RUN dotnet publish -c Release -o out

   # Create the final image
   FROM mcr.microsoft.com/dotnet/aspnet:6.0
   WORKDIR /app
   COPY --from=build /app/out .

   # Install GStreamer in the final image
   RUN apt-get update && apt-get install -y \
       gstreamer1.0-tools \
       gstreamer1.0-plugins-good \
       gstreamer1.0-plugins-bad \
       gstreamer1.0-plugins-ugly \
       gstreamer1.0-libav \
       && rm -rf /var/lib/apt/lists/*

   # Expose the HTTP server port
   EXPOSE 6677

   # Start the backend
   ENTRYPOINT ["dotnet", "enigmaToOculus.dll"]
   ```

3. **Create a `Dockerfile.frontend` for the frontend** (adjust based on your frontend technology, e.g., Node.js):

   ```dockerfile
   # Use a base image for Node.js (adjust based on your frontend)
   FROM node:18-alpine
   WORKDIR /app

   # Copy the frontend files
   COPY ./frontend .

   # Install dependencies
   RUN npm install

   # Build the frontend (if necessary)
   RUN npm run build

   # Expose the frontend port
   EXPOSE 6678

   # Start the frontend server
   CMD ["npm", "start"]
   ```

   **Note**: This `Dockerfile.frontend` is a generic example for a Node.js-based frontend. Adjust the commands (`npm run build`, `npm start`, etc.) based on your specific frontend setup.

4. **Build and run the containers**:

   ```bash
   docker-compose up --build -d
   ```

5. **Check the logs**:

   - For the backend:
     ```bash
     docker logs enigmaToOculus-backend
     ```
     You should see messages like:
     ```
     Downloading M3U list from http://192.168.1.100/web/services.m3u?...
     M3U list downloaded and cached.
     Total channels parsed from OpenWebif: 191
     Downloading M3U list from https://www.tdtchannels.com/lists/tv.m3u...
     M3U list downloaded and cached.
     Total channels parsed from the open list: X
     Total combined channels: Y
     Total channels (unfiltered): Y
     Parsed channels:
     - LA 1
     - LA 2
     - ANTENA 3
     ...
     - Channel 1 (open)
     - Channel 2 (open)
     ...
     HTTP server started. Listening on http://*:6677/
     ```

   - For the frontend:
     ```bash
     docker logs enigmaToOculus-frontend
     ```
     You should see messages indicating that the frontend is serving on port `6678`.

#### Setup without Docker

1. **Clone or copy the source code**:

   Ensure you have the backend and frontend code in local directories.

2. **Install GStreamer** (for the backend):

   On Ubuntu/Debian:

   ```bash
   sudo apt-get update
   sudo apt-get install gstreamer1.0-tools gstreamer1.0-plugins-good gstreamer1.0-plugins-bad gstreamer1.0-plugins-ugly gstreamer1.0-libav
   ```

3. **Set up environment variables for the backend**:

   On Linux:

   ```bash
   export M3U_URL="http://192.168.1.100/web/services.m3u?..."
   export OPEN_M3U_URL="https://www.tdtchannels.com/lists/tv.m3u"
   export OUTPUT_DIR="/path/to/output"
   export CHANNELS=""
   ```

4. **Build and run the backend**:

   ```bash
   cd backend
   dotnet build -c Release
   cd bin/Release/net6.0
   dotnet enigmaToOculus.dll
   ```

5. **Install and run the frontend** (adjust based on your frontend technology, e.g., Node.js):

   ```bash
   cd frontend
   npm install
   npm start
   ```

   Ensure the frontend is configured to listen on port `6678`.

6. **Check the logs**:

   Review the console output for both the backend and frontend to confirm that both services are running correctly.

### Usage

#### Accessing the Frontend

The web frontend is available on port `6678`. You can access it from a web browser to control the API and manage channels intuitively.

- **Frontend URL**: `http://<your-ip>:6678`  
  - Example: `http://192.168.1.165:6678`

The frontend provides a graphical interface where you can:
- View the full list of available channels (from both M3U lists).
- Start streaming a channel with a single click.
- Stop the current stream.
- View the real-time streaming status.

#### HTTP Server Endpoints (Backend)

The backend starts an HTTP server on port `6677`. The following endpoints are available for direct interaction with the API (the frontend uses them internally):

- **GET `/start?channel=<channel_name>`**  
  Starts streaming a specific channel.  
  - Example: `http://192.168.1.165:6677/start?channel=LA%201`  
  - Response: Path to the generated `.ts` file (e.g., `/output/LA_1.ts`).

- **GET `/stop`**  
  Stops the current stream and deletes the generated `.ts` files.  
  - Example: `http://192.168.1.165:6677/stop`  
  - Response: Confirmation message.

- **GET `/status`**  
  Returns the current streaming status.  
  - Example: `http://192.168.1.165:6677/status`  
  - Response (JSON):  
    ```json
    {
      "isStreaming": true,
      "channelName": "LA 1",
      "startTime": "2025-03-23T12:34:56.789Z"
    }
    ```

- **GET `/channels`**  
  Returns the full list of available channels (from both M3U lists).  
  - Example: `http://192.168.1.165:6677/channels`  
  - Response (JSON):  
    ```json
    [
      { "tvgName": "LA 1", "tvgLogo": "http://example.com/la1.png" },
      { "tvgName": "ANTENA 3", "tvgLogo": "http://example.com/antena3.png" },
      { "tvgName": "Channel 1 (open)", "tvgLogo": "http://example.com/channel1.png" },
      ...
    ]
    ```

#### Notes on Open Lists

- Channels from the `OPEN_M3U_URL` list are identified with the suffix `(open)` in their names (e.g., `Channel 1 (open)`).
- If the download of `OPEN_M3U_URL` fails, the backend will continue running with the channels from `M3U_URL` and log an error message.
- Ensure that the URL specified in `OPEN_M3U_URL` is accessible and contains a valid M3U list in the expected format:
  ```
  #EXTM3U
  #EXTINF:-1 tvg-name="Channel 1" tvg-logo="http://example.com/logo.png",Channel 1
  http://example.com/stream1
  ```

### Troubleshooting

If you encounter issues, check the backend logs to identify the error:

- **List not downloaded**:
  ```
  HTTP error downloading M3U list: The remote server returned an error: (404) Not Found.
  Status Code: 404
  ```
  - Solution: Verify that the URL (`M3U_URL` or `OPEN_M3U_URL`) is correct and accessible.

- **Network issues**:
  ```
  General error downloading M3U list: A connection attempt failed...
  ```
  - Solution: Ensure the container or machine has internet access.

- **Empty list**:
  ```
  Total channels parsed from the open list: 0
  ```
  - Solution: Manually download the M3U list and verify it has the correct format (`#EXTINF` with `tvg-name` and valid URLs).

### Contributing

If you’d like to contribute to the project, please create a *pull request* with your changes. Be sure to include tests and documentation for any new features.

### License

This project is licensed under the [MIT License](LICENSE).
