FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ConsultasPlan.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:5000 \
    ConnectionStrings__Default="Data Source=/data/consultas.db"
VOLUME /data
EXPOSE 5000
ENTRYPOINT ["dotnet", "ConsultasPlan.dll"]
