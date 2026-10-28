FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
ARG DRIA_API_URL=/api
ENV DRIA_API_URL=${DRIA_API_URL}
RUN npm run build

FROM nginx:stable-alpine AS runtime
# x-release-please-start-version
ARG DRIA_VERSION=0.8.1
# x-release-please-end
ARG DRIA_REVISION=local
LABEL org.opencontainers.image.title="DrIA Web" \
    org.opencontainers.image.version="${DRIA_VERSION}" \
    org.opencontainers.image.revision="${DRIA_REVISION}"
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/spa /usr/share/nginx/html

EXPOSE 80
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s \
    CMD wget -q -O /dev/null http://127.0.0.1/healthz || exit 1
