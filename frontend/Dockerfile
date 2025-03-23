# Usar una imagen base de Nginx
FROM nginx:alpine

# Copiar el archivo index.html al directorio de Nginx
COPY index.html /usr/share/nginx/html/

# Copiar el archivo de configuración de Nginx
COPY nginx.conf /etc/nginx/nginx.conf

# Exponer el puerto 6678
EXPOSE 6678

# Iniciar Nginx
CMD ["nginx", "-g", "daemon off;"]