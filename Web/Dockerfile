# Portail web Aetheria — image de déploiement (Render, ou tout hébergeur Docker).
# Le contexte de build DOIT être la racine du dépôt : ce projet référence Shared/ et Database/.
#
#   docker build -f Web/Dockerfile -t aetheria-web .
#
# Voir Docs/Deploiement-Web.md.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Shared/ Shared/
COPY Database/ Database/
COPY Web/ Web/

RUN dotnet publish Web/Aetheria.Web.csproj -c Release -o /app -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# libgssapi-krb5-2 : évite l'avertissement Npgsql « Cannot load library libgssapi_krb5.so.2 »
# au démarrage (probe d'authentification intégrée ; Neon utilise SCRAM/mot de passe).
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

ENV ASPNETCORE_ENVIRONMENT=Production
# Render fournit $PORT ; en local, défaut 8080. Program.cs lit PORT.
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Aetheria.Web.dll"]
