# Estágio 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Instala as dependências nativas para AOT
RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev

WORKDIR /src

# Copia e restaura os pacotes
COPY rinha-2025-rafael.csproj .
RUN dotnet restore rinha-2025-rafael.csproj

# Copia o resto do código
COPY . .

# Publica a aplicação em modo AOT
# A mudança crucial está na linha '--self-contained true'
RUN dotnet publish rinha-2025-rafael.csproj \
    -c Release \
    -r linux-x64 \
    -o /app/publish \
    /p:PublishAot=true \
    --self-contained true \
    /p:StripSymbols=true

# Estágio 2: Final
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["./rinha-2025-rafael"]
