# =========================================
# Multi-APP_LANGUAGE Dockerfile (Production-ready)
# Supports: .NET, Java, Node.js, Python
# =========================================

# ---------- Stage 1: Base runtime images ----------
ARG APP_LANGUAGE=csharp
ARG LANGUAGE_VERSION=8.0
ARG ENVIRONMENT=dev

FROM mcr.microsoft.com/dotnet/aspnet:${LANGUAGE_VERSION} AS csharp-runtime
FROM eclipse-temurin:${LANGUAGE_VERSION}-jre AS java-runtime
FROM node:${LANGUAGE_VERSION}-alpine AS node-runtime
FROM python:${LANGUAGE_VERSION}-slim AS python-runtime

# ---------- Stage 2: Final image ----------
FROM ${APP_LANGUAGE}-runtime AS final
WORKDIR /app

# Copy built artifacts from CI/CD workflow
ARG APP_PATH=./output
COPY ${APP_PATH}/ ./

# ---------- Stage 3: General environment variables ----------
# Only truly immutable defaults (safe for all languages)
ENV PORT=8080

# ---------- Stage 4: Non-root user ----------
RUN addgroup --system appgroup && \
    adduser --system --ingroup appgroup appuser && \
    chown -R appuser:appgroup /app
USER appuser

# ---------- Stage 5: Healthcheck ----------
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl --fail http://localhost:$PORT/health || exit 1

EXPOSE $PORT

# ---------- Stage 6: Dynamic entrypoint ----------
CMD ["sh", "-c", "\
if [ "$APP_LANGUAGE" = "csharp" ]; then \
    DLL_FILE=$(ls *.runtimeconfig.json | sed "s/\.runtimeconfig\.json$/.dll/") ; \
    exec dotnet "$DLL_FILE" ; \
elif [ \"$APP_LANGUAGE\" = 'java' ]; then \
    JAR_FILE=$(ls *.jar | head -n 1); \
    PROFILE=${ENVIRONMENT:-default}; \
    exec java -jar $JAR_FILE --spring.profiles.active=$PROFILE; \
elif [ \"$APP_LANGUAGE\" = 'node' ]; then \
    ENTRY_FILE=$(ls *.js | head -n 1); \
    exec node $ENTRY_FILE; \
elif [ \"$APP_LANGUAGE\" = 'python' ]; then \
    ENTRY_FILE=$(ls *.py | head -n 1); \
    exec python $ENTRY_FILE; \
else \
    echo \"Unsupported language: $APP_LANGUAGE\"; exit 1; \
fi"]