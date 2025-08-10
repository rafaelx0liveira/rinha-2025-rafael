# Estágio 1: Build
# Usando a imagem completa do SDK do .NET 9 para ter todas as ferramentas.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Instala as dependências nativas necessárias para a compilação AOT (Ahead-of-Time).
# - clang: compilador C++ usado pelo processo AOT.
# - zlib1g-dev: biblioteca de compressão que pode ser necessária.
# Usando 'apt-get' pois a imagem base do SDK é baseada em Debian.
RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev

# Define o diretório de trabalho dentro do container.
WORKDIR /src

# Copia os arquivos de projeto (.csproj) primeiro e restaura os pacotes.
# Isso aproveita o cache do Docker. Se os pacotes não mudarem, essa camada não será executada novamente.
COPY ["rinha-2025-rafael/rinha-2025-rafael.csproj", "rinha-2025-rafael/"]
RUN dotnet restore "./rinha-2025-rafael/rinha-2025-rafael.csproj"

# Copia todo o resto do código fonte.
COPY . .
WORKDIR "/src/rinha-2025-rafael"

# Publicando a aplicação em modo AOT.
# -c Release: Compila em modo de Release (otimizado).
# -r linux-x64: Especifica o runtime de destino.
# /p:PublishAot=true: HABILITA A COMPILAÇÃO AHEAD-OF-TIME.
# --self-contained false: Não inclui o runtime do .NET, pois a imagem final já terá as dependências.
# /p:StripSymbols=true: Remove símbolos de depuração para diminuir o tamanho do executável.
RUN dotnet publish "./rinha-2025-rafael.csproj" \
    -c Release \
    -r linux-x64 \
    -o /app/publish \
    /p:PublishAot=true \
    --self-contained false \
    /p:StripSymbols=true

# Estágio 2: Final
# A imagem final é a 'runtime-deps'. Ela é MÍNIMA.
# Contém apenas as dependências do sistema operacional (libc, etc.) necessárias para rodar um executável nativo.
# Não contém o runtime do .NET, pois nosso executável AOT é autossuficiente.
# Usamos a versão com '-alpine' para uma imagem ainda menor.
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine AS final

WORKDIR /app
EXPOSE 8080

# Copia APENAS o resultado da publicação do estágio de build.
COPY --from=build /app/publish .

# Define o ponto de entrada para ser o nosso executável nativo.
ENTRYPOINT ["./rinha-2025-rafael"]
