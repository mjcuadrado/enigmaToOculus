# Usar una imagen base con .NET 8.0 SDK para compilar la aplicación
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copiar los archivos del proyecto y restaurar las dependencias
COPY *.csproj ./
RUN dotnet restore

# Copiar el resto de los archivos y compilar la aplicación
COPY . ./
RUN dotnet publish -c Release -o out

# Usar una imagen base con .NET 8.0 runtime para la ejecución
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Evitar interacciones durante la instalación
ENV DEBIAN_FRONTEND=noninteractive

# Instalar GStreamer y sus plugins, incluyendo gstreamer1.0-libav para avenc_aac
RUN apt-get update && \
    apt-get install -y \
    gstreamer1.0-tools \
    gstreamer1.0-plugins-base \
    gstreamer1.0-plugins-good \
    gstreamer1.0-plugins-bad \
    gstreamer1.0-plugins-ugly \
    gstreamer1.0-libav \
    && apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Crear un usuario no root
RUN useradd -m -s /bin/bash appuser

# Crear directorios y establecer permisos
RUN mkdir -p /cache /output && \
    chown -R appuser:appuser /app /cache /output && \
    chmod -R 777 /cache /output

# Copiar los archivos compilados desde la etapa de build
COPY --from=build --chown=appuser:appuser /app/out .

# Cambiar al usuario no root
USER appuser

# Exponer el puerto 6677 para la API
EXPOSE 6677

# Definir el punto de entrada
ENTRYPOINT ["dotnet", "enigmaToOculus.dll"]