# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY frontend/package*.json ./
RUN npm install --quiet

# Copy source code and build
COPY frontend/ .
RUN npm run build -- --configuration=production

# Runtime stage with nginx
FROM nginx:alpine
COPY --from=build /app/dist/nl2sql-frontend/browser /usr/share/nginx/html

# Copy custom nginx config
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
