# Stage 1: build Angular
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json ./
RUN npm install --legacy-peer-deps

COPY . .
RUN npm run build:prod

# Mostra o que foi gerado para debug
RUN echo "=== dist ===" && find /app/dist -type f | head -20

# Stage 2: serve com nginx
FROM nginx:1.25-alpine

# Copia independente do caminho exato gerado pelo Angular 17
COPY --from=build /app/dist /tmp/dist

# Script que encontra index.html e copia para o nginx
RUN sh -c '\
  HTML=$(find /tmp/dist -name "index.html" | head -1); \
  DIR=$(dirname "$HTML"); \
  echo "Copiando de: $DIR"; \
  cp -r "$DIR/." /usr/share/nginx/html/; \
  echo "Arquivos no nginx:"; ls /usr/share/nginx/html/ \
'

COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
