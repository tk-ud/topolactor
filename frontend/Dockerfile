FROM denoland/deno:2.3.1
WORKDIR /app
# Build context is the repo root (docker-compose sets context: ..)
COPY . .
# Pre-cache Fresh and main dependencies
RUN deno cache frontend/main.ts || true
EXPOSE 8000
CMD ["deno", "run", "-A", "frontend/main.ts"]
