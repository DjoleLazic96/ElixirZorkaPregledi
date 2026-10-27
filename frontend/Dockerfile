# =============================================================================
# TPM pregledi — frontend (React + Vite) production image (Elixir blueprint 17 §4).
# Build kontekst: frontend/
# API base URL je relativan ('/api/v1') — bundle ne zna domen; isti image za sva okruženja.
# =============================================================================

# ─── 1) build ────────────────────────────────────────────────────────────────
FROM node:22-alpine AS build
WORKDIR /src

# package-lock.json je OBAVEZAN (npm ci bez njega puca) — zato bez glob-a.
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

# ARG/ENV tek posle `npm ci`: SHA se menja svaki commit i ne sme da ruši npm cache layer.
# vite.config.ts čita APP_VERSION/GIT_SHA u `define` (__APP_VERSION__, __GIT_SHA__).
ARG APP_VERSION=0.0.0-dev
ARG GIT_SHA=dev
ENV APP_VERSION=${APP_VERSION} GIT_SHA=${GIT_SHA}

COPY tsconfig*.json vite.config.ts index.html ./
COPY public/ ./public/
COPY src/ ./src/

ENV NODE_ENV=production
RUN npm run build

# ─── 2) runtime (nginx) ──────────────────────────────────────────────────────
# nginx stable grana (paran minor); pri release-u proveriti tekući stable tag.
FROM nginx:1.30-alpine AS final

RUN rm /etc/nginx/conf.d/default.conf
COPY nginx.conf /etc/nginx/conf.d/tpm-pregledi.conf
# Snippet sa sigurnosnim hederima — include-uje ga nginx.conf u server bloku i u svakom
# location-u koji ima svoj add_header (nginx ne nasleđuje add_header). Ne sme u conf.d/.
COPY nginx-security-headers.conf /etc/nginx/snippets/security-headers.conf

COPY --from=build /src/dist /usr/share/nginx/html

# Non-root nginx (uid 101): pid u /tmp, logovi na stdout/stderr.
RUN sed -i \
    -e 's|^pid .*|pid /tmp/nginx.pid;|' \
    -e 's|/var/log/nginx/access.log|/dev/stdout|' \
    -e 's|/var/log/nginx/error.log|/dev/stderr|' \
    /etc/nginx/nginx.conf \
 && chown -R nginx:nginx /var/cache/nginx /usr/share/nginx/html

ARG APP_VERSION=0.0.0-dev
ARG GIT_SHA=dev
ARG BUILD_UTC=
LABEL org.opencontainers.image.title="tpm-pregledi-frontend" \
      org.opencontainers.image.version="${APP_VERSION}" \
      org.opencontainers.image.revision="${GIT_SHA}" \
      org.opencontainers.image.created="${BUILD_UTC}"

USER nginx
EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --quiet --spider http://127.0.0.1:8080/ || exit 1
# Default CMD (nginx -g 'daemon off;') iz base image-a.
